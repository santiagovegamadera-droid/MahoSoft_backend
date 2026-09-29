using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace MahoSoft.Pruebas;

/// <summary>
/// The whole API running in memory against its own database (MahoSoft_Pruebas), recreated with the sample data on
/// every run, so the development database is never touched. Requires PostgreSQL on localhost, like the API.
/// </summary>
public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string Base = "mahosoft_pruebas";
    public const string Conexion = $"Host=localhost;Database={Base};Username=postgres;Password=postgres";
    private const string Servidor = "Host=localhost;Database=postgres;Username=postgres;Password=postgres";
    public const string Password = "Maho2026!";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, HttpClient> _sesiones = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development: the API migrates and loads the sample data on start
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:MahoSoft", Conexion);
        builder.UseSetting("Archivos:Carpeta", Path.Combine(Path.GetTempPath(), "mahosoft-pruebas-archivos"));
        // No email server nor Cloudinary in the tests: the API must work without them
        builder.UseSetting("Correo:Usuario", "");
        builder.UseSetting("Correo:Remitente", "");
        builder.UseSetting("Cloudinary:CloudName", "");
        builder.UseSetting("Cloudinary:ApiKey", "");
        builder.UseSetting("Cloudinary:ApiSecret", "");
    }

    public async Task InitializeAsync()
    {
        await using (var cn = new NpgsqlConnection(Servidor))
        {
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = $"DROP DATABASE IF EXISTS {Base} WITH (FORCE)";
            await cmd.ExecuteNonQueryAsync();
        }
        _ = Server; // starts the API, which creates the database
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    /// <summary>A client signed in as that sample user (one login per user: logins are limited to 5 a minute).</summary>
    public async Task<HttpClient> ComoAsync(string email)
    {
        if (_sesiones.TryGetValue(email, out var existente))
            return existente;
        var cliente = CreateClient();
        var r = await cliente.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        r.EnsureSuccessStatusCode();
        var token = (await r.Content.ReadFromJsonAsync<JsonObject>())!["token"]!.GetValue<string>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _sesiones[email] = cliente;
    }

    public Task<HttpClient> AdminAsync() => ComoAsync("ana@ellaboutique.co");

    /// <summary>Runs a query that returns one value, straight on the test database.</summary>
    public static async Task<T> SqlAsync<T>(string sql)
    {
        await using var cn = new NpgsqlConnection(Conexion);
        await cn.OpenAsync();
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        return (T)Convert.ChangeType((await cmd.ExecuteScalarAsync())!, typeof(T));
    }
}

[CollectionDefinition(Nombre)]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Nombre = "API";
}

/// <summary>Calls used by several tests.</summary>
public static class Llamadas
{
    public static async Task<JsonNode> LeerAsync(this HttpResponseMessage r) =>
        JsonNode.Parse(await r.Content.ReadAsStringAsync())!;

    /// <summary>ProblemDetails "detail" of an error answer.</summary>
    public static async Task<string> DetalleAsync(this HttpResponseMessage r) =>
        (await r.LeerAsync())["detail"]?.GetValue<string>() ?? "";

    public static async Task<int> StockAsync(this HttpClient c, int productoId, string talla)
    {
        var p = await (await c.GetAsync($"/api/productos/{productoId}")).LeerAsync();
        return p["stock"]![talla]?.GetValue<int>() ?? 0;
    }

    public static async Task<decimal> CostoAsync(this HttpClient c, int productoId) =>
        (await (await c.GetAsync($"/api/productos/{productoId}")).LeerAsync())["costo"]!.GetValue<decimal>();

    /// <summary>Posts "datos" (and an optional file) as multipart, like purchases and sales do.</summary>
    public static Task<HttpResponseMessage> MultipartAsync(
        this HttpClient c,
        string url,
        object datos,
        string? campoArchivo = null,
        byte[]? archivo = null,
        string nombre = "archivo.pdf"
    )
    {
        var form = new MultipartFormDataContent { { new StringContent(JsonSerializer.Serialize(datos, ApiFixture.Json)), "datos" } };
        if (campoArchivo is not null && archivo is not null)
            form.Add(new ByteArrayContent(archivo), campoArchivo, nombre);
        return c.PostAsync(url, form);
    }
}
