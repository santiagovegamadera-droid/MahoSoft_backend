using MahoSoft.Entidades;

namespace MahoSoft.Negocio.Compras;

/// <summary>
/// A supplier invoice as typed from the paper/PDF. Prices are per unit as printed: without IVA, or with IVA
/// when <see cref="PreciosIncluyenIva"/>. Descuento is an amount off the subtotal, before IVA. Hora is "HH:mm"
/// or empty. The server computes every total; the number (OC-…) is assigned on save.
/// </summary>
public record CompraRequest(
    int ProveedorId,
    string TipoComprobante,
    string NumeroComprobante,
    DateOnly Fecha,
    string? Hora,
    string? VendedorProveedor,
    string? Cufe,
    CondicionPago CondicionPago,
    DateOnly? FechaVencimiento,
    EstadoPago EstadoPago,
    decimal IvaPorcentaje,
    bool PreciosIncluyenIva,
    decimal Descuento,
    decimal? ValorComprobante,
    string? Notas,
    CompraItemRequest[] Items
);

public record CompraItemRequest(int ProductoId, string Talla, string? ReferenciaProveedor, int Cantidad, decimal PrecioUnitario);

public record CompraItemDto(
    int Id,
    int ProductoId,
    string Producto,
    string Talla,
    string ReferenciaProveedor,
    int Cantidad,
    decimal PrecioUnitario,
    decimal CostoUnitario
);

public record DocumentoDto(string Nombre, string TipoMime, long Tamano);

/// <summary>A registered purchase with the totals computed when it was saved.</summary>
public record CompraDto(
    int Id,
    string Numero,
    int ProveedorId,
    string Proveedor,
    string TipoComprobante,
    string NumeroComprobante,
    DateOnly Fecha,
    string? Hora,
    string VendedorProveedor,
    string Cufe,
    CondicionPago CondicionPago,
    DateOnly? FechaVencimiento,
    EstadoPago EstadoPago,
    decimal IvaPorcentaje,
    bool PreciosIncluyenIva,
    decimal Subtotal,
    decimal Descuento,
    decimal Iva,
    decimal Total,
    decimal? ValorComprobante,
    string Notas,
    string Usuario,
    DateTimeOffset CreadoEn,
    DocumentoDto? Documento,
    int Unidades,
    CompraItemDto[] Items
)
{
    public static CompraDto De(Compra c)
    {
        var fecha = c.FechaComprobante.ToOffset(CompraServicio.Colombia);
        return new(
            c.Id,
            c.Numero,
            c.ProveedorId,
            c.Proveedor.Nombre,
            c.TipoComprobante,
            c.NumeroComprobante,
            DateOnly.FromDateTime(fecha.DateTime),
            // 00:00 means the invoice didn't say
            fecha.TimeOfDay == TimeSpan.Zero ? null : fecha.ToString("HH:mm"),
            c.VendedorProveedor ?? "",
            c.Cufe ?? "",
            c.CondicionPago,
            c.FechaVencimiento,
            c.EstadoPago,
            c.IvaPorcentaje,
            c.PreciosIncluyenIva,
            c.Subtotal,
            c.Descuento,
            c.Iva,
            c.Total,
            c.ValorComprobante,
            c.Notas ?? "",
            c.Usuario.Nombre,
            c.CreadoEn,
            c.Documento is null ? null : new DocumentoDto(c.Documento.Nombre, c.Documento.TipoMime, c.Documento.Tamano),
            c.Items.Sum(i => i.Cantidad),
            c.Items.OrderBy(i => i.Id)
                .Select(i => new CompraItemDto(
                    i.Id,
                    i.ProductoId,
                    i.Producto.Nombre,
                    i.Talla.Valor,
                    i.ReferenciaProveedor ?? "",
                    i.Cantidad,
                    i.PrecioUnitario,
                    i.CostoUnitario
                ))
                .ToArray()
        );
    }
}

/// <summary>The stored invoice document, to send back to the browser.</summary>
public record DocumentoArchivo(Stream Contenido, string Nombre, string TipoMime);
