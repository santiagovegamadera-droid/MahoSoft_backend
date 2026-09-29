using MahoSoft.Entidades;
using Microsoft.EntityFrameworkCore;

namespace MahoSoft.Datos.Repositorios;

/// <summary>A stock movement with the names it is shown with, and the purchase or sale it came from.</summary>
public record MovimientoDetalle(
    int Id,
    DateTimeOffset Fecha,
    TipoMovimiento Tipo,
    int ProductoId,
    string Producto,
    string Talla,
    int Cantidad,
    string? Motivo,
    string Usuario,
    string? Compra,
    string? Venta
);

public interface IInventarioRepositorio
{
    /// <summary>
    /// The latest movements, newest first, optionally of one product or one type; at most <paramref name="limite"/>.
    /// </summary>
    Task<List<MovimientoDetalle>> ListarAsync(
        int? productoId,
        TipoMovimiento? tipo,
        int limite,
        CancellationToken ct = default
    );

    Task<MovimientoDetalle?> ObtenerAsync(int id, CancellationToken ct = default);
}

public class InventarioRepositorio(AppDbContext db) : IInventarioRepositorio
{
    private IQueryable<MovimientoDetalle> Detalle(IQueryable<MovimientoInventario> query) =>
        query
            .AsNoTracking()
            .Select(m => new MovimientoDetalle(
                m.Id,
                m.Fecha,
                m.Tipo,
                m.ProductoId,
                m.Producto.Nombre,
                m.Talla.Valor,
                m.Cantidad,
                m.Motivo,
                m.Usuario.Nombre,
                m.Compra != null ? m.Compra.Numero : null,
                m.Venta != null ? m.Venta.NumeroFactura : null
            ));

    public Task<List<MovimientoDetalle>> ListarAsync(
        int? productoId,
        TipoMovimiento? tipo,
        int limite,
        CancellationToken ct = default
    )
    {
        var query = db.MovimientosInventario.AsQueryable();
        if (productoId is not null)
            query = query.Where(m => m.ProductoId == productoId);
        if (tipo is not null)
            query = query.Where(m => m.Tipo == tipo);
        return Detalle(query.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id).Take(limite)).ToListAsync(ct);
    }

    public Task<MovimientoDetalle?> ObtenerAsync(int id, CancellationToken ct = default) =>
        Detalle(db.MovimientosInventario.Where(m => m.Id == id)).SingleOrDefaultAsync(ct);
}
