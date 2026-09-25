namespace MahoSoft.Api.Data.Entities;

/// <summary>
/// Every change to stock, with its reason. The sum of a product size's movements equals its
/// <see cref="ProductoTalla.Stock"/>, so the stock can always be explained.
/// </summary>
public class MovimientoInventario
{
    public int Id { get; set; }
    public DateTimeOffset Fecha { get; set; }
    public TipoMovimiento Tipo { get; set; }
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    public int TallaId { get; set; }
    public Talla Talla { get; set; } = null!;

    /// <summary>Signed units: positive adds stock, negative removes it.</summary>
    public int Cantidad { get; set; }

    public string? Motivo { get; set; }
    public int? CompraId { get; set; }
    public Compra? Compra { get; set; }
    public int? VentaId { get; set; }
    public Venta? Venta { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
}

/// <summary>An uploaded file: a product photo in Cloudinary or an invoice PDF on the server disk.</summary>
public class Archivo
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = "";
    public string TipoMime { get; set; } = "";
    public long Tamano { get; set; }
    public AlmacenArchivo Almacen { get; set; }

    /// <summary>Public URL (Cloudinary / external) or path relative to the files folder (Local).</summary>
    public string Ubicacion { get; set; } = "";

    /// <summary>Cloudinary public_id, needed to delete or transform the image.</summary>
    public string? PublicId { get; set; }

    public DateTimeOffset SubidoEn { get; set; }
}
