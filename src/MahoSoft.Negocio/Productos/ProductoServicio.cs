using MahoSoft.Datos.Archivos;
using MahoSoft.Datos.Repositorios;
using MahoSoft.Entidades;

namespace MahoSoft.Negocio.Productos;

public interface IProductoServicio
{
    Task<List<ProductoDto>> ListarAsync(CancellationToken ct = default);

    Task<ProductoDto> ObtenerAsync(int id, CancellationToken ct = default);

    Task<ProductoDto> CrearAsync(ProductoRequest req, int usuarioId, CancellationToken ct = default);

    Task<ProductoDto> ActualizarAsync(int id, ProductoRequest req, int usuarioId, CancellationToken ct = default);

    /// <summary>Only a product never bought nor sold can be deleted; otherwise it should be deactivated.</summary>
    Task EliminarAsync(int id, CancellationToken ct = default);

    /// <summary>Uploads a product photo; the product points to it when saved with <see cref="ProductoRequest.ImagenId"/>.</summary>
    Task<ImagenDto> SubirImagenAsync(
        Stream contenido,
        string nombre,
        string tipoMime,
        long tamano,
        CancellationToken ct = default
    );
}

public class ProductoServicio(
    IProductoRepositorio productos,
    ICategoriaRepositorio categorias,
    IConfiguracionRepositorio config,
    IAlmacenImagenes imagenes,
    IUnidadDeTrabajo unidad
) : IProductoServicio
{
    private const string NoExiste = "El producto no existe";
    public const long ImagenTamanoMaximo = 5 * 1024 * 1024;
    private static readonly string[] TiposImagen = ["image/jpeg", "image/png", "image/webp"];

    public async Task<List<ProductoDto>> ListarAsync(CancellationToken ct = default) =>
        (await productos.ListarAsync(ct)).Select(ProductoDto.De).ToList();

    public async Task<ProductoDto> ObtenerAsync(int id, CancellationToken ct = default) =>
        ProductoDto.De(await productos.ObtenerAsync(id, ct) ?? throw new NoEncontradoException(NoExiste));

    public async Task<ProductoDto> CrearAsync(ProductoRequest req, int usuarioId, CancellationToken ct = default)
    {
        var producto = new Producto { CreadoEn = DateTimeOffset.UtcNow };
        await AplicarAsync(producto, req, usuarioId, ct);
        productos.Agregar(producto);
        await unidad.GuardarCambiosAsync(ct);
        return await ObtenerAsync(producto.Id, ct);
    }

    public async Task<ProductoDto> ActualizarAsync(
        int id,
        ProductoRequest req,
        int usuarioId,
        CancellationToken ct = default
    )
    {
        var producto = await productos.ObtenerParaEditarAsync(id, ct) ?? throw new NoEncontradoException(NoExiste);
        var imagenAnterior = producto.Imagen;
        // Stock changes run in the database right away, so they commit together with the rest or not at all
        await unidad.EnTransaccionAsync(
            async () =>
            {
                await AplicarAsync(producto, req, usuarioId, ct);
                await unidad.GuardarCambiosAsync(ct);
                return producto.Id;
            },
            ct
        );

        if (imagenAnterior is not null && imagenAnterior.Id != producto.ImagenId)
            await EliminarImagenAsync(imagenAnterior, id, ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var producto = await productos.ObtenerParaEditarAsync(id, ct) ?? throw new NoEncontradoException(NoExiste);
        if (await productos.TieneComprasOVentasAsync(id, ct))
            throw new ConflictoException(
                $"{producto.Nombre} ya tiene compras o ventas, así que no se puede eliminar. Desactívalo para que no se venda más."
            );

        // Without purchases or sales, its only movements are the adjustments made from this screen
        await productos.EliminarMovimientosAsync(id, ct);
        var imagen = producto.Imagen;
        productos.Eliminar(producto);
        await unidad.GuardarCambiosAsync(ct);
        if (imagen is not null)
            await EliminarImagenAsync(imagen, id, ct);
    }

    public async Task<ImagenDto> SubirImagenAsync(
        Stream contenido,
        string nombre,
        string tipoMime,
        long tamano,
        CancellationToken ct = default
    )
    {
        if (!TiposImagen.Contains(tipoMime))
            throw new ValidacionException("La imagen debe ser JPG, PNG o WebP");
        if (tamano == 0)
            throw new ValidacionException("La imagen está vacía");
        if (tamano > ImagenTamanoMaximo)
            throw new ValidacionException("La imagen pesa más de 5 MB");

        ImagenSubida subida;
        try
        {
            subida = await imagenes.SubirAsync(contenido, nombre, ct);
        }
        catch (InvalidOperationException)
        {
            throw new ValidacionException("No se pudo subir la imagen. Revisa que sea una foto válida e inténtalo de nuevo.");
        }

        var archivo = new Archivo
        {
            Id = Guid.NewGuid(),
            Nombre = Path.GetFileName(nombre),
            TipoMime = tipoMime,
            Tamano = subida.Tamano,
            Almacen = AlmacenArchivo.Cloudinary,
            Ubicacion = subida.Url,
            PublicId = subida.PublicId,
            SubidoEn = DateTimeOffset.UtcNow,
        };
        productos.AgregarArchivo(archivo);
        await unidad.GuardarCambiosAsync(ct);
        return new ImagenDto(archivo.Id, archivo.Ubicacion);
    }

    private async Task AplicarAsync(Producto producto, ProductoRequest req, int usuarioId, CancellationToken ct)
    {
        var nombre = req.Nombre.Trim();
        if (nombre.Length == 0)
            throw new ValidacionException("El nombre es obligatorio");

        // A deactivated category can't be chosen anew, but a product already in it keeps it
        var categoria =
            await categorias.ObtenerPorIdAsync(req.CategoriaId, ct)
            ?? throw new ValidacionException("Elige una categoría");
        if (!categoria.Activo && categoria.Id != producto.CategoriaId)
            throw new ValidacionException($"La categoría {categoria.Nombre} está desactivada");

        // A new photo must be one uploaded through SubirImagenAsync; set the navigation (not only the id) so
        // EF doesn't keep pointing at the previous one
        if (req.ImagenId != producto.ImagenId)
        {
            Archivo? imagen = null;
            if (req.ImagenId is Guid imagenId)
            {
                imagen = await productos.ObtenerArchivoAsync(imagenId, ct);
                if (imagen is null || imagen.Almacen != AlmacenArchivo.Cloudinary)
                    throw new ValidacionException("La imagen no existe; vuelve a subirla");
            }
            producto.Imagen = imagen;
            producto.ImagenId = imagen?.Id;
        }

        producto.Nombre = nombre;
        producto.Categoria = categoria;
        producto.PrecioVenta = req.PrecioVenta;
        producto.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        producto.Activo = req.Activo;
        AplicarColores(producto, req.Colores ?? []);
        await AplicarStockAsync(producto, req.Stock ?? [], usuarioId, ct);
    }

    private static void AplicarColores(Producto producto, string[] colores)
    {
        var lista = colores.Select(c => (c ?? "").Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (lista.Any(c => c.Length > 50))
            throw new ValidacionException("Cada color admite hasta 50 caracteres");

        producto.Colores.RemoveAll(c => !lista.Contains(c.Color, StringComparer.OrdinalIgnoreCase));
        foreach (var color in lista.Where(c => !producto.Colores.Any(x => string.Equals(x.Color, c, StringComparison.OrdinalIgnoreCase))))
            producto.Colores.Add(new ProductoColor { Color = color });
    }

    /// <summary>
    /// Makes the product's sizes and units match <paramref name="stock"/>. Each change of units is stored as an
    /// adjustment movement, so the stock can always be explained; a size with units can't be dropped. On sizes
    /// already saved the difference is added in the database itself (Stock = Stock + n), so a sale made while the
    /// form was open isn't overwritten: the adjustment is the change the user made.
    /// </summary>
    private async Task AplicarStockAsync(
        Producto producto,
        Dictionary<string, int> stock,
        int usuarioId,
        CancellationToken ct
    )
    {
        var tallas = (await config.ListarGruposTallaAsync(ct))
            .SelectMany(g => g.Tallas)
            .ToDictionary(t => t.Valor, StringComparer.OrdinalIgnoreCase);

        var pedidas = new Dictionary<int, (Talla Talla, int Unidades)>();
        foreach (var (valor, unidades) in stock)
        {
            if (!tallas.TryGetValue(valor.Trim(), out var talla))
                throw new ValidacionException($"La talla {valor} no está en Configuración → Tallas");
            if (unidades < 0)
                throw new ValidacionException($"El stock de la talla {talla.Valor} no puede ser negativo");
            pedidas[talla.Id] = (talla, unidades);
        }

        var nuevo = producto.Id == 0;
        foreach (var actual in producto.Tallas.Where(t => !pedidas.ContainsKey(t.TallaId)).ToList())
        {
            if (actual.Stock > 0)
                throw new ConflictoException(
                    $"La talla {actual.Talla.Valor} tiene {actual.Stock} unidades. Pon su stock en 0 antes de quitarla."
                );
            producto.Tallas.Remove(actual);
        }

        var ahora = DateTimeOffset.UtcNow;
        foreach (var (tallaId, (talla, unidades)) in pedidas)
        {
            var actual = producto.Tallas.SingleOrDefault(t => t.TallaId == tallaId);
            var diferencia = unidades - (actual?.Stock ?? 0);
            if (actual is null)
            {
                producto.Tallas.Add(new ProductoTalla { Talla = talla, Stock = unidades });
            }
            else if (diferencia != 0 && !await productos.CambiarStockAsync(producto.Id, tallaId, diferencia, ct))
            {
                var quedan = await productos.StockAsync(producto.Id, tallaId, ct);
                throw new ConflictoException(
                    $"El stock de la talla {talla.Valor} cambió mientras editabas (ahora hay {quedan}). Vuelve a abrir el producto."
                );
            }
            if (diferencia == 0)
                continue;

            productos.AgregarMovimiento(
                new MovimientoInventario
                {
                    Fecha = ahora,
                    Tipo = TipoMovimiento.Ajuste,
                    Producto = producto,
                    Talla = talla,
                    Cantidad = diferencia,
                    Motivo = nuevo ? "Stock inicial" : "Ajuste desde Productos",
                    UsuarioId = usuarioId,
                }
            );
        }
    }

    private async Task EliminarImagenAsync(Archivo imagen, int productoId, CancellationToken ct)
    {
        // Sample images are external links, and another product might show the same photo
        if (imagen.Almacen != AlmacenArchivo.Cloudinary || await productos.ImagenEnUsoAsync(imagen.Id, productoId, ct))
            return;
        productos.EliminarArchivo(imagen);
        await unidad.GuardarCambiosAsync(ct);
        if (imagen.PublicId is not null)
            await imagenes.EliminarAsync(imagen.PublicId);
    }
}
