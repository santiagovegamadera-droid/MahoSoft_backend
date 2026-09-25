namespace MahoSoft.Datos.Repositorios;

/// <summary>
/// Saves at once everything the repositories changed during the request, so an operation that touches
/// several tables (a sale, a purchase) is stored whole or not at all.
/// </summary>
public interface IUnidadDeTrabajo
{
    Task GuardarCambiosAsync(CancellationToken ct = default);
}

public class UnidadDeTrabajo(AppDbContext db) : IUnidadDeTrabajo
{
    public Task GuardarCambiosAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
