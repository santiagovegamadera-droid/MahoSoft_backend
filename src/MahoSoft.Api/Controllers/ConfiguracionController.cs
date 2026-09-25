using MahoSoft.Entidades;
using MahoSoft.Negocio.Configuracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MahoSoft.Api.Controllers;

/// <summary>
/// Anyone signed in reads the settings (receipts, POS, product forms use them); only the administrator
/// changes them. Each section is saved on its own and the answer is the whole, updated settings.
/// </summary>
[ApiController]
[Route("api/configuracion")]
public class ConfiguracionController(IConfiguracionServicio config) : ControllerBase
{
    [HttpGet]
    public Task<ConfiguracionDto> Obtener(CancellationToken ct) => config.ObtenerAsync(ct);

    [HttpPut("negocio")]
    [Authorize(Roles = nameof(Rol.Administradora))]
    public Task<ConfiguracionDto> GuardarNegocio(NegocioRequest req, CancellationToken ct) =>
        config.GuardarNegocioAsync(req, ct);

    [HttpPut("inventario")]
    [Authorize(Roles = nameof(Rol.Administradora))]
    public Task<ConfiguracionDto> GuardarInventario(InventarioRequest req, CancellationToken ct) =>
        config.GuardarInventarioAsync(req, ct);

    [HttpPut("pos")]
    [Authorize(Roles = nameof(Rol.Administradora))]
    public Task<ConfiguracionDto> GuardarPos(PosRequest req, CancellationToken ct) => config.GuardarPosAsync(req, ct);

    [HttpPut("tallas")]
    [Authorize(Roles = nameof(Rol.Administradora))]
    public Task<ConfiguracionDto> GuardarTallas(TallasRequest req, CancellationToken ct) =>
        config.GuardarTallasAsync(req, ct);

    [HttpPut("tipos-documento")]
    [Authorize(Roles = nameof(Rol.Administradora))]
    public Task<ConfiguracionDto> GuardarTiposDocumento(TiposDocumentoRequest req, CancellationToken ct) =>
        config.GuardarTiposDocumentoAsync(req, ct);
}
