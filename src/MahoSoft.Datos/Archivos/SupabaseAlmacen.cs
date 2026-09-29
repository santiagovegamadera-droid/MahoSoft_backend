using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MahoSoft.Entidades;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MahoSoft.Datos.Archivos;

/// <summary>Settings of the "Supabase" section. With Url and ServiceKey set, documents go to Supabase Storage.</summary>
public class SupabaseOptions
{
    public const string Seccion = "Supabase";

    /// <summary>Project URL, e.g. https://abcd1234.supabase.co</summary>
    public string Url { get; set; } = "";

    /// <summary>Secret key (sb_secret_…) or the legacy service_role key. Server only: it bypasses every policy.</summary>
    public string ServiceKey { get; set; } = "";

    /// <summary>Private bucket for the documents; created on first use if missing.</summary>
    public string Bucket { get; set; } = "documentos";

    public bool Configurado => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(ServiceKey);
}

/// <summary>
/// Documents in a private Supabase Storage bucket, through its REST API. Only the API can read them: files are
/// served by the API to signed-in users, never by public links.
/// </summary>
public class SupabaseAlmacen : IAlmacenDocumentos
{
    private static readonly Dictionary<string, string> TiposMime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
    };

    private readonly HttpClient _http;
    private readonly string _bucket;
    private readonly ILogger<SupabaseAlmacen> _logger;
    private readonly SemaphoreSlim _bucketListo = new(1, 1);
    private bool _bucketCreado;

    public SupabaseAlmacen(IOptions<SupabaseOptions> options, ILogger<SupabaseAlmacen> logger)
    {
        var o = options.Value;
        _bucket = o.Bucket;
        _logger = logger;
        _http = new HttpClient { BaseAddress = new Uri(o.Url.TrimEnd('/') + "/storage/v1/"), Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.Add("apikey", o.ServiceKey);
        // Legacy keys are JWTs and also go as Bearer; the new sb_secret_ keys only go in "apikey"
        if (o.ServiceKey.StartsWith("eyJ", StringComparison.Ordinal))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", o.ServiceKey);
    }

    public AlmacenArchivo Almacen => AlmacenArchivo.Supabase;

    public async Task<string> GuardarAsync(
        Stream contenido,
        string subcarpeta,
        string extension,
        CancellationToken ct = default
    )
    {
        await AsegurarBucketAsync(ct);
        var ruta = IAlmacenDocumentos.NuevaRuta(subcarpeta, extension);
        using var cuerpo = new StreamContent(contenido);
        cuerpo.Headers.ContentType = new MediaTypeHeaderValue(
            TiposMime.GetValueOrDefault(extension, "application/octet-stream")
        );
        using var r = await _http.PostAsync(Objeto(ruta), cuerpo, ct);
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Supabase Storage no guardó el archivo ({(int)r.StatusCode}): {await r.Content.ReadAsStringAsync(ct)}"
            );
        return ruta;
    }

    public async Task<Stream?> AbrirAsync(string ruta, CancellationToken ct = default)
    {
        var r = await _http.GetAsync($"object/authenticated/{_bucket}/{Codificar(ruta)}", ct);
        // Storage answers 400 "Object not found" as well as 404
        if (r.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            r.Dispose();
            return null;
        }
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadAsStreamAsync(ct);
    }

    public async Task EliminarAsync(string ruta)
    {
        try
        {
            using var r = await _http.DeleteAsync(Objeto(ruta));
            if (!r.IsSuccessStatusCode)
                _logger.LogWarning("No se pudo borrar {Ruta} de Supabase ({Estado})", ruta, (int)r.StatusCode);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "No se pudo borrar {Ruta} de Supabase", ruta);
        }
    }

    /// <summary>Creates the private bucket the first time; "already exists" is fine.</summary>
    private async Task AsegurarBucketAsync(CancellationToken ct)
    {
        if (_bucketCreado)
            return;
        await _bucketListo.WaitAsync(ct);
        try
        {
            if (_bucketCreado)
                return;
            using var r = await _http.PostAsJsonAsync("bucket", new { id = _bucket, name = _bucket, @public = false }, ct);
            var texto = await r.Content.ReadAsStringAsync(ct);
            if (!r.IsSuccessStatusCode && !texto.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Supabase Storage no creó el bucket ({(int)r.StatusCode}): {texto}");
            _bucketCreado = true;
        }
        finally
        {
            _bucketListo.Release();
        }
    }

    private string Objeto(string ruta) => $"object/{_bucket}/{Codificar(ruta)}";

    private static string Codificar(string ruta) => string.Join('/', ruta.Split('/').Select(Uri.EscapeDataString));
}
