using System.Globalization;
using MahoSoft.Datos.Archivos;
using MahoSoft.Datos.Repositorios;
using MahoSoft.Entidades;

namespace MahoSoft.Negocio.Compras;

/// <summary>An uploaded file as it arrives from the browser.</summary>
public record ArchivoEntrante(Stream Contenido, string Nombre, long Tamano);

public interface ICompraServicio
{
    Task<List<CompraDto>> ListarAsync(CancellationToken ct = default);

    Task<CompraDto> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Registers a supplier invoice: assigns its number, computes the totals, adds the units to stock through
    /// entrada movements and updates each product's cost. <paramref name="documento"/> is the optional PDF/photo.
    /// </summary>
    Task<CompraDto> RegistrarAsync(
        CompraRequest req,
        ArchivoEntrante? documento,
        int usuarioId,
        CancellationToken ct = default
    );

    Task<CompraDto> MarcarPagadaAsync(int id, CancellationToken ct = default);

    Task<DocumentoArchivo> ObtenerDocumentoAsync(int id, CancellationToken ct = default);
}

public class CompraServicio(
    ICompraRepositorio compras,
    IProveedorRepositorio proveedores,
    IProductoRepositorio productos,
    IConfiguracionRepositorio config,
    IAlmacenDocumentos documentos,
    IUnidadDeTrabajo unidad
) : ICompraServicio
{
    /// <summary>Colombia is UTC-5 all year; invoice dates and consecutive years are in local time.</summary>
    public static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    public const long DocumentoTamanoMaximo = 10 * 1024 * 1024;
    private const string NoExiste = "La compra no existe";

    public async Task<List<CompraDto>> ListarAsync(CancellationToken ct = default) =>
        (await compras.ListarAsync(ct)).Select(CompraDto.De).ToList();

    public async Task<CompraDto> ObtenerAsync(int id, CancellationToken ct = default) =>
        CompraDto.De(await compras.ObtenerAsync(id, ct) ?? throw new NoEncontradoException(NoExiste));

    public async Task<CompraDto> RegistrarAsync(
        CompraRequest req,
        ArchivoEntrante? documento,
        int usuarioId,
        CancellationToken ct = default
    )
    {
        var compra = await ArmarAsync(req, usuarioId, ct);
        var archivo = documento is null ? null : await LeerDocumentoAsync(documento, ct);

        // The file goes to disk first; if the database part fails, it is removed again
        string? ruta = null;
        if (archivo is not null)
        {
            ruta = await documentos.GuardarAsync(new MemoryStream(archivo.Value.Bytes), "facturas", archivo.Value.Extension, ct);
            compra.Documento = new Archivo
            {
                Id = Guid.NewGuid(),
                Nombre = Path.GetFileName(documento!.Nombre),
                TipoMime = archivo.Value.TipoMime,
                Tamano = archivo.Value.Bytes.Length,
                Almacen = AlmacenArchivo.Local,
                Ubicacion = ruta,
                SubidoEn = DateTimeOffset.UtcNow,
            };
        }

        try
        {
            await unidad.EnTransaccionAsync(
                async () =>
                {
                    var anio = DateTimeOffset.UtcNow.ToOffset(Colombia).Year;
                    compra.Numero = await compras.SiguienteNumeroAsync($"OC-{anio}-", ct);
                    await SumarAlInventarioAsync(compra, usuarioId, ct);
                    compras.Agregar(compra);
                    await unidad.GuardarCambiosAsync(ct);
                    return compra.Id;
                },
                ct
            );
        }
        catch
        {
            if (ruta is not null)
                documentos.Eliminar(ruta);
            throw;
        }
        return await ObtenerAsync(compra.Id, ct);
    }

    public async Task<CompraDto> MarcarPagadaAsync(int id, CancellationToken ct = default)
    {
        var compra = await compras.ObtenerParaEditarAsync(id, ct) ?? throw new NoEncontradoException(NoExiste);
        compra.EstadoPago = EstadoPago.Pagada;
        await unidad.GuardarCambiosAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    public async Task<DocumentoArchivo> ObtenerDocumentoAsync(int id, CancellationToken ct = default)
    {
        var compra = await compras.ObtenerAsync(id, ct) ?? throw new NoEncontradoException(NoExiste);
        if (compra.Documento is null)
            throw new NoEncontradoException("Esta compra no tiene documento adjunto");
        var contenido =
            documentos.Abrir(compra.Documento.Ubicacion)
            ?? throw new NoEncontradoException("El documento ya no está en el servidor");
        return new DocumentoArchivo(contenido, compra.Documento.Nombre, compra.Documento.TipoMime);
    }

    /// <summary>Validates the request and builds the purchase with its items and totals (no number yet).</summary>
    private async Task<Compra> ArmarAsync(CompraRequest req, int usuarioId, CancellationToken ct)
    {
        var proveedor =
            await proveedores.ObtenerPorIdAsync(req.ProveedorId, ct) ?? throw new ValidacionException("Elige un proveedor");
        if (!proveedor.Activo)
            throw new ValidacionException($"{proveedor.Nombre} está desactivado; actívalo en Proveedores para comprarle");

        var tipo =
            Texto(req.TipoComprobante, 60, "El tipo de comprobante")
            ?? throw new ValidacionException("Elige el tipo de comprobante");
        var numero =
            Texto(req.NumeroComprobante, 50, "El número de la factura")
            ?? throw new ValidacionException("Escribe el número de la factura del proveedor");
        if (await compras.ExisteComprobanteAsync(proveedor.Id, numero, ct))
            throw new ConflictoException($"La factura {numero} de {proveedor.Nombre} ya está registrada");

        var hora = TimeOnly.MinValue;
        string[] formatos = ["HH:mm", "HH:mm:ss"];
        if (
            !string.IsNullOrWhiteSpace(req.Hora)
            && !TimeOnly.TryParseExact(req.Hora.Trim(), formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out hora)
        )
            throw new ValidacionException("La hora debe tener el formato HH:mm");

        if (req.CondicionPago == CondicionPago.Credito && req.FechaVencimiento is null)
            throw new ValidacionException("Indica cuándo vence el crédito");
        if (req.IvaPorcentaje is < 0 or > 100)
            throw new ValidacionException("El IVA debe estar entre 0 y 100");
        if (req.Descuento < 0)
            throw new ValidacionException("El descuento no puede ser negativo");
        if (req.ValorComprobante < 0)
            throw new ValidacionException("El valor del comprobante no puede ser negativo");
        if (req.Items is not { Length: > 0 })
            throw new ValidacionException("Agrega al menos un producto");

        var tallas = (await config.ListarGruposTallaAsync(ct))
            .SelectMany(g => g.Tallas)
            .ToDictionary(t => t.Valor, StringComparer.OrdinalIgnoreCase);
        var items = new List<CompraItem>();
        foreach (var i in req.Items)
        {
            if (i.Cantidad <= 0)
                throw new ValidacionException("Las cantidades deben ser números enteros mayores a 0");
            if (i.PrecioUnitario < 0)
                throw new ValidacionException("El precio unitario no puede ser negativo");
            var producto =
                await productos.ObtenerParaEditarAsync(i.ProductoId, ct)
                ?? throw new ValidacionException("Uno de los productos ya no existe");
            if (!tallas.TryGetValue((i.Talla ?? "").Trim(), out var talla))
                throw new ValidacionException($"La talla {i.Talla} de {producto.Nombre} no está en Configuración → Tallas");
            items.Add(
                new CompraItem
                {
                    Producto = producto,
                    ProductoId = producto.Id,
                    Talla = talla,
                    ReferenciaProveedor = Texto(i.ReferenciaProveedor, 60, "La referencia del proveedor"),
                    Cantidad = i.Cantidad,
                    PrecioUnitario = i.PrecioUnitario,
                }
            );
        }

        var compra = new Compra
        {
            Proveedor = proveedor,
            TipoComprobante = tipo,
            NumeroComprobante = numero,
            FechaComprobante = new DateTimeOffset(req.Fecha.ToDateTime(hora), Colombia),
            VendedorProveedor = Texto(req.VendedorProveedor, 150, "El vendedor"),
            Cufe = Texto(req.Cufe, 200, "El CUFE / UUID"),
            CondicionPago = req.CondicionPago,
            FechaVencimiento = req.CondicionPago == CondicionPago.Credito ? req.FechaVencimiento : null,
            EstadoPago = req.EstadoPago,
            IvaPorcentaje = req.IvaPorcentaje,
            PreciosIncluyenIva = req.PreciosIncluyenIva && req.IvaPorcentaje > 0,
            Descuento = req.Descuento,
            ValorComprobante = req.ValorComprobante is > 0 ? req.ValorComprobante : null,
            Notas = Texto(req.Notas, 1000, "Las notas"),
            UsuarioId = usuarioId,
            CreadoEn = DateTimeOffset.UtcNow,
            Items = items,
        };
        CalcularTotales(compra);
        return compra;
    }

    /// <summary>
    /// Invoice totals, as the purchase form shows them: subtotal without IVA, discount off the subtotal, IVA on
    /// what is left. Each item's cost is its price without IVA less its share of the discount (IVA isn't cost).
    /// </summary>
    private static void CalcularTotales(Compra c)
    {
        var tasa = c.IvaPorcentaje / 100m;
        decimal SinIva(decimal precio) => c.PreciosIncluyenIva ? precio / (1 + tasa) : precio;

        var subtotal = c.Items.Sum(i => i.Cantidad * SinIva(i.PrecioUnitario));
        if (c.Descuento > subtotal)
            throw new ValidacionException("El descuento no puede ser mayor que el subtotal");
        var baseIva = subtotal - c.Descuento;
        var iva = baseIva * tasa;
        var proporcion = subtotal > 0 ? 1 - c.Descuento / subtotal : 1;

        c.Subtotal = Pesos(subtotal);
        c.Descuento = Pesos(c.Descuento);
        c.Iva = Pesos(iva);
        c.Total = Pesos(baseIva + iva);
        foreach (var i in c.Items)
            i.CostoUnitario = Pesos(SinIva(i.PrecioUnitario) * proporcion);
    }

    /// <summary>Adds each item's units to stock (entrada movement) and updates the product's cost.</summary>
    private async Task SumarAlInventarioAsync(Compra compra, int usuarioId, CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;
        foreach (var item in compra.Items)
        {
            var producto = item.Producto;
            var existencia = producto.Tallas.SingleOrDefault(t => t.TallaId == item.Talla.Id);
            if (existencia is null)
            {
                // The product didn't come in this size yet: now it does
                existencia = new ProductoTalla { Talla = item.Talla };
                producto.Tallas.Add(existencia);
            }
            existencia.Stock += item.Cantidad;
            productos.AgregarMovimiento(
                new MovimientoInventario
                {
                    Fecha = ahora,
                    Tipo = TipoMovimiento.Entrada,
                    Producto = producto,
                    Talla = item.Talla,
                    Cantidad = item.Cantidad,
                    Motivo = $"Compra {compra.Numero}",
                    Compra = compra,
                    UsuarioId = usuarioId,
                }
            );
        }

        // The cost is the one from the most recent invoice: an older invoice typed late doesn't overwrite it
        foreach (var grupo in compra.Items.GroupBy(i => i.ProductoId))
        {
            if (!await compras.HayCompraPosteriorAsync(grupo.Key, compra.FechaComprobante, ct))
                grupo.Last().Producto.CostoActual = grupo.Last().CostoUnitario;
        }
    }

    /// <summary>Reads the upload and checks, by its content, that it is a PDF or an image of at most 10 MB.</summary>
    private static async Task<(byte[] Bytes, string TipoMime, string Extension)?> LeerDocumentoAsync(
        ArchivoEntrante documento,
        CancellationToken ct
    )
    {
        if (documento.Tamano == 0)
            throw new ValidacionException("El documento está vacío");
        if (documento.Tamano > DocumentoTamanoMaximo)
            throw new ValidacionException("El documento no puede pesar más de 10 MB");

        using var memoria = new MemoryStream();
        await documento.Contenido.CopyToAsync(memoria, ct);
        var bytes = memoria.ToArray();
        var tipo =
            DetectarTipo(bytes)
            ?? throw new ValidacionException("El documento debe ser un PDF o una imagen (JPG, PNG o WebP)");
        return (bytes, tipo.Mime, tipo.Extension);
    }

    private static (string Mime, string Extension)? DetectarTipo(byte[] b)
    {
        bool Empieza(params byte[] firma) => b.Length >= firma.Length && b.AsSpan(0, firma.Length).SequenceEqual(firma);

        if (Empieza("%PDF-"u8.ToArray()))
            return ("application/pdf", ".pdf");
        if (Empieza(0xFF, 0xD8, 0xFF))
            return ("image/jpeg", ".jpg");
        if (Empieza(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A))
            return ("image/png", ".png");
        if (b.Length >= 12 && Empieza("RIFF"u8.ToArray()) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return ("image/webp", ".webp");
        return null;
    }

    private static decimal Pesos(decimal valor) => Math.Round(valor, 0, MidpointRounding.AwayFromZero);

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
