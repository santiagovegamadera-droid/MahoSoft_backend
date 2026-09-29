using System.ComponentModel.DataAnnotations;
using MahoSoft.Datos.Repositorios;

namespace MahoSoft.Negocio.Categorias;

public record CategoriaRequest(
    [Required(ErrorMessage = "El nombre es obligatorio"), MaxLength(100, ErrorMessage = "El nombre admite hasta 100 caracteres")]
        string Nombre,
    [MaxLength(500, ErrorMessage = "La descripción admite hasta 500 caracteres")] string? Descripcion,
    bool Activo = true
);

public record CategoriaDto(int Id, string Nombre, string? Descripcion, bool Activo, int Productos, int ProductosActivos)
{
    public static CategoriaDto De(CategoriaConConteo x) =>
        new(
            x.Categoria.Id,
            x.Categoria.Nombre,
            x.Categoria.Descripcion,
            x.Categoria.Activo,
            x.Productos,
            x.ProductosActivos
        );
}
