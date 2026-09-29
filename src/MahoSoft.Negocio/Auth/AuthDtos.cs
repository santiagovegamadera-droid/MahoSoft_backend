using System.ComponentModel.DataAnnotations;
using MahoSoft.Entidades;

namespace MahoSoft.Negocio.Auth;

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public record CambiarPasswordRequest([Required] string Actual, [Required, MinLength(8)] string Nueva);

public record RecuperarPasswordRequest([Required, EmailAddress] string Email);

/// <summary>The code from the emailed link and the new password.</summary>
public record RestablecerPasswordRequest(
    [Required] string Codigo,
    [Required, MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")] string Nueva
);

/// <summary>The signed-in user as the frontend needs it.</summary>
public record UsuarioSesion(int Id, string Nombre, string Email, string Rol, string[] Permisos)
{
    public static UsuarioSesion De(Usuario u) =>
        new(u.Id, u.Nombre, u.Email, u.Rol.ToString(), u.Permisos.Select(p => p.Permiso.ToString()).Order().ToArray());
}
