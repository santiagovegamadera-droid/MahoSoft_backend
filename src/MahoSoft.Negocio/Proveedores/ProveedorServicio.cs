using System.Net.Mail;
using MahoSoft.Datos.Repositorios;
using MahoSoft.Entidades;

namespace MahoSoft.Negocio.Proveedores;

public interface IProveedorServicio
{
    Task<List<ProveedorDto>> ListarAsync(CancellationToken ct = default);

    Task<ProveedorDto> ObtenerAsync(int id, CancellationToken ct = default);

    Task<ProveedorDto> CrearAsync(ProveedorRequest req, CancellationToken ct = default);

    /// <summary>Also activates or deactivates it through <see cref="ProveedorRequest.Activo"/>.</summary>
    Task<ProveedorDto> ActualizarAsync(int id, ProveedorRequest req, CancellationToken ct = default);

    /// <summary>Only a supplier without purchases can be deleted; otherwise it should be deactivated.</summary>
    Task EliminarAsync(int id, CancellationToken ct = default);
}

public class ProveedorServicio(
    IProveedorRepositorio proveedores,
    IConfiguracionRepositorio config,
    IUnidadDeTrabajo unidad
) : IProveedorServicio
{
    private const string NoExiste = "El proveedor no existe";

    public async Task<List<ProveedorDto>> ListarAsync(CancellationToken ct = default) =>
        (await proveedores.ListarConResumenAsync(ct)).Select(ProveedorDto.De).ToList();

    public async Task<ProveedorDto> ObtenerAsync(int id, CancellationToken ct = default) =>
        ProveedorDto.De(await proveedores.ObtenerConResumenAsync(id, ct) ?? throw new NoEncontradoException(NoExiste));

    public async Task<ProveedorDto> CrearAsync(ProveedorRequest req, CancellationToken ct = default)
    {
        var proveedor = new Proveedor();
        await AplicarAsync(proveedor, req, ct);
        proveedores.Agregar(proveedor);
        await unidad.GuardarCambiosAsync(ct);
        return await ObtenerAsync(proveedor.Id, ct);
    }

    public async Task<ProveedorDto> ActualizarAsync(int id, ProveedorRequest req, CancellationToken ct = default)
    {
        var proveedor = await proveedores.ObtenerPorIdAsync(id, ct) ?? throw new NoEncontradoException(NoExiste);
        await AplicarAsync(proveedor, req, ct);
        await unidad.GuardarCambiosAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var proveedor = await proveedores.ObtenerPorIdAsync(id, ct) ?? throw new NoEncontradoException(NoExiste);
        // Its invoices would lose who issued them
        var compras = await proveedores.ContarComprasAsync(id, ct);
        if (compras > 0)
            throw new ConflictoException(
                $"{proveedor.Nombre} tiene {compras} {(compras == 1 ? "compra registrada" : "compras registradas")}. "
                    + "Desactívalo para que ya no aparezca al registrar compras."
            );

        proveedores.Eliminar(proveedor);
        await unidad.GuardarCambiosAsync(ct);
    }

    private async Task AplicarAsync(Proveedor proveedor, ProveedorRequest req, CancellationToken ct)
    {
        var nombre = req.Nombre.Trim();
        var documento = req.Documento.Trim();
        if (nombre.Length == 0)
            throw new ValidacionException("El nombre es obligatorio");
        if (documento.Length == 0)
            throw new ValidacionException("El documento es obligatorio");

        // A type deactivated in Configuración can't be chosen anew, but a supplier that already has it keeps it
        var tipo = await config.ObtenerTipoDocumentoAsync(req.TipoDocumento.Trim(), ct);
        if (tipo is null || (!tipo.Activo && tipo.Id != proveedor.TipoDocumentoId))
            throw new ValidacionException("Elige un tipo de documento de la lista");

        var excluir = proveedor.Id == 0 ? (int?)null : proveedor.Id;
        if (await proveedores.ExisteDocumentoAsync(tipo.Id, documento, excluir, ct))
            throw new ConflictoException($"Ya existe un proveedor con el documento {tipo.Codigo} {documento}");

        var email = Texto(req.Email);
        if (email is not null && !MailAddress.TryCreate(email, out _))
            throw new ValidacionException("El email no es válido");

        proveedor.Nombre = nombre;
        proveedor.TipoDocumento = tipo;
        proveedor.Documento = documento;
        proveedor.Direccion = Texto(req.Direccion);
        proveedor.Ciudad = Texto(req.Ciudad);
        proveedor.Telefono = Texto(req.Telefono);
        proveedor.Email = email;
        proveedor.Contacto = Texto(req.Contacto);
        proveedor.IvaPorcentaje = req.IvaPorcentaje;
        proveedor.Activo = req.Activo;
    }

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
