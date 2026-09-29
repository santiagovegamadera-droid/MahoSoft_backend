using MahoSoft.Entidades;

namespace MahoSoft.Negocio.Usuarios;

/// <summary>The signed-in user's own details.</summary>
public record PerfilRequest(string Nombre, string Email, string? Telefono, string? TipoDocumento, string? Documento);

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
