using MahoSoft.Entidades;
using Microsoft.EntityFrameworkCore;

namespace MahoSoft.Datos.Repositorios;

/// <summary>
/// A supplier with what its purchases say about it: how many there are, how many distinct products were
/// bought from it and the categories of those products (what it supplies).
/// </summary>
public record ProveedorConResumen(Proveedor Proveedor, int Compras, int Productos, List<Categoria> Categorias);

public interface IProveedorRepositorio
{
    /// <summary>Every supplier (with its document type) and its purchase summary, by name.</summary>
    Task<List<ProveedorConResumen>> ListarConResumenAsync(CancellationToken ct = default);

    Task<ProveedorConResumen?> ObtenerConResumenAsync(int id, CancellationToken ct = default);

    Task<Proveedor?> ObtenerPorIdAsync(int id, CancellationToken ct = default);

    /// <summary>Whether another supplier (other than <paramref name="excluirId"/>) has that document.</summary>
    Task<bool> ExisteDocumentoAsync(
        int tipoDocumentoId,
        string documento,
        int? excluirId = null,
        CancellationToken ct = default
    );

    Task<int> ContarComprasAsync(int id, CancellationToken ct = default);

    void Agregar(Proveedor proveedor);

    void Eliminar(Proveedor proveedor);
}

public class ProveedorRepositorio(AppDbContext db) : IProveedorRepositorio
{
    public Task<List<ProveedorConResumen>> ListarConResumenAsync(CancellationToken ct = default) =>
        ConResumenAsync(db.Proveedores.OrderBy(p => p.Nombre), ct);

    public async Task<ProveedorConResumen?> ObtenerConResumenAsync(int id, CancellationToken ct = default) =>
        (await ConResumenAsync(db.Proveedores.Where(p => p.Id == id), ct)).SingleOrDefault();

    public Task<Proveedor?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        db.Proveedores.Include(p => p.TipoDocumento).SingleOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> ExisteDocumentoAsync(
        int tipoDocumentoId,
        string documento,
        int? excluirId = null,
        CancellationToken ct = default
    ) =>
        db.Proveedores.AnyAsync(
            p => p.TipoDocumentoId == tipoDocumentoId && p.Documento == documento && p.Id != excluirId,
            ct
        );

    public Task<int> ContarComprasAsync(int id, CancellationToken ct = default) =>
        db.Compras.CountAsync(c => c.ProveedorId == id, ct);

    public void Agregar(Proveedor proveedor) => db.Proveedores.Add(proveedor);

    public void Eliminar(Proveedor proveedor) => db.Proveedores.Remove(proveedor);

    private async Task<List<ProveedorConResumen>> ConResumenAsync(IQueryable<Proveedor> query, CancellationToken ct)
    {
        var filas = await query
            .AsNoTracking()
            .Include(p => p.TipoDocumento)
            .Select(p => new
            {
                Proveedor = p,
                Compras = db.Compras.Count(c => c.ProveedorId == p.Id),
                Productos = db.Compras.Where(c => c.ProveedorId == p.Id)
                    .SelectMany(c => c.Items)
                    .Select(i => i.ProductoId)
                    .Distinct()
                    .Count(),
            })
            .ToListAsync(ct);

        // Categories each supplier supplies, from the products on its purchases
        var ids = filas.Select(f => f.Proveedor.Id).ToList();
        var surtidas = await db.Compras.AsNoTracking()
            .Where(c => ids.Contains(c.ProveedorId))
            .SelectMany(c => c.Items, (c, i) => new { c.ProveedorId, i.Producto.Categoria })
            .Distinct()
            .ToListAsync(ct);
        var categorias = surtidas.ToLookup(s => s.ProveedorId, s => s.Categoria);

        return filas
            .Select(f => new ProveedorConResumen(
                f.Proveedor,
                f.Compras,
                f.Productos,
                categorias[f.Proveedor.Id].OrderBy(c => c.Nombre).ToList()
            ))
            .ToList();
    }
}
