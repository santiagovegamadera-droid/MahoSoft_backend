using MahoSoft.Entidades;
using Microsoft.EntityFrameworkCore;

namespace MahoSoft.Datos.Repositorios;

/// <summary>One purchase that included the product: who it was bought from, and when.</summary>
public record CompraDeProducto(int ProveedorId, string Proveedor, string Numero, DateTimeOffset Fecha);

/// <summary>A product with its colors, sizes (in display order), image and purchases, newest first.</summary>
public record ProductoConCompras(Producto Producto, List<CompraDeProducto> Compras);

public interface IProductoRepositorio
{
    /// <summary>Every product, by name.</summary>
    Task<List<ProductoConCompras>> ListarAsync(CancellationToken ct = default);

    Task<ProductoConCompras?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>The product with colors, sizes and image, tracked for changes.</summary>
    Task<Producto?> ObtenerParaEditarAsync(int id, CancellationToken ct = default);

    /// <summary>Whether a purchase or a sale includes the product (then it can only be deactivated).</summary>
    Task<bool> TieneComprasOVentasAsync(int id, CancellationToken ct = default);

    /// <summary>Removes the product's stock adjustments; used when deleting a product without purchases or sales.</summary>
    Task EliminarMovimientosAsync(int id, CancellationToken ct = default);

    Task<Archivo?> ObtenerArchivoAsync(Guid id, CancellationToken ct = default);

    /// <summary>Whether another product (other than <paramref name="excluirProductoId"/>) shows this image.</summary>
    Task<bool> ImagenEnUsoAsync(Guid archivoId, int excluirProductoId, CancellationToken ct = default);

    /// <summary>
    /// Adds <paramref name="cantidad"/> units (negative to take them out) in the database itself, never leaving the
    /// stock below 0. False when the product doesn't come in that size, or has fewer units than it takes out.
    /// Runs right away, so call it inside the transaction of the operation.
    /// </summary>
    Task<bool> CambiarStockAsync(int productoId, int tallaId, int cantidad, CancellationToken ct = default);

    /// <summary>Units of one size right now (0 if the product doesn't come in it).</summary>
    Task<int> StockAsync(int productoId, int tallaId, CancellationToken ct = default);

    void Agregar(Producto producto);

    void Eliminar(Producto producto);

    void AgregarMovimiento(MovimientoInventario movimiento);

    void AgregarArchivo(Archivo archivo);

    void EliminarArchivo(Archivo archivo);
}

public class ProductoRepositorio(AppDbContext db) : IProductoRepositorio
{
    public Task<List<ProductoConCompras>> ListarAsync(CancellationToken ct = default) =>
        ConComprasAsync(db.Productos.OrderBy(p => p.Nombre), ct);

    public async Task<ProductoConCompras?> ObtenerAsync(int id, CancellationToken ct = default) =>
        (await ConComprasAsync(db.Productos.Where(p => p.Id == id), ct)).SingleOrDefault();

    public Task<Producto?> ObtenerParaEditarAsync(int id, CancellationToken ct = default) =>
        db.Productos.Include(p => p.Colores)
            .Include(p => p.Tallas)
            .ThenInclude(t => t.Talla)
            .Include(p => p.Imagen)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Id == id, ct);

    public async Task<bool> TieneComprasOVentasAsync(int id, CancellationToken ct = default) =>
        await db.CompraItems.AnyAsync(i => i.ProductoId == id, ct)
        || await db.VentaItems.AnyAsync(i => i.ProductoId == id, ct);

    public async Task EliminarMovimientosAsync(int id, CancellationToken ct = default) =>
        db.MovimientosInventario.RemoveRange(
            await db.MovimientosInventario.Where(m => m.ProductoId == id).ToListAsync(ct)
        );

    public Task<Archivo?> ObtenerArchivoAsync(Guid id, CancellationToken ct = default) =>
        db.Archivos.SingleOrDefaultAsync(a => a.Id == id, ct);

    public Task<bool> ImagenEnUsoAsync(Guid archivoId, int excluirProductoId, CancellationToken ct = default) =>
        db.Productos.AnyAsync(p => p.ImagenId == archivoId && p.Id != excluirProductoId, ct);

    public async Task<bool> CambiarStockAsync(int productoId, int tallaId, int cantidad, CancellationToken ct = default) =>
        await db.ProductoTallas.Where(t => t.ProductoId == productoId && t.TallaId == tallaId && t.Stock + cantidad >= 0)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Stock, t => t.Stock + cantidad), ct) > 0;

    public async Task<int> StockAsync(int productoId, int tallaId, CancellationToken ct = default) =>
        await db.ProductoTallas.Where(t => t.ProductoId == productoId && t.TallaId == tallaId)
            .Select(t => (int?)t.Stock)
            .SingleOrDefaultAsync(ct) ?? 0;

    public void Agregar(Producto producto) => db.Productos.Add(producto);

    public void Eliminar(Producto producto) => db.Productos.Remove(producto);

    public void AgregarMovimiento(MovimientoInventario movimiento) => db.MovimientosInventario.Add(movimiento);

    public void AgregarArchivo(Archivo archivo) => db.Archivos.Add(archivo);

    public void EliminarArchivo(Archivo archivo) => db.Archivos.Remove(archivo);

    private async Task<List<ProductoConCompras>> ConComprasAsync(IQueryable<Producto> query, CancellationToken ct)
    {
        var productos = await query
            .AsNoTracking()
            .Include(p => p.Colores.OrderBy(c => c.Id))
            .Include(p => p.Tallas)
            .ThenInclude(t => t.Talla)
            .ThenInclude(t => t.GrupoTalla)
            .Include(p => p.Imagen)
            .AsSplitQuery()
            .ToListAsync(ct);
        // Sizes in the order set in Configuración
        foreach (var p in productos)
            p.Tallas = p.Tallas.OrderBy(t => t.Talla.GrupoTalla.Orden).ThenBy(t => t.Talla.Orden).ToList();

        var ids = productos.Select(p => p.Id).ToList();
        var compras = await db.Compras.AsNoTracking()
            .SelectMany(c => c.Items, (c, i) => new { i.ProductoId, c.Id, c.ProveedorId, Proveedor = c.Proveedor.Nombre, c.Numero, c.FechaComprobante })
            .Where(x => ids.Contains(x.ProductoId))
            .Distinct()
            .ToListAsync(ct);
        var porProducto = compras
            .OrderByDescending(c => c.FechaComprobante)
            .ThenByDescending(c => c.Id)
            .ToLookup(c => c.ProductoId, c => new CompraDeProducto(c.ProveedorId, c.Proveedor, c.Numero, c.FechaComprobante));

        return productos.Select(p => new ProductoConCompras(p, porProducto[p.Id].ToList())).ToList();
    }
}
