using System.ComponentModel.DataAnnotations;
using System.Net;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MahoSoft.Datos.Archivos;

/// <summary>Settings of the "Cloudinary" section. The secret lives in user-secrets (development) or an environment variable.</summary>
public class CloudinaryOptions
{
    public const string Seccion = "Cloudinary";

    [Required]
    public string CloudName { get; set; } = "";

    [Required]
    public string ApiKey { get; set; } = "";

    [Required]
    public string ApiSecret { get; set; } = "";
}

/// <summary>Where an uploaded image ended up.</summary>
public record ImagenSubida(string Url, string PublicId, long Tamano);

/// <summary>Product photos, stored outside the database.</summary>
public interface IAlmacenImagenes
{
    /// <summary>Whether the Cloudinary keys are set; without them photos can't be uploaded, but everything else works.</summary>
    bool Configurado { get; }

    Task<ImagenSubida> SubirAsync(Stream contenido, string nombre, CancellationToken ct = default);

    /// <summary>Removes an image; a failure is logged, not thrown (the database is already consistent).</summary>
    Task EliminarAsync(string publicId);
}

public class CloudinaryAlmacen(IOptions<CloudinaryOptions> options, ILogger<CloudinaryAlmacen> logger) : IAlmacenImagenes
{
    private const string Carpeta = "mahosoft/productos";

    // Created on first use, so a missing key only affects uploading photos, never listing products
    private readonly Lazy<Cloudinary> _cloudinary = new(() =>
        new Cloudinary(new Account(options.Value.CloudName, options.Value.ApiKey, options.Value.ApiSecret))
        {
            Api = { Secure = true },
        }
    );

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(options.Value.CloudName)
        && !string.IsNullOrWhiteSpace(options.Value.ApiKey)
        && !string.IsNullOrWhiteSpace(options.Value.ApiSecret);

    public async Task<ImagenSubida> SubirAsync(Stream contenido, string nombre, CancellationToken ct = default)
    {
        var parametros = new ImageUploadParams
        {
            File = new FileDescription(nombre, contenido),
            Folder = Carpeta,
            // Store at most 1200 px per side: enough for the catalog, and uploads from a phone stay small
            Transformation = new Transformation().Width(1200).Height(1200).Crop("limit"),
        };
        var resultado = await _cloudinary.Value.UploadAsync(parametros, ct);
        if (resultado.Error is not null || resultado.StatusCode != HttpStatusCode.OK)
        {
            logger.LogError("Cloudinary rechazó la imagen {Nombre}: {Error}", nombre, resultado.Error?.Message);
            throw new InvalidOperationException($"No se pudo subir la imagen: {resultado.Error?.Message}");
        }
        return new ImagenSubida(resultado.SecureUrl.ToString(), resultado.PublicId, resultado.Bytes);
    }

    public async Task EliminarAsync(string publicId)
    {
        if (!Configurado)
        {
            logger.LogWarning("Cloudinary no está configurado: no se borró {PublicId}", publicId);
            return;
        }
        try
        {
            var resultado = await _cloudinary.Value.DestroyAsync(new DeletionParams(publicId));
            if (resultado.Error is not null)
                logger.LogWarning("No se pudo borrar {PublicId} de Cloudinary: {Error}", publicId, resultado.Error.Message);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "No se pudo borrar {PublicId} de Cloudinary", publicId);
        }
    }
}
