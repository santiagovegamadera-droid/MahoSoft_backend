namespace MahoSoft.Datos.Repositorios;

/// <summary>
/// Saves at once everything the repositories changed during the request, so an operation that touches
/// several tables (a sale, a purchase) is stored whole or not at all.
/// </summary>
public interface IUnidadDeTrabajo
{
    Task GuardarCambiosAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs <paramref name="accion"/> in a database transaction, committed only if it finishes without error.
    /// Needed when a read must stay locked until the save (e.g. taking the next consecutive number).
    /// </summary>
    Task<T> EnTransaccionAsync<T>(Func<Task<T>> accion, CancellationToken ct = default);
}

public class UnidadDeTrabajo(AppDbContext db) : IUnidadDeTrabajo
{
    public Task GuardarCambiosAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task<T> EnTransaccionAsync<T>(Func<Task<T>> accion, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var resultado = await accion();
        await tx.CommitAsync(ct);
        return resultado;
    }
}
