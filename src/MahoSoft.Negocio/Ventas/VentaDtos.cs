using MahoSoft.Entidades;

namespace MahoSoft.Negocio.Ventas;

/// <summary>
/// A POS sale. Prices and costs come from the products, not from the request; the discount must be one of the
/// POS discounts in Configuración. Cliente is optional for Tienda and required (name, phone) for Pedido, which
/// also needs Entrega. Comprobante applies to transfers; its file travels apart in the same request.
/// </summary>
public record VentaRequest(
    TipoVenta Tipo,
    MetodoPago MetodoPago,
    decimal DescuentoPorcentaje,
    ClienteRequest? Cliente,
    EntregaRequest? Entrega,
    ComprobanteRequest? Comprobante,
    VentaItemRequest[] Items
);

/// <summary>Customer details as typed; an existing customer is found by document, or else by phone.</summary>
public record ClienteRequest(string? Nombre, string? TipoDocumento, string? Documento, string? Telefono, string? Correo);

public record EntregaRequest(
    string Direccion,
    string? Barrio,
    string? Ciudad,
    DateOnly? FechaEntrega,
    decimal Envio,
    string? Notas
);

/// <summary>The bank or wallet by its name in Configuración, and the transfer reference.</summary>
public record ComprobanteRequest(string? Banco, string? Referencia);

public record VentaItemRequest(int ProductoId, string Talla, int Cantidad);

public record AnularVentaRequest(string Motivo);

public record ClienteDto(
    int Id,
    string Nombre,
    string TipoDocumento,
    string Documento,
    string Telefono,
    string Correo
)
{
    public static ClienteDto De(Cliente c) =>
        new(c.Id, c.Nombre, c.TipoDocumento?.Codigo ?? "", c.Documento ?? "", c.Telefono ?? "", c.Correo ?? "");
}

public record EntregaDto(string Direccion, string Barrio, string Ciudad, DateOnly? FechaEntrega, string Notas);

public record ComprobanteDto(string Banco, string Referencia, Compras.DocumentoDto? Archivo);

public record VentaItemDto(int Id, int ProductoId, string Producto, string Talla, int Cantidad, decimal PrecioUnitario);

/// <summary>A sale as registered; voided sales stay, with who voided them, when and why.</summary>
public record VentaDto(
    int Id,
    string NumeroFactura,
    DateTimeOffset Fecha,
    TipoVenta Tipo,
    ClienteDto? Cliente,
    int VendedorId,
    string Vendedor,
    MetodoPago MetodoPago,
    decimal DescuentoPorcentaje,
    decimal Subtotal,
    decimal Descuento,
    decimal Envio,
    decimal Total,
    EstadoVenta Estado,
    DateTimeOffset? AnuladaEn,
    string? AnuladaPor,
    string? MotivoAnulacion,
    EntregaDto? Entrega,
    ComprobanteDto? Comprobante,
    int Unidades,
    VentaItemDto[] Items
)
{
    public static VentaDto De(Venta v) =>
        new(
            v.Id,
            v.NumeroFactura,
            v.Fecha,
            v.Tipo,
            v.Cliente is null ? null : ClienteDto.De(v.Cliente),
            v.VendedorId,
            v.Vendedor.Nombre,
            v.MetodoPago,
            v.DescuentoPorcentaje,
            v.Subtotal,
            v.Descuento,
            v.Envio,
            v.Total,
            v.Estado,
            v.AnuladaEn,
            v.AnuladaPor?.Nombre,
            v.MotivoAnulacion,
            v.Entrega is null
                ? null
                : new EntregaDto(
                    v.Entrega.Direccion,
                    v.Entrega.Barrio ?? "",
                    v.Entrega.Ciudad ?? "",
                    v.Entrega.FechaEntrega,
                    v.Entrega.Notas ?? ""
                ),
            v.Comprobante is null
                ? null
                : new ComprobanteDto(
                    v.Comprobante.Banco?.Nombre ?? "",
                    v.Comprobante.Referencia ?? "",
                    v.Comprobante.Archivo is null
                        ? null
                        : new Compras.DocumentoDto(
                            v.Comprobante.Archivo.Nombre,
                            v.Comprobante.Archivo.TipoMime,
                            v.Comprobante.Archivo.Tamano
                        )
                ),
            v.Items.Sum(i => i.Cantidad),
            v.Items.OrderBy(i => i.Id)
                .Select(i => new VentaItemDto(i.Id, i.ProductoId, i.NombreProducto, i.Talla.Valor, i.Cantidad, i.PrecioUnitario))
                .ToArray()
        );
}
