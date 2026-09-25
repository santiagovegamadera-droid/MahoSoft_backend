using MahoSoft.Entidades;
using Microsoft.EntityFrameworkCore;

namespace MahoSoft.Datos.Repositorios;

public interface IUsuarioRepositorio
{
    /// <summary>The user with that email and their permissions, or null.</summary>
    Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct = default);

    /// <summary>The user with that id and their permissions, or null.</summary>
    Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
}

public class UsuarioRepositorio(AppDbContext db) : IUsuarioRepositorio
{
    public Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken ct = default) =>
        db.Usuarios.Include(u => u.Permisos).SingleOrDefaultAsync(u => u.Email == email, ct);

    public Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        db.Usuarios.Include(u => u.Permisos).SingleOrDefaultAsync(u => u.Id == id, ct);
}
