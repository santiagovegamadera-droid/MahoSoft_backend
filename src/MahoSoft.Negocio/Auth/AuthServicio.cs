using MahoSoft.Datos.Repositorios;
using MahoSoft.Entidades;
using Microsoft.AspNetCore.Identity;

namespace MahoSoft.Negocio.Auth;

public interface IAuthServicio
{
    /// <summary>Checks the credentials and records the access. Throws if they are wrong or the user is deactivated.</summary>
    Task<UsuarioSesion> LoginAsync(LoginRequest req, CancellationToken ct = default);

    /// <summary>The user with their current permissions, or null if they no longer exist or were deactivated.</summary>
    Task<UsuarioSesion?> ObtenerSesionActivaAsync(int usuarioId, CancellationToken ct = default);

    Task CambiarPasswordAsync(int usuarioId, CambiarPasswordRequest req, CancellationToken ct = default);
}

public class AuthServicio(IUsuarioRepositorio usuarios, IUnidadDeTrabajo unidad, IPasswordHasher<Usuario> hasher)
    : IAuthServicio
{
    private const string CredencialesInvalidas = "Correo o contraseña incorrectos";

    public async Task<UsuarioSesion> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var usuario = await usuarios.ObtenerPorEmailAsync(req.Email.Trim(), ct);

        // Same answer for an unknown email and a wrong password, so emails can't be probed
        if (usuario is null)
            throw new NoAutenticadoException(CredencialesInvalidas);
        var resultado = hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, req.Password);
        if (resultado == PasswordVerificationResult.Failed)
            throw new NoAutenticadoException(CredencialesInvalidas);

        if (!usuario.Activo)
            throw new AccesoDenegadoException("Tu usuario está desactivado. Pídele a la administradora que lo active.");

        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
            usuario.PasswordHash = hasher.HashPassword(usuario, req.Password);
        usuario.UltimoAcceso = DateTimeOffset.UtcNow;
        await unidad.GuardarCambiosAsync(ct);

        return UsuarioSesion.De(usuario);
    }

    public async Task<UsuarioSesion?> ObtenerSesionActivaAsync(int usuarioId, CancellationToken ct = default)
    {
        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId, ct);
        return usuario is { Activo: true } ? UsuarioSesion.De(usuario) : null;
    }

    public async Task CambiarPasswordAsync(int usuarioId, CambiarPasswordRequest req, CancellationToken ct = default)
    {
        var usuario =
            await usuarios.ObtenerPorIdAsync(usuarioId, ct) ?? throw new NoEncontradoException("El usuario no existe");
        if (hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, req.Actual) == PasswordVerificationResult.Failed)
            throw new ValidacionException("La contraseña actual no es correcta");

        usuario.PasswordHash = hasher.HashPassword(usuario, req.Nueva);
        await unidad.GuardarCambiosAsync(ct);
    }
}
