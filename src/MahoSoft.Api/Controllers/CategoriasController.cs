using MahoSoft.Entidades;
using MahoSoft.Negocio.Categorias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MahoSoft.Api.Controllers;

/// <summary>Anyone signed in can read categories (the POS filters by them); changing them needs Compras.</summary>
[ApiController]
[Route("api/categorias")]
public class CategoriasController(ICategoriaServicio categorias) : ControllerBase
{
    [HttpGet]
    public Task<List<CategoriaDto>> Listar(CancellationToken ct) => categorias.ListarAsync(ct);

    [HttpGet("{id:int}")]
    public Task<CategoriaDto> Obtener(int id, CancellationToken ct) => categorias.ObtenerAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = nameof(Permiso.Compras))]
    public async Task<ActionResult<CategoriaDto>> Crear(CategoriaRequest req, CancellationToken ct)
    {
        var categoria = await categorias.CrearAsync(req, ct);
        return CreatedAtAction(nameof(Obtener), new { id = categoria.Id }, categoria);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = nameof(Permiso.Compras))]
    public Task<CategoriaDto> Actualizar(int id, CategoriaRequest req, CancellationToken ct) =>
        categorias.ActualizarAsync(id, req, ct);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = nameof(Permiso.Compras))]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await categorias.EliminarAsync(id, ct);
        return NoContent();
    }
}
