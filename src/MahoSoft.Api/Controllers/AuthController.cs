using MahoSoft.Api.Auth;
using MahoSoft.Negocio.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MahoSoft.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthServicio auth, TokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<ActionResult<SesionResponse>> Login(LoginRequest req, CancellationToken ct) =>
        tokens.Crear(await auth.LoginAsync(req, ct));

    /// <summary>The signed-in user, with permissions as they are now in the database.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<UsuarioSesion>> Me(CancellationToken ct)
    {
        var usuario = await auth.ObtenerSesionActivaAsync(User.UsuarioId(), ct);
        return usuario is null ? Unauthorized() : usuario;
    }

    [HttpPost("cambiar-password")]
    public async Task<IActionResult> CambiarPassword(CambiarPasswordRequest req, CancellationToken ct)
    {
        await auth.CambiarPasswordAsync(User.UsuarioId(), req, ct);
        return NoContent();
    }
}
