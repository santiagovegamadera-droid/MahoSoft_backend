using System.ComponentModel.DataAnnotations;
using MahoSoft.Api.Auth;
using MahoSoft.Api.Data;
using MahoSoft.Api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace MahoSoft.Api.Controllers;

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public record CambiarPasswordRequest([Required] string Actual, [Required, MinLength(8)] string Nueva);

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IPasswordHasher<Usuario> hasher, TokenService tokens) : ControllerBase
{
    private const string CredencialesInvalidas = "Correo o contraseña incorrectos";

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<ActionResult<SesionResponse>> Login(LoginRequest req)
    {
        var email = req.Email.Trim();
        var usuario = await db.Usuarios.Include(u => u.Permisos).SingleOrDefaultAsync(u => u.Email == email);

        // Same answer for an unknown email and a wrong password, so emails can't be probed
        if (usuario is null)
            return Problem(CredencialesInvalidas, statusCode: StatusCodes.Status401Unauthorized);
        var resultado = hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, req.Password);
        if (resultado == PasswordVerificationResult.Failed)
            return Problem(CredencialesInvalidas, statusCode: StatusCodes.Status401Unauthorized);

        if (!usuario.Activo)
            return Problem(
                "Tu usuario está desactivado. Pídele a la administradora que lo active.",
                statusCode: StatusCodes.Status403Forbidden
            );

        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
            usuario.PasswordHash = hasher.HashPassword(usuario, req.Password);
        usuario.UltimoAcceso = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return tokens.Crear(usuario);
    }

    /// <summary>The signed-in user, with permissions as they are now in the database.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<UsuarioSesion>> Me()
    {
        var usuario = await db.Usuarios.Include(u => u.Permisos).SingleAsync(u => u.Id == User.UsuarioId());
        return UsuarioSesion.De(usuario);
    }

    [HttpPost("cambiar-password")]
    public async Task<IActionResult> CambiarPassword(CambiarPasswordRequest req)
    {
        var usuario = await db.Usuarios.SingleAsync(u => u.Id == User.UsuarioId());
        if (hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, req.Actual) == PasswordVerificationResult.Failed)
            return Problem("La contraseña actual no es correcta", statusCode: StatusCodes.Status400BadRequest);

        usuario.PasswordHash = hasher.HashPassword(usuario, req.Nueva);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
