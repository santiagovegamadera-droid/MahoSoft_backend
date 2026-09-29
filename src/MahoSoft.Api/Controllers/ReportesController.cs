using MahoSoft.Api.Reportes;
using MahoSoft.Entidades;
using MahoSoft.Negocio.Configuracion;
using MahoSoft.Negocio.Reportes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MahoSoft.Api.Controllers;

/// <summary>The home screen's figures (Dashboard permission).</summary>
[ApiController]
[Route("api/tablero")]
[Authorize(Policy = nameof(Permiso.Dashboard))]
public class TableroController(IReporteServicio reportes) : ControllerBase
{
    [HttpGet]
    public Task<TableroDto> Obtener(CancellationToken ct) => reportes.TableroAsync(ct);
}

/// <summary>Sales reports between two dates (both included), as data, Excel or PDF (Reportes permission).</summary>
[ApiController]
[Route("api/reportes")]
[Authorize(Policy = nameof(Permiso.Reportes))]
public class ReportesController(IReporteServicio reportes, IConfiguracionServicio config) : ControllerBase
{
    [HttpGet]
    public Task<ReporteDto> Obtener([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, CancellationToken ct) =>
        reportes.ReporteAsync(desde, hasta, ct);

    [HttpGet("excel")]
    public async Task<IActionResult> Excel([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, CancellationToken ct)
    {
        var r = await reportes.ReporteAsync(desde, hasta, ct);
        var negocio = (await config.ObtenerAsync(ct)).Nombre;
        return File(
            ReporteArchivos.Excel(r, negocio),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ReporteArchivos.NombreArchivo(r, "xlsx")
        );
    }

    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, CancellationToken ct)
    {
        var r = await reportes.ReporteAsync(desde, hasta, ct);
        var negocio = (await config.ObtenerAsync(ct)).Nombre;
        return File(ReporteArchivos.Pdf(r, negocio), "application/pdf", ReporteArchivos.NombreArchivo(r, "pdf"));
    }
}
