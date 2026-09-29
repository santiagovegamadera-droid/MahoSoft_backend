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

/// <summary>Documents stored on the server disk. Paths are relative to the configured folder.</summary>
public interface IAlmacenDocumentos
{
    /// <summary>Saves the content under a new name (the user's file name is never used as a path).</summary>
    Task<string> GuardarAsync(Stream contenido, string subcarpeta, string extension, CancellationToken ct = default);

    /// <summary>The stored file, or null if it is no longer on disk.</summary>
    Stream? Abrir(string ruta);

    /// <summary>Deletes a file; a failure is logged, not thrown.</summary>
    void Eliminar(string ruta);
}

public class DiscoAlmacen(IOptions<ArchivosOptions> options, IHostEnvironment env, ILogger<DiscoAlmacen> logger)
    : IAlmacenDocumentos
{
    private readonly string _raiz = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.Carpeta));

    public async Task<string> GuardarAsync(
        Stream contenido,
        string subcarpeta,
        string extension,
        CancellationToken ct = default
    )
    {
        var hoy = DateTime.UtcNow;
        var ruta = Path.Combine(subcarpeta, hoy.ToString("yyyy"), hoy.ToString("MM"), $"{Guid.NewGuid():N}{extension}");
        var completa = Completa(ruta);
        Directory.CreateDirectory(Path.GetDirectoryName(completa)!);
        await using (var destino = new FileStream(completa, FileMode.CreateNew))
            await contenido.CopyToAsync(destino, ct);
        return ruta.Replace('\\', '/');
    }

    public Stream? Abrir(string ruta)
    {
        var completa = Completa(ruta);
        return File.Exists(completa) ? File.OpenRead(completa) : null;
    }

    public void Eliminar(string ruta)
    {
        try
        {
            File.Delete(Completa(ruta));
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "No se pudo borrar el archivo {Ruta}", ruta);
        }
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
