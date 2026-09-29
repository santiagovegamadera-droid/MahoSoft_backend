using System.Net.Mail;
using MahoSoft.Datos.Repositorios;
using MahoSoft.Entidades;
using Microsoft.AspNetCore.Identity;

namespace MahoSoft.Negocio.Usuarios;

public interface IUsuarioServicio
{
    Task<List<UsuarioDto>> ListarAsync(CancellationToken ct = default);

    Task<UsuarioDto> CrearAsync(UsuarioRequest req, CancellationToken ct = default);

    /// <summary>
    /// Edits a user, also activating or deactivating it. <paramref name="editorId"/> is who edits: nobody can lock
    /// themselves out, and at least one active user must keep the Usuarios permission.
    /// </summary>
    Task<UsuarioDto> ActualizarAsync(int id, UsuarioRequest req, int editorId, CancellationToken ct = default);

    /// <summary>Sets a new password for a user who forgot it (the administrator tells it to them).</summary>
    Task RestablecerPasswordAsync(int id, PasswordRequest req, CancellationToken ct = default);

    Task<UsuarioDto> ObtenerPerfilAsync(int usuarioId, CancellationToken ct = default);

    Task<UsuarioDto> ActualizarPerfilAsync(int usuarioId, PerfilRequest req, CancellationToken ct = default);
}

public class UsuarioServicio(
    IUsuarioRepositorio usuarios,
    IConfiguracionRepositorio config,
    IPasswordHasher<Usuario> hasher,
    IUnidadDeTrabajo unidad
) : IUsuarioServicio
{
    private const string NoExiste = "El usuario no existe";
    public const int PasswordMinimo = 8;

    public async Task<List<UsuarioDto>> ListarAsync(CancellationToken ct = default) =>
        (await usuarios.ListarAsync(ct)).Select(UsuarioDto.De).ToList();

    public async Task<UsuarioDto> CrearAsync(UsuarioRequest req, CancellationToken ct = default)
    {
        var usuario = new Usuario { CreadoEn = DateTimeOffset.UtcNow };
        await AplicarDatosAsync(usuario, req.Nombre, req.Email, req.Telefono, req.TipoDocumento, req.Documento, ct);
        AplicarAcceso(usuario, req);
        usuario.PasswordHash = hasher.HashPassword(usuario, ValidarPassword(req.Password));
        usuarios.Agregar(usuario);
        await unidad.GuardarCambiosAsync(ct);
        return await ObtenerAsync(usuario.Id, ct);
    }

    public async Task<UsuarioDto> ActualizarAsync(
        int id,
        UsuarioRequest req,
        int editorId,
        CancellationToken ct = default
    )
    {
        var usuario = await usuarios.ObtenerPorIdAsync(id, ct) ?? throw new NoEncontradoException(NoExiste);
        await AplicarDatosAsync(usuario, req.Nombre, req.Email, req.Telefono, req.TipoDocumento, req.Documento, ct);
        AplicarAcceso(usuario, req);

        var administra = usuario.Activo && usuario.Permisos.Any(p => p.Permiso == Permiso.Usuarios);
        if (id == editorId && !usuario.Activo)
            throw new ValidacionException("No puedes desactivar tu propio usuario");
        if (id == editorId && !administra)
            throw new ValidacionException("No puedes quitarte el permiso Usuarios: te quedarías sin acceso a esta pantalla");
        if (!administra && await usuarios.ContarAdministradoresAsync(id, ct) == 0)
            throw new ConflictoException("Debe quedar al menos un usuario activo con el permiso Usuarios");

        await unidad.GuardarCambiosAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task RestablecerPasswordAsync(int id, PasswordRequest req, CancellationToken ct = default)
    {
        var usuario = await usuarios.ObtenerPorIdAsync(id, ct) ?? throw new NoEncontradoException(NoExiste);
        usuario.PasswordHash = hasher.HashPassword(usuario, ValidarPassword(req.Nueva));
        await unidad.GuardarCambiosAsync(ct);
    }

    public Task<UsuarioDto> ObtenerPerfilAsync(int usuarioId, CancellationToken ct = default) =>
        ObtenerAsync(usuarioId, ct);

    public async Task<UsuarioDto> ActualizarPerfilAsync(int usuarioId, PerfilRequest req, CancellationToken ct = default)
    {
        var usuario = await usuarios.ObtenerPorIdAsync(usuarioId, ct) ?? throw new NoEncontradoException(NoExiste);
        await AplicarDatosAsync(usuario, req.Nombre, req.Email, req.Telefono, req.TipoDocumento, req.Documento, ct);
        await unidad.GuardarCambiosAsync(ct);
        return await ObtenerAsync(usuarioId, ct);
    }

    private async Task<UsuarioDto> ObtenerAsync(int id, CancellationToken ct) =>
        UsuarioDto.De(await usuarios.ObtenerPorIdAsync(id, ct) ?? throw new NoEncontradoException(NoExiste));

    /// <summary>Personal details, shared by the Usuarios screen and the user's own profile.</summary>
    private async Task AplicarDatosAsync(
        Usuario usuario,
        string nombreDado,
        string emailDado,
        string? telefono,
        string? tipoDocumento,
        string? documento,
        CancellationToken ct
    )
    {
        var nombre = Texto(nombreDado, 150, "El nombre") ?? throw new ValidacionException("El nombre es obligatorio");
        var email = (Texto(emailDado, 256, "El email") ?? throw new ValidacionException("El email es obligatorio"))
            .ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out _))
            throw new ValidacionException("El email no es válido");
        if (await usuarios.ExisteEmailAsync(email, usuario.Id == 0 ? null : usuario.Id, ct))
            throw new ConflictoException($"Ya hay un usuario con el email {email}");

        var doc = Texto(documento, 30, "El documento");
        TipoDocumento? tipo = null;
        if (doc is not null)
        {
            tipo = await config.ObtenerTipoDocumentoAsync((tipoDocumento ?? "").Trim(), ct);
            if (tipo is null || (!tipo.Activo && tipo.Id != usuario.TipoDocumentoId))
                throw new ValidacionException("Elige un tipo de documento de la lista");
        }

        usuario.Nombre = nombre;
        usuario.Email = email;
        usuario.Telefono = Texto(telefono, 30, "El teléfono");
        usuario.TipoDocumento = tipo;
        usuario.TipoDocumentoId = tipo?.Id;
        usuario.Documento = doc;
    }

    private static void AplicarAcceso(Usuario usuario, UsuarioRequest req)
    {
        if (!Enum.IsDefined(req.Rol))
            throw new ValidacionException("Elige un rol");
        usuario.Rol = req.Rol;
        usuario.Activo = req.Activo;

        var permisos = (req.Permisos ?? []).Distinct().ToList();
        if (permisos.Any(p => !Enum.IsDefined(p)))
            throw new ValidacionException("Uno de los permisos no existe");
        usuario.Permisos.RemoveAll(p => !permisos.Contains(p.Permiso));
        foreach (var p in permisos.Where(p => usuario.Permisos.All(x => x.Permiso != p)))
            usuario.Permisos.Add(new UsuarioPermiso { Permiso = p });
    }

    private static string ValidarPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < PasswordMinimo)
            throw new ValidacionException($"La contraseña debe tener al menos {PasswordMinimo} caracteres");
        return password;
    }

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
