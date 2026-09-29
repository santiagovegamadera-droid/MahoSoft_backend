using MahoSoft.Entidades;

namespace MahoSoft.Negocio.Usuarios;

/// <summary>
/// A user as the administrator edits it. Password is only read when creating (the initial one); later it is
/// reset with <see cref="PasswordRequest"/>. TipoDocumento is the code (CC, CE…).
/// </summary>
public record UsuarioRequest(
    string Nombre,
    string Email,
    Rol Rol,
    string? Telefono,
    string? TipoDocumento,
    string? Documento,
    Permiso[] Permisos,
    bool Activo = true,
    string? Password = null
);

/// <summary>The signed-in user's own details (role and permissions are set by the administrator).</summary>
public record PerfilRequest(string Nombre, string Email, string? Telefono, string? TipoDocumento, string? Documento);

public record PasswordRequest(string Nueva);

/// <summary>A user; empty text fields come as "". Never includes the password.</summary>
public record UsuarioDto(
    int Id,
    string Nombre,
    string Email,
    Rol Rol,
    string Telefono,
    string TipoDocumento,
    string Documento,
    bool Activo,
    DateTimeOffset? UltimoAcceso,
    DateTimeOffset CreadoEn,
    Permiso[] Permisos
)
{
    public static UsuarioDto De(Usuario u) =>
        new(
            u.Id,
            u.Nombre,
            u.Email,
            u.Rol,
            u.Telefono ?? "",
            u.TipoDocumento?.Codigo ?? "",
            u.Documento ?? "",
            u.Activo,
            u.UltimoAcceso,
            u.CreadoEn,
            u.Permisos.Select(p => p.Permiso).Order().ToArray()
        );
}
