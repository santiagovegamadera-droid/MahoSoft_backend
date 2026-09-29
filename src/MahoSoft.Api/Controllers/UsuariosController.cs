using MahoSoft.Api.Auth;
using MahoSoft.Entidades;
using MahoSoft.Negocio.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MahoSoft.Api.Controllers;

/// <summary>Users are managed with the Usuarios permission. They are deactivated, never deleted (their history stays).</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = nameof(Permiso.Usuarios))]
public class UsuariosController(IUsuarioServicio usuarios) : ControllerBase
{
    [HttpGet]
    public Task<List<UsuarioDto>> Listar(CancellationToken ct) => usuarios.ListarAsync(ct);

    /// <summary>Password is required here: it is the one the user signs in with the first time.</summary>
    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Crear(UsuarioRequest req, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await usuarios.CrearAsync(req, ct));

    [HttpPut("{id:int}")]
    public Task<UsuarioDto> Actualizar(int id, UsuarioRequest req, CancellationToken ct) =>
        usuarios.ActualizarAsync(id, req, User.UsuarioId(), ct);

    [HttpPost("{id:int}/password")]
    public async Task<IActionResult> RestablecerPassword(int id, PasswordRequest req, CancellationToken ct)
    {
        await usuarios.RestablecerPasswordAsync(id, req, ct);
        return NoContent();
    }
}

/// <summary>The signed-in user's own details. The password is changed at POST /api/auth/cambiar-password.</summary>
[ApiController]
[Route("api/perfil")]
public class PerfilController(IUsuarioServicio usuarios) : ControllerBase
{
    [HttpGet]
    public Task<UsuarioDto> Obtener(CancellationToken ct) => usuarios.ObtenerPerfilAsync(User.UsuarioId(), ct);

    [HttpPut]
    public Task<UsuarioDto> Actualizar(PerfilRequest req, CancellationToken ct) =>
        usuarios.ActualizarPerfilAsync(User.UsuarioId(), req, ct);
}
