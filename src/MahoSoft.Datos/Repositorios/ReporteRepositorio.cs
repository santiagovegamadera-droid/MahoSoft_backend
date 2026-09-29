using MahoSoft.Entidades;
using Microsoft.EntityFrameworkCore;

namespace MahoSoft.Datos.Repositorios;

/// <summary>A line of a sale, with what it takes to add up revenue, cost and categories.</summary>
public record LineaVendida(
    int ProductoId,
    string Producto,
    string Categoria,
    int Cantidad,
    decimal PrecioUnitario,
    decimal CostoUnitario
);

/// <summary>A registered (not voided) sale reduced to what reports use.</summary>
public record VentaParaReporte(
    int Id,
    DateTimeOffset Fecha,
    MetodoPago MetodoPago,
    decimal Subtotal,
    decimal Descuento,
    decimal Envio,
    decimal Total,
    List<LineaVendida> Lineas
);

/// <summary>A size of an active product at or below the low-stock threshold.</summary>
public record TallaStockBajo(int ProductoId, string Producto, string Talla, int Stock);

public interface IReporteRepositorio
{
    /// <summary>Registered sales with <paramref name="desde"/> &lt;= Fecha &lt; <paramref name="hasta"/>; voided ones don't count.</summary>
    Task<List<VentaParaReporte>> VentasAsync(DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct = default);

    /// <summary>Sizes of active products with at most <paramref name="umbral"/> units, fewest first.</summary>
    Task<List<TallaStockBajo>> StockBajoAsync(int umbral, CancellationToken ct = default);

    /// <summary>Total units in stock of each product.</summary>
    Task<Dictionary<int, int>> StockPorProductoAsync(IEnumerable<int> productoIds, CancellationToken ct = default);
}

public class ReporteRepositorio(AppDbContext db) : IReporteRepositorio
{
    public Task<List<VentaParaReporte>> VentasAsync(
        DateTimeOffset desde,
        DateTimeOffset hasta,
        CancellationToken ct = default
    ) =>
        db.Ventas.AsNoTracking()
            .Where(v => v.Estado == EstadoVenta.Registrada && v.Fecha >= desde && v.Fecha < hasta)
            .OrderBy(v => v.Fecha)
            .Select(v => new VentaParaReporte(
                v.Id,
                v.Fecha,
                v.MetodoPago,
                v.Subtotal,
                v.Descuento,
                v.Envio,
                v.Total,
                v.Items.Select(i => new LineaVendida(
                        i.ProductoId,
                        i.NombreProducto,
                        i.Producto.Categoria.Nombre,
                        i.Cantidad,
                        i.PrecioUnitario,
                        i.CostoUnitario
                    ))
                    .ToList()
            ))
            .ToListAsync(ct);

    public Task<List<TallaStockBajo>> StockBajoAsync(int umbral, CancellationToken ct = default) =>
        db.ProductoTallas.AsNoTracking()
            .Where(t => t.Stock <= umbral && db.Productos.Any(p => p.Id == t.ProductoId && p.Activo))
            .OrderBy(t => t.Stock)
            .ThenBy(t => t.Talla.GrupoTalla.Orden)
            .ThenBy(t => t.Talla.Orden)
            .Select(t => new TallaStockBajo(
                t.ProductoId,
                db.Productos.Where(p => p.Id == t.ProductoId).Select(p => p.Nombre).First(),
                t.Talla.Valor,
                t.Stock
            ))
            .ToListAsync(ct);

    public async Task<Dictionary<int, int>> StockPorProductoAsync(
        IEnumerable<int> productoIds,
        CancellationToken ct = default
    )
    {
        var ids = productoIds.ToList();
        return await db.ProductoTallas.AsNoTracking()
            .Where(t => ids.Contains(t.ProductoId))
            .GroupBy(t => t.ProductoId)
            .Select(g => new { g.Key, Total = g.Sum(t => t.Stock) })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
    }
}
