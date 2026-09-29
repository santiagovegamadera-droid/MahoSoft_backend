using MahoSoft.Entidades;
using Microsoft.EntityFrameworkCore;

namespace MahoSoft.Datos.Repositorios;

public interface IVentaRepositorio
{
    /// <summary>Every sale (voided ones too) with customer, seller, delivery, receipt and items, newest first.</summary>
    Task<List<Venta>> ListarAsync(CancellationToken ct = default);

    Task<Venta?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>The sale with its items, tracked for changes.</summary>
    Task<Venta?> ObtenerParaEditarAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Next invoice number after the highest <c>{prefijo}NNNN</c> (e.g. VTA-2026-0847). Must run inside a
    /// transaction: numbering stays locked until it ends, so two tills can't get the same number.
    /// </summary>
    Task<string> SiguienteNumeroAsync(string prefijo, CancellationToken ct = default);

    void Agregar(Venta venta);
}

public class VentaRepositorio(AppDbContext db) : IVentaRepositorio
{
    private IQueryable<Venta> Completa() =>
        db.Ventas.AsNoTracking()
            .Include(v => v.Cliente)
            .ThenInclude(c => c!.TipoDocumento)
            .Include(v => v.Vendedor)
            .Include(v => v.AnuladaPor)
            .Include(v => v.Entrega)
            .Include(v => v.Comprobante)
            .ThenInclude(c => c!.Banco)
            .Include(v => v.Comprobante)
            .ThenInclude(c => c!.Archivo)
            .Include(v => v.Items)
            .ThenInclude(i => i.Talla)
            .AsSplitQuery();

    public Task<List<Venta>> ListarAsync(CancellationToken ct = default) =>
        Completa().OrderByDescending(v => v.Fecha).ThenByDescending(v => v.Id).ToListAsync(ct);

    public Task<Venta?> ObtenerAsync(int id, CancellationToken ct = default) =>
        Completa().SingleOrDefaultAsync(v => v.Id == id, ct);

    public Task<Venta?> ObtenerParaEditarAsync(int id, CancellationToken ct = default) =>
        db.Ventas.Include(v => v.Items).ThenInclude(i => i.Talla).SingleOrDefaultAsync(v => v.Id == id, ct);

    public async Task<string> SiguienteNumeroAsync(string prefijo, CancellationToken ct = default)
    {
        // Held until the transaction ends, also when there is no sale yet to lock
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({Bloqueos.NumeroVenta})", ct);
        var numeros = await db.Ventas
            .Where(v => v.NumeroFactura.StartsWith(prefijo))
            .Select(v => v.NumeroFactura)
            .ToListAsync(ct);
        var ultimo = numeros.Select(n => int.TryParse(n[prefijo.Length..], out var x) ? x : 0).DefaultIfEmpty(0).Max();
        return $"{prefijo}{ultimo + 1:0000}";
    }

    public void Agregar(Venta venta) => db.Ventas.Add(venta);
}

public interface IClienteRepositorio
{
    Task<Cliente?> ObtenerPorDocumentoAsync(int tipoDocumentoId, string documento, CancellationToken ct = default);

    /// <summary>The most recent customer with that phone, tracked for changes.</summary>
    Task<Cliente?> ObtenerPorTelefonoAsync(string telefono, CancellationToken ct = default);

    void Agregar(Cliente cliente);
}

public class ClienteRepositorio(AppDbContext db) : IClienteRepositorio
{
    public Task<Cliente?> ObtenerPorDocumentoAsync(int tipoDocumentoId, string documento, CancellationToken ct = default) =>
        db.Clientes.SingleOrDefaultAsync(c => c.TipoDocumentoId == tipoDocumentoId && c.Documento == documento, ct);

    public Task<Cliente?> ObtenerPorTelefonoAsync(string telefono, CancellationToken ct = default) =>
        db.Clientes.Where(c => c.Telefono == telefono).OrderByDescending(c => c.Id).FirstOrDefaultAsync(ct);

    public void Agregar(Cliente cliente) => db.Clientes.Add(cliente);
}
