using MahoSoft.Entidades;
using Microsoft.EntityFrameworkCore;

namespace MahoSoft.Datos.Repositorios;

/// <summary>A category with how many products it has, and how many of them are active.</summary>
public record CategoriaConConteo(Categoria Categoria, int Productos, int ProductosActivos);

public interface ICategoriaRepositorio
{
    /// <summary>Every category with its product counts, by name.</summary>
    Task<List<CategoriaConConteo>> ListarConConteoAsync(CancellationToken ct = default);

    Task<CategoriaConConteo?> ObtenerConConteoAsync(int id, CancellationToken ct = default);

    Task<Categoria?> ObtenerPorIdAsync(int id, CancellationToken ct = default);

    /// <summary>Whether another category (other than <paramref name="excluirId"/>) already uses that name.</summary>
    Task<bool> ExisteNombreAsync(string nombre, int? excluirId = null, CancellationToken ct = default);

    Task<bool> TieneProductosAsync(int id, CancellationToken ct = default);

    void Agregar(Categoria categoria);

    void Eliminar(Categoria categoria);
}

public class CategoriaRepositorio(AppDbContext db) : ICategoriaRepositorio
{
    // Filter and sort before this: EF can't translate conditions on the record it builds
    private IQueryable<CategoriaConConteo> ConConteo(IQueryable<Categoria> query) =>
        query
            .AsNoTracking()
            .Select(c => new CategoriaConConteo(
                c,
                db.Productos.Count(p => p.CategoriaId == c.Id),
                db.Productos.Count(p => p.CategoriaId == c.Id && p.Activo)
            ));

    public Task<List<CategoriaConConteo>> ListarConConteoAsync(CancellationToken ct = default) =>
        ConConteo(db.Categorias.OrderBy(c => c.Nombre)).ToListAsync(ct);

    public Task<CategoriaConConteo?> ObtenerConConteoAsync(int id, CancellationToken ct = default) =>
        ConConteo(db.Categorias.Where(c => c.Id == id)).SingleOrDefaultAsync(ct);

    public Task<Categoria?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        db.Categorias.SingleOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExisteNombreAsync(string nombre, int? excluirId = null, CancellationToken ct = default) =>
        db.Categorias.AnyAsync(c => c.Nombre == nombre && c.Id != excluirId, ct);

    public Task<bool> TieneProductosAsync(int id, CancellationToken ct = default) =>
        db.Productos.AnyAsync(p => p.CategoriaId == id, ct);

    public void Agregar(Categoria categoria) => db.Categorias.Add(categoria);

    public void Eliminar(Categoria categoria) => db.Categorias.Remove(categoria);
}
