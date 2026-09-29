using System.Net.Mail;
using MahoSoft.Datos.Repositorios;
using MahoSoft.Entidades;

namespace MahoSoft.Negocio.Usuarios;

/// <summary>The system has a single user, the administrator; this is their own profile.</summary>
public interface IUsuarioServicio
{
    Task<UsuarioDto> ObtenerPerfilAsync(int usuarioId, CancellationToken ct = default);

    Task<UsuarioDto> ActualizarPerfilAsync(int usuarioId, PerfilRequest req, CancellationToken ct = default);
}

public class UsuarioServicio(IUsuarioRepositorio usuarios, IConfiguracionRepositorio config, IUnidadDeTrabajo unidad)
    : IUsuarioServicio
{
    private const string NoExiste = "El usuario no existe";

    public Task<UsuarioDto> ObtenerPerfilAsync(int usuarioId, CancellationToken ct = default) =>
        ObtenerAsync(usuarioId, ct);

    public async Task<UsuarioDto> ActualizarPerfilAsync(int usuarioId, PerfilRequest req, CancellationToken ct = default)
    {
        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId, ct) ?? throw new NoEncontradoException(NoExiste);

        var nombre = Texto(req.Nombre, 150, "El nombre") ?? throw new ValidacionException("El nombre es obligatorio");
        var email = (Texto(req.Email, 256, "El email") ?? throw new ValidacionException("El email es obligatorio"))
            .ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out _))
            throw new ValidacionException("El email no es válido");
        if (await usuarios.ExisteEmailAsync(email, usuario.Id, ct))
            throw new ConflictoException($"Ya hay un usuario con el email {email}");

        var documento = Texto(req.Documento, 30, "El documento");
        TipoDocumento? tipo = null;
        if (documento is not null)
        {
            tipo = await config.ObtenerTipoDocumentoAsync((req.TipoDocumento ?? "").Trim(), ct);
            if (tipo is null || (!tipo.Activo && tipo.Id != usuario.TipoDocumentoId))
                throw new ValidacionException("Elige un tipo de documento de la lista");
        }

        usuario.Nombre = nombre;
        usuario.Email = email;
        usuario.Telefono = Texto(req.Telefono, 30, "El teléfono");
        usuario.TipoDocumento = tipo;
        usuario.TipoDocumentoId = tipo?.Id;
        usuario.Documento = documento;
        await unidad.GuardarCambiosAsync(ct);
        return await ObtenerAsync(usuarioId, ct);
    }

    private async Task<UsuarioDto> ObtenerAsync(int id, CancellationToken ct) =>
        UsuarioDto.De(await usuarios.ObtenerPorIdAsync(id, ct) ?? throw new NoEncontradoException(NoExiste));

    private static string? Texto(string? valor, int largoMaximo, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        var texto = valor.Trim();
        if (texto.Length > largoMaximo)
            throw new ValidacionException($"{campo} admite hasta {largoMaximo} caracteres");
        return texto;
    }
}
