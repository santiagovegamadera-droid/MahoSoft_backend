namespace MahoSoft.Api.Data.Entities;

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
    public DateTimeOffset CreadoEn { get; set; }
    public List<UsuarioPermiso> Permisos { get; set; } = [];
}

public class UsuarioPermiso
{
    public int UsuarioId { get; set; }
    public Permiso Permiso { get; set; }
}
