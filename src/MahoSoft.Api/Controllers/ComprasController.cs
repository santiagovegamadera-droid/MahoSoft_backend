using System.Text.Json;
using MahoSoft.Api.Auth;
using MahoSoft.Entidades;
using MahoSoft.Negocio.Compras;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace MahoSoft.Api.Controllers;

[ApiController]
[Route("api/compras")]
[Authorize(Policy = nameof(Permiso.Compras))]
public class ComprasController(ICompraServicio compras, IOptions<JsonOptions> json) : ControllerBase
{
    [HttpGet]
    public Task<List<CompraDto>> Listar(CancellationToken ct) => compras.ListarAsync(ct);

    [HttpGet("{id:int}")]
    public Task<CompraDto> Obtener(int id, CancellationToken ct) => compras.ObtenerAsync(id, ct);

    /// <summary>
    /// multipart/form-data: "datos" is the purchase as JSON (<see cref="CompraRequest"/>) and "documento" the
    /// optional invoice PDF or photo, so both are saved together or not at all.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(CompraServicio.DocumentoTamanoMaximo + 256 * 1024)]
    public async Task<ActionResult<CompraDto>> Registrar([FromForm] string datos, IFormFile? documento, CancellationToken ct)
    {
        CompraRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<CompraRequest>(datos, json.Value.JsonSerializerOptions);
        }
        catch (JsonException)
        {
            req = null;
        }
        if (req is null)
            return Problem("Los datos de la compra no son válidos", statusCode: StatusCodes.Status400BadRequest);

        await using var contenido = documento?.OpenReadStream();
        var archivo = documento is null ? null : new ArchivoEntrante(contenido!, documento.FileName, documento.Length);
        var compra = await compras.RegistrarAsync(req, archivo, User.UsuarioId(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = compra.Id }, compra);
    }

    [HttpPost("{id:int}/pagada")]
    public Task<CompraDto> MarcarPagada(int id, CancellationToken ct) => compras.MarcarPagadaAsync(id, ct);

    /// <summary>The invoice document, shown in the browser (inline) with its original name.</summary>
    [HttpGet("{id:int}/documento")]
    public async Task<IActionResult> Documento(int id, CancellationToken ct)
    {
        var doc = await compras.ObtenerDocumentoAsync(id, ct);
        Response.Headers.ContentDisposition = new System.Net.Mime.ContentDisposition
        {
            Inline = true,
            FileName = doc.Nombre,
        }.ToString();
        return File(doc.Contenido, doc.TipoMime);
    }
}
