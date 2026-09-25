using MahoSoft.Entidades;
using Microsoft.EntityFrameworkCore;

namespace MahoSoft.Datos.Repositorios;

/// <summary>
/// The store's settings. Lists come back whole (inactive rows included, in their order) so the service
/// can match them against the list the user saved.
/// </summary>
public interface IConfiguracionRepositorio
{
    /// <summary>The single row of store details (created with the database).</summary>
    Task<Negocio> ObtenerNegocioAsync(CancellationToken ct = default);

    Task<List<TipoDocumento>> ListarTiposDocumentoAsync(CancellationToken ct = default);

    /// <summary>The document type with that code (CC, NIT…), active or not, or null.</summary>
    Task<TipoDocumento?> ObtenerTipoDocumentoAsync(string codigo, CancellationToken ct = default);

    Task<List<Banco>> ListarBancosAsync(CancellationToken ct = default);

    Task<List<DescuentoPos>> ListarDescuentosAsync(CancellationToken ct = default);

    /// <summary>Size groups with their sizes, both in display order.</summary>
    Task<List<GrupoTalla>> ListarGruposTallaAsync(CancellationToken ct = default);

    /// <summary>Whether a supplier, user or customer has this document type.</summary>
    Task<bool> TipoDocumentoEnUsoAsync(int id, CancellationToken ct = default);

    /// <summary>Whether a transfer receipt names this bank.</summary>
    Task<bool> BancoEnUsoAsync(int id, CancellationToken ct = default);

    /// <summary>Whether a product, purchase, sale or stock movement refers to this size.</summary>
    Task<bool> TallaEnUsoAsync(int id, CancellationToken ct = default);

    void Agregar<T>(T entidad)
        where T : class;

    void Eliminar<T>(T entidad)
        where T : class;
}

public class ConfiguracionRepositorio(AppDbContext db) : IConfiguracionRepositorio
{
    public Task<Negocio> ObtenerNegocioAsync(CancellationToken ct = default) => db.Negocio.SingleAsync(ct);

    public Task<List<TipoDocumento>> ListarTiposDocumentoAsync(CancellationToken ct = default) =>
        db.TiposDocumento.OrderBy(t => t.Orden).ToListAsync(ct);

    public Task<TipoDocumento?> ObtenerTipoDocumentoAsync(string codigo, CancellationToken ct = default) =>
        db.TiposDocumento.SingleOrDefaultAsync(t => t.Codigo == codigo, ct);

    public Task<List<Banco>> ListarBancosAsync(CancellationToken ct = default) =>
        db.Bancos.OrderBy(b => b.Orden).ToListAsync(ct);

    public Task<List<DescuentoPos>> ListarDescuentosAsync(CancellationToken ct = default) =>
        db.DescuentosPos.OrderBy(d => d.Porcentaje).ToListAsync(ct);

    public Task<List<GrupoTalla>> ListarGruposTallaAsync(CancellationToken ct = default) =>
        db.GruposTalla.Include(g => g.Tallas.OrderBy(t => t.Orden)).OrderBy(g => g.Orden).ToListAsync(ct);

    public async Task<bool> TipoDocumentoEnUsoAsync(int id, CancellationToken ct = default) =>
        await db.Proveedores.AnyAsync(p => p.TipoDocumentoId == id, ct)
        || await db.Usuarios.AnyAsync(u => u.TipoDocumentoId == id, ct)
        || await db.Clientes.AnyAsync(c => c.TipoDocumentoId == id, ct);

    public Task<bool> BancoEnUsoAsync(int id, CancellationToken ct = default) =>
        db.ComprobantesTransferencia.AnyAsync(c => c.BancoId == id, ct);

    public async Task<bool> TallaEnUsoAsync(int id, CancellationToken ct = default) =>
        await db.ProductoTallas.AnyAsync(p => p.TallaId == id, ct)
        || await db.CompraItems.AnyAsync(i => i.TallaId == id, ct)
        || await db.VentaItems.AnyAsync(i => i.TallaId == id, ct)
        || await db.MovimientosInventario.AnyAsync(m => m.TallaId == id, ct);

    public void Agregar<T>(T entidad)
        where T : class => db.Add(entidad);

    public void Eliminar<T>(T entidad)
        where T : class => db.Remove(entidad);
}
