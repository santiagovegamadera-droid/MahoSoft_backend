using MahoSoft.Api.Auth;
using MahoSoft.Negocio.Usuarios;
using Microsoft.AspNetCore.Mvc;

namespace MahoSoft.Api.Controllers;

/// <summary>
/// The signed-in user's own details. The system has a single user (the administrator), so there is no endpoint to
/// create or manage others. The password is changed at POST /api/auth/cambiar-password.
/// </summary>
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
