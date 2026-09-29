using MahoSoft.Entidades;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MahoSoft.Datos.Archivos;

/// <summary>Settings of the "Archivos" section: where documents (supplier invoices) are kept on the server.</summary>
public class ArchivosOptions
{
    public const string Seccion = "Archivos";

    /// <summary>Folder for the files; a relative path is taken from the API's folder.</summary>
    public string Carpeta { get; set; } = "App_Data/archivos";
}

/// <summary>Documents (supplier invoices, transfer receipts). Paths are relative to the storage root.</summary>
public interface IAlmacenDocumentos
{
    /// <summary>Where the files saved from now on go, recorded on each <see cref="Archivo"/>.</summary>
    AlmacenArchivo Almacen { get; }

    /// <summary>Saves the content under a new name (the user's file name is never used as a path).</summary>
    Task<string> GuardarAsync(Stream contenido, string subcarpeta, string extension, CancellationToken ct = default);

    /// <summary>The stored file, or null if it is no longer there.</summary>
    Task<Stream?> AbrirAsync(string ruta, CancellationToken ct = default);

    /// <summary>Deletes a file; a failure is logged, not thrown.</summary>
    Task EliminarAsync(string ruta);

    /// <summary>New stored name: subfolder/yyyy/MM/random + extension.</summary>
    static string NuevaRuta(string subcarpeta, string extension)
    {
        var hoy = DateTime.UtcNow;
        return $"{subcarpeta}/{hoy:yyyy}/{hoy:MM}/{Guid.NewGuid():N}{extension}";
    }
}

/// <summary>Documents on the server disk, under the configured folder.</summary>
public class DiscoAlmacen(IOptions<ArchivosOptions> options, IHostEnvironment env, ILogger<DiscoAlmacen> logger)
    : IAlmacenDocumentos
{
    private readonly string _raiz = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.Carpeta));

    public AlmacenArchivo Almacen => AlmacenArchivo.Local;

    public async Task<string> GuardarAsync(
        Stream contenido,
        string subcarpeta,
        string extension,
        CancellationToken ct = default
    )
    {
        var ruta = IAlmacenDocumentos.NuevaRuta(subcarpeta, extension);
        var completa = Completa(ruta);
        Directory.CreateDirectory(Path.GetDirectoryName(completa)!);
        await using (var destino = new FileStream(completa, FileMode.CreateNew))
            await contenido.CopyToAsync(destino, ct);
        return ruta;
    }

    public Task<Stream?> AbrirAsync(string ruta, CancellationToken ct = default)
    {
        var completa = Completa(ruta);
        return Task.FromResult<Stream?>(File.Exists(completa) ? File.OpenRead(completa) : null);
    }

    public Task EliminarAsync(string ruta)
    {
        try
        {
            File.Delete(Completa(ruta));
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "No se pudo borrar el archivo {Ruta}", ruta);
        }
        return Task.CompletedTask;
    }

    // Stored paths come from GuardarAsync, but never let one point outside the folder
    private string Completa(string ruta)
    {
        var completa = Path.GetFullPath(Path.Combine(_raiz, ruta));
        if (!completa.StartsWith(_raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Ruta de archivo fuera de la carpeta: {ruta}");
        return completa;
    }
}
