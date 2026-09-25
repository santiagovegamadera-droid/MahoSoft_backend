namespace MahoSoft.Api.Data.Entities;

/// <summary>A returning customer, so their details don't have to be typed again. Walk-in sales have no customer.</summary>
public class Cliente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int? TipoDocumentoId { get; set; }
    public TipoDocumento? TipoDocumento { get; set; }
    public string? Documento { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}

/// <summary>A POS sale. Voiding marks it as Anulada instead of deleting it.</summary>
public class Venta
{
    public int Id { get; set; }

    /// <summary>Invoice number, e.g. VTA-2026-0847.</summary>
    public string NumeroFactura { get; set; } = "";

    public DateTimeOffset Fecha { get; set; }
    public TipoVenta Tipo { get; set; }

    /// <summary>Null for "Cliente general".</summary>
    public int? ClienteId { get; set; }

    public Cliente? Cliente { get; set; }
    public int VendedorId { get; set; }
    public Usuario Vendedor { get; set; } = null!;
    public MetodoPago MetodoPago { get; set; }
    public decimal DescuentoPorcentaje { get; set; }
    public decimal Envio { get; set; }
    public decimal Subtotal { get; set; }

    /// <summary>Discount amount (Subtotal × DescuentoPorcentaje).</summary>
    public decimal Descuento { get; set; }

    public decimal Total { get; set; }
    public EstadoVenta Estado { get; set; }
    public DateTimeOffset? AnuladaEn { get; set; }
    public int? AnuladaPorId { get; set; }
    public Usuario? AnuladaPor { get; set; }
    public string? MotivoAnulacion { get; set; }
    public List<VentaItem> Items { get; set; } = [];
    public VentaEntrega? Entrega { get; set; }
    public ComprobanteTransferencia? Comprobante { get; set; }
}

public class VentaItem
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    public int TallaId { get; set; }
    public Talla Talla { get; set; } = null!;

    /// <summary>Product name at the time of the sale, so receipts don't change if the product is renamed.</summary>
    public string NombreProducto { get; set; } = "";

    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }

    /// <summary>Product cost when sold, so the sale's real margin stays fixed.</summary>
    public decimal CostoUnitario { get; set; }
}

/// <summary>Delivery details of a Pedido.</summary>
public class VentaEntrega
{
    public int VentaId { get; set; }
    public string Direccion { get; set; } = "";
    public string? Barrio { get; set; }
    public string? Ciudad { get; set; }
    public DateOnly? FechaEntrega { get; set; }
    public string? Notas { get; set; }
}

/// <summary>Proof of a transfer payment.</summary>
public class ComprobanteTransferencia
{
    public int VentaId { get; set; }
    public int? BancoId { get; set; }
    public Banco? Banco { get; set; }
    public string? Referencia { get; set; }
    public Guid? ArchivoId { get; set; }
    public Archivo? Archivo { get; set; }
}
