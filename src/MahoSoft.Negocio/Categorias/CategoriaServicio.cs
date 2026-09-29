using MahoSoft.Datos.Repositorios;
using MahoSoft.Entidades;

namespace MahoSoft.Negocio.Categorias;

public interface ICategoriaServicio
{
    Task<List<CategoriaDto>> ListarAsync(CancellationToken ct = default);

    Task<CategoriaDto> ObtenerAsync(int id, CancellationToken ct = default);

    Task<CategoriaDto> CrearAsync(CategoriaRequest req, CancellationToken ct = default);

    /// <summary>Also activates or deactivates it through <see cref="CategoriaRequest.Activo"/>.</summary>
    Task<CategoriaDto> ActualizarAsync(int id, CategoriaRequest req, CancellationToken ct = default);

    /// <summary>Only a category without products can be deleted; otherwise it should be deactivated.</summary>
    Task EliminarAsync(int id, CancellationToken ct = default);
}

public class CategoriaServicio(ICategoriaRepositorio categorias, IUnidadDeTrabajo unidad) : ICategoriaServicio
{
    private const string NoExiste = "La categoría no existe";

    public async Task<List<CategoriaDto>> ListarAsync(CancellationToken ct = default) =>
        (await categorias.ListarConConteoAsync(ct)).Select(CategoriaDto.De).ToList();

    public async Task<CategoriaDto> ObtenerAsync(int id, CancellationToken ct = default) =>
        CategoriaDto.De(await categorias.ObtenerConConteoAsync(id, ct) ?? throw new NoEncontradoException(NoExiste));

    public async Task<CategoriaDto> CrearAsync(CategoriaRequest req, CancellationToken ct = default)
    {
        var categoria = new Categoria();
        await AplicarAsync(categoria, req, ct);
        categorias.Agregar(categoria);
        await unidad.GuardarCambiosAsync(ct);
        return await ObtenerAsync(categoria.Id, ct);
    }

    public async Task<CategoriaDto> ActualizarAsync(int id, CategoriaRequest req, CancellationToken ct = default)
    {
        var categoria = await categorias.ObtenerPorIdAsync(id, ct) ?? throw new NoEncontradoException(NoExiste);
        await AplicarAsync(categoria, req, ct);
        await unidad.GuardarCambiosAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var categoria = await categorias.ObtenerPorIdAsync(id, ct) ?? throw new NoEncontradoException(NoExiste);
        if (await categorias.TieneProductosAsync(id, ct))
            throw new ConflictoException(
                $"La categoría {categoria.Nombre} tiene productos. Muévelos a otra categoría o desactívala."
            );

        categorias.Eliminar(categoria);
        await unidad.GuardarCambiosAsync(ct);
    }

    private async Task AplicarAsync(Categoria categoria, CategoriaRequest req, CancellationToken ct)
    {
        var nombre = req.Nombre.Trim();
        if (nombre.Length == 0)
            throw new ValidacionException("El nombre es obligatorio");
        if (await categorias.ExisteNombreAsync(nombre, categoria.Id == 0 ? null : categoria.Id, ct))
            throw new ConflictoException($"Ya existe una categoría llamada {nombre}");

        categoria.Nombre = nombre;
        categoria.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        categoria.Activo = req.Activo;
    }
}
