namespace MahoSoft.Negocio;

/// <summary>An uploaded file as it arrives from the browser.</summary>
public record ArchivoEntrante(Stream Contenido, string Nombre, long Tamano);

/// <summary>An upload already read and checked: its bytes and the type found in its content.</summary>
public record DocumentoLeido(byte[] Bytes, string TipoMime, string Extension);

/// <summary>Checks for documents people attach (supplier invoices, transfer receipts).</summary>
public static class Documentos
{
    public const long TamanoMaximo = 10 * 1024 * 1024;

    /// <summary>
    /// Reads the upload and checks, by its content (not its name or declared type), that it is a PDF or a JPG, PNG or
    /// WebP image of at most 10 MB. <paramref name="que"/> names it in the messages ("El documento", "El comprobante").
    /// </summary>
    public static async Task<DocumentoLeido> LeerAsync(ArchivoEntrante archivo, string que, CancellationToken ct)
    {
        if (archivo.Tamano == 0)
            throw new ValidacionException($"{que} está vacío");
        if (archivo.Tamano > TamanoMaximo)
            throw new ValidacionException($"{que} no puede pesar más de 10 MB");

        using var memoria = new MemoryStream();
        await archivo.Contenido.CopyToAsync(memoria, ct);
        var bytes = memoria.ToArray();
        var tipo =
            DetectarTipo(bytes)
            ?? throw new ValidacionException($"{que} debe ser un PDF o una imagen (JPG, PNG o WebP)");
        return new DocumentoLeido(bytes, tipo.Mime, tipo.Extension);
    }

    private static (string Mime, string Extension)? DetectarTipo(byte[] b)
    {
        bool Empieza(ReadOnlySpan<byte> firma) => b.AsSpan().StartsWith(firma);

        if (Empieza("%PDF-"u8))
            return ("application/pdf", ".pdf");
        if (Empieza([0xFF, 0xD8, 0xFF]))
            return ("image/jpeg", ".jpg");
        if (Empieza([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
            return ("image/png", ".png");
        if (b.Length >= 12 && Empieza("RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return ("image/webp", ".webp");
        return null;
    }
}
