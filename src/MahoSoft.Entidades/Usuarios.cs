namespace MahoSoft.Entidades;

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public Rol Rol { get; set; }
    public string? Telefono { get; set; }
    public int? TipoDocumentoId { get; set; }
    public TipoDocumento? TipoDocumento { get; set; }
    public string? Documento { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset? UltimoAcceso { get; set; }

    /// <summary>SHA-256 of the password-reset code sent by email (the code itself is never stored), or null.</summary>
    public string? ResetTokenHash { get; set; }

    /// <summary>When the reset code stops working.</summary>
    public DateTimeOffset? ResetTokenExpira { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public List<UsuarioPermiso> Permisos { get; set; } = [];
}

public class UsuarioPermiso
{
    public int UsuarioId { get; set; }
    public Permiso Permiso { get; set; }
}
