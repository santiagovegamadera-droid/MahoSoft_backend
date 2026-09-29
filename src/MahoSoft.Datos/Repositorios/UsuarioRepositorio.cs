using MahoSoft.Entidades;
using Microsoft.EntityFrameworkCore;

namespace MahoSoft.Datos.Repositorios;

public interface IUsuarioRepositorio
{
    /// <summary>The user with that email and their permissions, or null.</summary>
    Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct = default);

    /// <summary>The user with that id, their permissions and document type, tracked for changes; or null.</summary>
    Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken ct = default);

    /// <summary>Whether another user (other than <paramref name="excluirId"/>) signs in with that email.</summary>
    Task<bool> ExisteEmailAsync(string email, int? excluirId = null, CancellationToken ct = default);

    /// <summary>The user holding that password-reset code (by its hash), tracked for changes; or null.</summary>
    Task<Usuario?> ObtenerPorResetTokenAsync(string hash, CancellationToken ct = default);

}

public class UsuarioRepositorio(AppDbContext db) : IUsuarioRepositorio
{
    public Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct = default) =>
        db.Usuarios.Include(u => u.Permisos).SingleOrDefaultAsync(u => u.Email == email, ct);

    public Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        db.Usuarios.Include(u => u.Permisos).Include(u => u.TipoDocumento).SingleOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> ExisteEmailAsync(string email, int? excluirId = null, CancellationToken ct = default) =>
        db.Usuarios.AnyAsync(u => u.Email == email && u.Id != excluirId, ct);

    public Task<Usuario?> ObtenerPorResetTokenAsync(string hash, CancellationToken ct = default) =>
        db.Usuarios.SingleOrDefaultAsync(u => u.ResetTokenHash == hash, ct);
}
