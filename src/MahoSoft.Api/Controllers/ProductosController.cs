using MahoSoft.Api.Auth;
using MahoSoft.Entidades;
using MahoSoft.Negocio.Productos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MahoSoft.Api.Controllers;

/// <summary>Anyone signed in can read products (the POS sells them); changing them needs Compras.</summary>
[ApiController]
[Route("api/productos")]
public class ProductosController(IProductoServicio productos) : ControllerBase
{
    [HttpGet]
    public Task<List<ProductoDto>> Listar(CancellationToken ct) => productos.ListarAsync(ct);

    [HttpGet("{id:int}")]
    public Task<ProductoDto> Obtener(int id, CancellationToken ct) => productos.ObtenerAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = nameof(Permiso.Compras))]
    public async Task<ActionResult<ProductoDto>> Crear(ProductoRequest req, CancellationToken ct)
    {
        var producto = await productos.CrearAsync(req, User.UsuarioId(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = producto.Id }, producto);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = nameof(Permiso.Compras))]
    public Task<ProductoDto> Actualizar(int id, ProductoRequest req, CancellationToken ct) =>
        productos.ActualizarAsync(id, req, User.UsuarioId(), ct);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = nameof(Permiso.Compras))]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await productos.EliminarAsync(id, ct);
        return NoContent();
    }

    /// <summary>Uploads a photo (multipart, field "archivo"); then save the product with the returned id.</summary>
    [HttpPost("imagenes")]
    [Authorize(Policy = nameof(Permiso.Compras))]
    [RequestSizeLimit(ProductoServicio.ImagenTamanoMaximo + 64 * 1024)]
    public async Task<ActionResult<ImagenDto>> SubirImagen(IFormFile archivo, CancellationToken ct)
    {
        await using var contenido = archivo.OpenReadStream();
        return await productos.SubirImagenAsync(contenido, archivo.FileName, archivo.ContentType, archivo.Length, ct);
    }
}
