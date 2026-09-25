namespace MahoSoft.Api.Data.Entities;

public class Categoria
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>
/// A garment. Its suppliers aren't stored here: they come from the purchases that include it.
/// </summary>
public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;
    public decimal PrecioVenta { get; set; }

    /// <summary>Unit cost without IVA from the latest purchase; 0 until the product is first bought.</summary>
    public decimal CostoActual { get; set; }

    public string? Descripcion { get; set; }
    public Guid? ImagenId { get; set; }
    public Archivo? Imagen { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreadoEn { get; set; }
    public List<ProductoColor> Colores { get; set; } = [];
    public List<ProductoTalla> Tallas { get; set; } = [];
}

public class ProductoColor
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public string Color { get; set; } = "";
}

/// <summary>The sizes a product comes in and the units currently in stock for each (single store).</summary>
public class ProductoTalla
{
    public int ProductoId { get; set; }
    public int TallaId { get; set; }
    public Talla Talla { get; set; } = null!;
    public int Stock { get; set; }
}
