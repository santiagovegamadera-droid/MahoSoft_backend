using System.ComponentModel.DataAnnotations;
using MahoSoft.Datos.Repositorios;

namespace MahoSoft.Negocio.Proveedores;

public record ProveedorRequest(
    [Required(ErrorMessage = "El nombre es obligatorio"), MaxLength(200, ErrorMessage = "El nombre admite hasta 200 caracteres")]
        string Nombre,
    [Required(ErrorMessage = "Elige el tipo de documento")] string TipoDocumento,
    [Required(ErrorMessage = "El documento es obligatorio"), MaxLength(30, ErrorMessage = "El documento admite hasta 30 caracteres")]
        string Documento,
    [MaxLength(250, ErrorMessage = "La dirección admite hasta 250 caracteres")] string? Direccion,
    [MaxLength(100, ErrorMessage = "La ciudad admite hasta 100 caracteres")] string? Ciudad,
    [MaxLength(30, ErrorMessage = "El teléfono admite hasta 30 caracteres")] string? Telefono,
    [MaxLength(256, ErrorMessage = "El email admite hasta 256 caracteres")] string? Email,
    [MaxLength(150, ErrorMessage = "El contacto admite hasta 150 caracteres")] string? Contacto,
    [Range(0, 100, ErrorMessage = "El IVA debe estar entre 0 y 100")] decimal IvaPorcentaje,
    bool Activo = true
);

public record CategoriaSurtidaDto(int Id, string Nombre);

/// <summary>
/// A supplier; empty text fields come as "". Compras, Productos and Categorias come from its purchases.
/// </summary>
public record ProveedorDto(
    int Id,
    string Nombre,
    string TipoDocumento,
    string Documento,
    string Direccion,
    string Ciudad,
    string Telefono,
    string Email,
    string Contacto,
    decimal IvaPorcentaje,
    bool Activo,
    int Compras,
    int Productos,
    CategoriaSurtidaDto[] Categorias
)
{
    public static ProveedorDto De(ProveedorConResumen x) =>
        new(
            x.Proveedor.Id,
            x.Proveedor.Nombre,
            x.Proveedor.TipoDocumento.Codigo,
            x.Proveedor.Documento,
            x.Proveedor.Direccion ?? "",
            x.Proveedor.Ciudad ?? "",
            x.Proveedor.Telefono ?? "",
            x.Proveedor.Email ?? "",
            x.Proveedor.Contacto ?? "",
            x.Proveedor.IvaPorcentaje,
            x.Proveedor.Activo,
            x.Compras,
            x.Productos,
            x.Categorias.Select(c => new CategoriaSurtidaDto(c.Id, c.Nombre)).ToArray()
        );
}
