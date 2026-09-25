namespace MahoSoft.Api.Data.Entities;

/// <summary>A supplier invoice. Totals are stored so old purchases never change if the math does.</summary>
public class Compra
{
    public int Id { get; set; }

    /// <summary>Internal consecutive, e.g. OC-2026-046.</summary>
    public string Numero { get; set; } = "";

    public int ProveedorId { get; set; }
    public Proveedor Proveedor { get; set; } = null!;

    // Supplier's document
    public string TipoComprobante { get; set; } = "";
    public string NumeroComprobante { get; set; } = "";

    /// <summary>Issue date of the supplier's document; the time part is optional (00:00 when unknown).</summary>
    public DateTimeOffset FechaComprobante { get; set; }

    public string? VendedorProveedor { get; set; }

    /// <summary>CUFE / UUID of the electronic invoice.</summary>
    public string? Cufe { get; set; }

    public CondicionPago CondicionPago { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public EstadoPago EstadoPago { get; set; }

    // IVA and totals
    public decimal IvaPorcentaje { get; set; }

    /// <summary>Whether the item prices were typed with IVA already included.</summary>
    public bool PreciosIncluyenIva { get; set; }

    /// <summary>Amount off the subtotal, before IVA.</summary>
    public decimal Descuento { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Iva { get; set; }
    public decimal Total { get; set; }

    /// <summary>"Valor" printed on the supplier's document, to check it against <see cref="Total"/>.</summary>
    public decimal? ValorComprobante { get; set; }

    public string? Notas { get; set; }

    /// <summary>The supplier's invoice PDF or photo.</summary>
    public Guid? DocumentoId { get; set; }

    public Archivo? Documento { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public DateTimeOffset CreadoEn { get; set; }
    public List<CompraItem> Items { get; set; } = [];
}

public class CompraItem
{
    public int Id { get; set; }
    public int CompraId { get; set; }
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    public int TallaId { get; set; }
    public Talla Talla { get; set; } = null!;

    /// <summary>The supplier's code for the item (e.g. AM49306LT-1).</summary>
    public string? ReferenciaProveedor { get; set; }

    public int Cantidad { get; set; }

    /// <summary>Unit price as typed from the invoice (with or without IVA, see <see cref="Compra.PreciosIncluyenIva"/>).</summary>
    public decimal PrecioUnitario { get; set; }

    /// <summary>What one unit cost the store: without IVA and less its share of the discount.</summary>
    public decimal CostoUnitario { get; set; }
}
