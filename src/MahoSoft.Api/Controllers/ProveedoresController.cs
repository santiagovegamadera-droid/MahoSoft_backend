using MahoSoft.Api.Auth;
using MahoSoft.Entidades;
using MahoSoft.Negocio.Proveedores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MahoSoft.Api.Controllers;

/// <summary>
/// Reading needs Proveedores or Compras (purchases pick a supplier and show its details); changing
/// suppliers needs Proveedores.
/// </summary>
[ApiController]
[Route("api/proveedores")]
[Authorize(Policy = Politicas.VerProveedores)]
public class ProveedoresController(IProveedorServicio proveedores) : ControllerBase
{
    [HttpGet]
    public Task<List<ProveedorDto>> Listar(CancellationToken ct) => proveedores.ListarAsync(ct);

    [HttpGet("{id:int}")]
    public Task<ProveedorDto> Obtener(int id, CancellationToken ct) => proveedores.ObtenerAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = nameof(Permiso.Proveedores))]
    public async Task<ActionResult<ProveedorDto>> Crear(ProveedorRequest req, CancellationToken ct)
    {
        var proveedor = await proveedores.CrearAsync(req, ct);
        return CreatedAtAction(nameof(Obtener), new { id = proveedor.Id }, proveedor);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = nameof(Permiso.Proveedores))]
    public Task<ProveedorDto> Actualizar(int id, ProveedorRequest req, CancellationToken ct) =>
        proveedores.ActualizarAsync(id, req, ct);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = nameof(Permiso.Proveedores))]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await proveedores.EliminarAsync(id, ct);
        return NoContent();
    }
}
