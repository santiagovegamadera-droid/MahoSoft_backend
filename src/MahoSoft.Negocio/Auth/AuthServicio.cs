using System.Security.Cryptography;
using System.Text;
using MahoSoft.Datos.Correo;
using MahoSoft.Datos.Repositorios;
using MahoSoft.Entidades;
using MahoSoft.Negocio.Correo;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace MahoSoft.Negocio.Auth;

public interface IAuthServicio
{
    /// <summary>Checks the credentials and records the access. Throws if they are wrong or the user is deactivated.</summary>
    Task<UsuarioSesion> LoginAsync(LoginRequest req, CancellationToken ct = default);

    /// <summary>The user with their current permissions, or null if they no longer exist or were deactivated.</summary>
    Task<UsuarioSesion?> ObtenerSesionActivaAsync(int usuarioId, CancellationToken ct = default);

    Task CambiarPasswordAsync(int usuarioId, CambiarPasswordRequest req, CancellationToken ct = default);

    /// <summary>
    /// Emails a link to set a new password. Answers the same whether the email exists or not, so it can't be used to
    /// find out who has an account.
    /// </summary>
    Task RecuperarPasswordAsync(RecuperarPasswordRequest req, CancellationToken ct = default);

    /// <summary>Sets a new password with the code from the emailed link (valid once, for 1 hour).</summary>
    Task RestablecerPasswordAsync(RestablecerPasswordRequest req, CancellationToken ct = default);
}

public class AuthServicio(
    IUsuarioRepositorio usuarios,
    IConfiguracionRepositorio config,
    IUnidadDeTrabajo unidad,
    IPasswordHasher<Usuario> hasher,
    IEnviadorCorreo correo,
    IOptions<AppOptions> app
) : IAuthServicio
{
    private static readonly TimeSpan VigenciaCodigo = TimeSpan.FromHours(1);

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

    public async Task RecuperarPasswordAsync(RecuperarPasswordRequest req, CancellationToken ct = default)
    {
        if (!correo.Configurado)
            throw new ServicioNoDisponibleException(
                "La recuperación por correo todavía no está configurada. Pídele a la administradora que te asigne una contraseña."
            );

        var usuario = await usuarios.ObtenerPorEmailAsync(req.Email.Trim(), ct);
        if (usuario is null || !usuario.Activo)
            return;

        // 32 random bytes as the code; only its hash is stored, so a copy of the database can't be used to reset
        var codigo = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        usuario.ResetTokenHash = Hash(codigo);
        usuario.ResetTokenExpira = DateTimeOffset.UtcNow.Add(VigenciaCodigo);
        await unidad.GuardarCambiosAsync(ct);

        var negocio = (await config.ObtenerNegocioAsync(ct)).Nombre;
        var enlace = $"{app.Value.UrlFrontend.TrimEnd('/')}/?restablecer={codigo}";
        await CorreoServicio.Enviar(
            () =>
                correo.EnviarAsync(
                    usuario.Email,
                    $"Cambia tu contraseña de {negocio}",
                    PlantillasCorreo.RecuperarPassword(negocio, usuario.Nombre, enlace),
                    ct
                )
        );
    }

    public async Task RestablecerPasswordAsync(RestablecerPasswordRequest req, CancellationToken ct = default)
    {
        var usuario = await usuarios.ObtenerPorResetTokenAsync(Hash(req.Codigo.Trim()), ct);
        if (usuario is null || usuario.ResetTokenExpira < DateTimeOffset.UtcNow || !usuario.Activo)
            throw new ValidacionException("El enlace no es válido o ya venció. Pide uno nuevo.");

        usuario.PasswordHash = hasher.HashPassword(usuario, req.Nueva);
        usuario.ResetTokenHash = null;
        usuario.ResetTokenExpira = null;
        await unidad.GuardarCambiosAsync(ct);
    }

    private static string Hash(string codigo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codigo))).ToLowerInvariant();
}
