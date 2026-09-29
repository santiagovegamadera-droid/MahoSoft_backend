using System.Text.Json;
using MahoSoft.Api.Auth;
using MahoSoft.Entidades;
using MahoSoft.Negocio;
using MahoSoft.Negocio.Correo;
using MahoSoft.Negocio.Ventas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace MahoSoft.Api.Controllers;

[ApiController]
[Route("api/ventas")]
[Authorize(Policy = nameof(Permiso.POS))]
public class VentasController(IVentaServicio ventas, IOptions<JsonOptions> json) : ControllerBase
{
    [HttpGet]
    public Task<List<VentaDto>> Listar(CancellationToken ct) => ventas.ListarAsync(ct);

    [HttpGet("{id:int}")]
    public Task<VentaDto> Obtener(int id, CancellationToken ct) => ventas.ObtenerAsync(id, ct);

    /// <summary>
    /// multipart/form-data: "datos" is the sale as JSON (<see cref="VentaRequest"/>) and "comprobante" the optional
    /// transfer receipt, so both are saved together or not at all.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(Documentos.TamanoMaximo + 256 * 1024)]
    public async Task<ActionResult<VentaDto>> Registrar([FromForm] string datos, IFormFile? comprobante, CancellationToken ct)
    {
        VentaRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<VentaRequest>(datos, json.Value.JsonSerializerOptions);
        }
        catch (JsonException)
        {
            req = null;
        }
        if (req is null)
            return Problem("Los datos de la venta no son válidos", statusCode: StatusCodes.Status400BadRequest);

        await using var contenido = comprobante?.OpenReadStream();
        var archivo = comprobante is null ? null : new ArchivoEntrante(contenido!, comprobante.FileName, comprobante.Length);
        var venta = await ventas.RegistrarAsync(req, archivo, User.UsuarioId(), ct);
        return CreatedAtAction(nameof(Obtener), new { id = venta.Id }, venta);
    }

    /// <summary>Emails the sale's receipt to the customer.</summary>
    [HttpPost("{id:int}/enviar")]
    public async Task<IActionResult> Enviar(int id, EnviarFacturaRequest req, [FromServices] ICorreoServicio correo, CancellationToken ct)
    {
        await correo.EnviarFacturaAsync(id, req.Correo, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/anular")]
    public Task<VentaDto> Anular(int id, AnularVentaRequest req, CancellationToken ct) =>
        ventas.AnularAsync(id, req, User.UsuarioId(), ct);

    /// <summary>The transfer receipt, shown in the browser (inline) with its original name.</summary>
    [HttpGet("{id:int}/comprobante")]
    public async Task<IActionResult> Comprobante(int id, CancellationToken ct)
    {
        var doc = await ventas.ObtenerComprobanteAsync(id, ct);
        Response.Headers.ContentDisposition = new System.Net.Mime.ContentDisposition
        {
            Inline = true,
            FileName = doc.Nombre,
        }.ToString();
        return File(doc.Contenido, doc.TipoMime);
    }
}

public record EnviarFacturaRequest(string Correo);
