using MahoSoft.Api.Auth;
using MahoSoft.Entidades;
using MahoSoft.Negocio.Inventario;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MahoSoft.Api.Controllers;

/// <summary>Stock movements and changes made by hand (counts, damaged garments, returns); permission Compras.</summary>
[ApiController]
[Route("api/inventario")]
[Authorize(Policy = nameof(Permiso.Compras))]
public class InventarioController(IInventarioServicio inventario) : ControllerBase
{
    /// <summary>The latest 300 movements, newest first; optionally of one product or one type.</summary>
    [HttpGet("movimientos")]
    public Task<List<MovimientoDto>> Movimientos(
        [FromQuery] int? productoId,
        [FromQuery] TipoMovimiento? tipo,
        CancellationToken ct
    ) => inventario.MovimientosAsync(productoId, tipo, ct);

    [HttpPost("ajustes")]
    public async Task<ActionResult<MovimientoDto>> Ajustar(AjusteRequest req, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await inventario.AjustarAsync(req, User.UsuarioId(), ct));
}
