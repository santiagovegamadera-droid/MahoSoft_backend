using MahoSoft.Entidades;
using Microsoft.EntityFrameworkCore;

namespace MahoSoft.Datos.Repositorios;

public interface ICompraRepositorio
{
    /// <summary>Every purchase with its supplier, user, document and items, newest invoice first.</summary>
    Task<List<Compra>> ListarAsync(CancellationToken ct = default);

    Task<Compra?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>The purchase alone, tracked for changes.</summary>
    Task<Compra?> ObtenerParaEditarAsync(int id, CancellationToken ct = default);

    /// <summary>Whether this supplier's invoice number is already registered.</summary>
    Task<bool> ExisteComprobanteAsync(int proveedorId, string numeroComprobante, CancellationToken ct = default);

    /// <summary>
    /// Next consecutive after the highest <c>{prefijo}NNN</c> (e.g. OC-2026-046). Must run inside a transaction:
    /// numbering stays locked until it ends, so two purchases saved at once can't get the same number.
    /// </summary>
    Task<string> SiguienteNumeroAsync(string prefijo, CancellationToken ct = default);

    /// <summary>Whether a purchase with a later invoice date includes the product (its cost is newer).</summary>
    Task<bool> HayCompraPosteriorAsync(int productoId, DateTimeOffset fecha, CancellationToken ct = default);

    void Agregar(Compra compra);
}

public class CompraRepositorio(AppDbContext db) : ICompraRepositorio
{
    private IQueryable<Compra> Completa() =>
        db.Compras.AsNoTracking()
            .Include(c => c.Proveedor)
            .Include(c => c.Usuario)
            .Include(c => c.Documento)
            .Include(c => c.Items)
            .ThenInclude(i => i.Producto)
            .Include(c => c.Items)
            .ThenInclude(i => i.Talla)
            .AsSplitQuery();

    public Task<List<Compra>> ListarAsync(CancellationToken ct = default) =>
        Completa().OrderByDescending(c => c.FechaComprobante).ThenByDescending(c => c.Id).ToListAsync(ct);

    public Task<Compra?> ObtenerAsync(int id, CancellationToken ct = default) =>
        Completa().SingleOrDefaultAsync(c => c.Id == id, ct);

    public Task<Compra?> ObtenerParaEditarAsync(int id, CancellationToken ct = default) =>
        db.Compras.SingleOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExisteComprobanteAsync(int proveedorId, string numeroComprobante, CancellationToken ct = default) =>
        db.Compras.AnyAsync(c => c.ProveedorId == proveedorId && c.NumeroComprobante == numeroComprobante, ct);

    public async Task<string> SiguienteNumeroAsync(string prefijo, CancellationToken ct = default)
    {
        // Held until the transaction ends, also when there is no purchase yet to lock
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({Bloqueos.NumeroCompra})", ct);
        var numeros = await db.Compras.Where(c => c.Numero.StartsWith(prefijo)).Select(c => c.Numero).ToListAsync(ct);
        var ultimo = numeros.Select(n => int.TryParse(n[prefijo.Length..], out var x) ? x : 0).DefaultIfEmpty(0).Max();
        return $"{prefijo}{ultimo + 1:000}";
    }

    public Task<bool> HayCompraPosteriorAsync(int productoId, DateTimeOffset fecha, CancellationToken ct = default) =>
        db.Compras.AnyAsync(c => c.FechaComprobante > fecha && c.Items.Any(i => i.ProductoId == productoId), ct);

    public void Agregar(Compra compra) => db.Compras.Add(compra);
}
