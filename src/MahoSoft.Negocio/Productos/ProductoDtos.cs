using System.ComponentModel.DataAnnotations;
using MahoSoft.Datos.Repositorios;

namespace MahoSoft.Negocio.Productos;

/// <summary>
/// Stock maps each size the product comes in to its units (sizes it doesn't list, it doesn't come in).
/// Changing a size's units is recorded as a stock adjustment by whoever saves.
/// </summary>
public record ProductoRequest(
    [Required(ErrorMessage = "El nombre es obligatorio"), MaxLength(150, ErrorMessage = "El nombre admite hasta 150 caracteres")]
        string Nombre,
    int CategoriaId,
    [Range(0.01, 1_000_000_000, ErrorMessage = "Ingresa un precio mayor a 0")] decimal PrecioVenta,
    [MaxLength(1000, ErrorMessage = "La descripción admite hasta 1000 caracteres")] string? Descripcion,
    string[]? Colores,
    Dictionary<string, int>? Stock,
    Guid? ImagenId,
    bool Activo = true
);

public record ProveedorDeProductoDto(int Id, string Nombre);

public record UltimaCompraDto(string Numero, DateTimeOffset Fecha, int ProveedorId, string Proveedor);

/// <summary>
/// A product. Costo is the unit cost without IVA from its latest purchase; Proveedores (newest first) and
/// UltimaCompra come from the purchases that include it.
/// </summary>
public record ProductoDto(
    int Id,
    string Nombre,
    int CategoriaId,
    decimal PrecioVenta,
    decimal Costo,
    string Descripcion,
    string[] Colores,
    Dictionary<string, int> Stock,
    bool Activo,
    Guid? ImagenId,
    string? ImagenUrl,
    ProveedorDeProductoDto[] Proveedores,
    UltimaCompraDto? UltimaCompra
)
{
    public static ProductoDto De(ProductoConCompras x)
    {
        var p = x.Producto;
        var ultima = x.Compras.FirstOrDefault();
        return new(
            p.Id,
            p.Nombre,
            p.CategoriaId,
            p.PrecioVenta,
            p.CostoActual,
            p.Descripcion ?? "",
            p.Colores.Select(c => c.Color).ToArray(),
            // Keeps the order of p.Tallas (the one set in Configuración)
            p.Tallas.ToDictionary(t => t.Talla.Valor, t => t.Stock),
            p.Activo,
            p.ImagenId,
            p.Imagen?.Ubicacion,
            x.Compras.DistinctBy(c => c.ProveedorId).Select(c => new ProveedorDeProductoDto(c.ProveedorId, c.Proveedor)).ToArray(),
            ultima is null ? null : new UltimaCompraDto(ultima.Numero, ultima.Fecha, ultima.ProveedorId, ultima.Proveedor)
        );
    }
}

public record ImagenDto(Guid Id, string Url);
