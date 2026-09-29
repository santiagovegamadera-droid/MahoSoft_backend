using System.Net.Mail;
using MahoSoft.Datos.Archivos;
using MahoSoft.Datos.Repositorios;
using MahoSoft.Entidades;
using MahoSoft.Negocio.Compras;

namespace MahoSoft.Negocio.Ventas;

public interface IVentaServicio
{
    Task<List<VentaDto>> ListarAsync(CancellationToken ct = default);

    Task<VentaDto> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Registers a sale: assigns its invoice number, takes its units out of stock (salida movements) and keeps each
    /// product's price and cost at that moment. <paramref name="comprobante"/> is the transfer receipt file, if any.
    /// </summary>
    Task<VentaDto> RegistrarAsync(
        VentaRequest req,
        ArchivoEntrante? comprobante,
        int vendedorId,
        CancellationToken ct = default
    );

    /// <summary>Voids the sale (it stays in the history) and returns its units to stock.</summary>
    Task<VentaDto> AnularAsync(int id, AnularVentaRequest req, int usuarioId, CancellationToken ct = default);

    Task<DocumentoArchivo> ObtenerComprobanteAsync(int id, CancellationToken ct = default);
}

public class VentaServicio(
    IVentaRepositorio ventas,
    IClienteRepositorio clientes,
    IProductoRepositorio productos,
    IConfiguracionRepositorio config,
    IAlmacenDocumentos documentos,
    IUnidadDeTrabajo unidad
) : IVentaServicio
{
    private const string NoExiste = "La venta no existe";

    public async Task<List<VentaDto>> ListarAsync(CancellationToken ct = default) =>
        (await ventas.ListarAsync(ct)).Select(VentaDto.De).ToList();

    public async Task<VentaDto> ObtenerAsync(int id, CancellationToken ct = default) =>
        VentaDto.De(await ventas.ObtenerAsync(id, ct) ?? throw new NoEncontradoException(NoExiste));

    public async Task<VentaDto> RegistrarAsync(
        VentaRequest req,
        ArchivoEntrante? comprobante,
        int vendedorId,
        CancellationToken ct = default
    )
    {
        var venta = await ArmarAsync(req, vendedorId, ct);
        var archivo = comprobante is null ? null : await Documentos.LeerAsync(comprobante, "El comprobante", ct);

        // The file goes to disk first; if the database part fails, it is removed again
        string? ruta = null;
        if (archivo is not null && venta.Comprobante is not null)
        {
            ruta = await documentos.GuardarAsync(new MemoryStream(archivo.Bytes), "comprobantes", archivo.Extension, ct);
            venta.Comprobante.Archivo = new Archivo
            {
                Id = Guid.NewGuid(),
                Nombre = Path.GetFileName(comprobante!.Nombre),
                TipoMime = archivo.TipoMime,
                Tamano = archivo.Bytes.Length,
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
                    var anio = DateTimeOffset.UtcNow.ToOffset(CompraServicio.Colombia).Year;
                    venta.NumeroFactura = await ventas.SiguienteNumeroAsync($"VTA-{anio}-", ct);
                    venta.Cliente = await ClienteAsync(req, ct);
                    await DescontarDelInventarioAsync(venta, vendedorId, ct);
                    ventas.Agregar(venta);
                    await unidad.GuardarCambiosAsync(ct);
                    return venta.Id;
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
        return await ObtenerAsync(venta.Id, ct);
    }

    public async Task<VentaDto> AnularAsync(
        int id,
        AnularVentaRequest req,
        int usuarioId,
        CancellationToken ct = default
    )
    {
        var motivo = Texto(req.Motivo, 300, "El motivo") ?? throw new ValidacionException("Escribe el motivo de la anulación");
        var venta = await ventas.ObtenerParaEditarAsync(id, ct) ?? throw new NoEncontradoException(NoExiste);
        if (venta.Estado == EstadoVenta.Anulada)
            throw new ConflictoException($"La venta {venta.NumeroFactura} ya está anulada");

        await unidad.EnTransaccionAsync(
            async () =>
            {
                var ahora = DateTimeOffset.UtcNow;
                foreach (var item in venta.Items)
                {
                    if (!await productos.CambiarStockAsync(item.ProductoId, item.TallaId, item.Cantidad, ct))
                    {
                        // The size was removed from the product after the sale (it had no units): bring it back
                        var producto = await productos.ObtenerParaEditarAsync(item.ProductoId, ct);
                        producto!.Tallas.Add(new ProductoTalla { TallaId = item.TallaId, Stock = item.Cantidad });
                    }
                    productos.AgregarMovimiento(
                        new MovimientoInventario
                        {
                            Fecha = ahora,
                            Tipo = TipoMovimiento.Entrada,
                            ProductoId = item.ProductoId,
                            TallaId = item.TallaId,
                            Cantidad = item.Cantidad,
                            Motivo = $"Anulación {venta.NumeroFactura}",
                            VentaId = venta.Id,
                            UsuarioId = usuarioId,
                        }
                    );
                }
                venta.Estado = EstadoVenta.Anulada;
                venta.AnuladaEn = ahora;
                venta.AnuladaPorId = usuarioId;
                venta.MotivoAnulacion = motivo;
                await unidad.GuardarCambiosAsync(ct);
                return venta.Id;
            },
            ct
        );
        return await ObtenerAsync(id, ct);
    }

    public async Task<DocumentoArchivo> ObtenerComprobanteAsync(int id, CancellationToken ct = default)
    {
        var venta = await ventas.ObtenerAsync(id, ct) ?? throw new NoEncontradoException(NoExiste);
        var archivo =
            venta.Comprobante?.Archivo ?? throw new NoEncontradoException("Esta venta no tiene comprobante adjunto");
        var contenido =
            documentos.Abrir(archivo.Ubicacion) ?? throw new NoEncontradoException("El comprobante ya no está en el servidor");
        return new DocumentoArchivo(contenido, archivo.Nombre, archivo.TipoMime);
    }

    /// <summary>Validates the request and builds the sale with its items and totals (no number, customer or stock yet).</summary>
    private async Task<Venta> ArmarAsync(VentaRequest req, int vendedorId, CancellationToken ct)
    {
        if (req.Items is not { Length: > 0 })
            throw new ValidacionException("Agrega al menos un producto");

        var descuentos = (await config.ListarDescuentosAsync(ct)).Select(d => d.Porcentaje).ToHashSet();
        if (req.DescuentoPorcentaje != 0 && !descuentos.Contains(req.DescuentoPorcentaje))
            throw new ValidacionException("Elige uno de los descuentos del punto de venta");

        var esPedido = req.Tipo == TipoVenta.Pedido;
        if (esPedido)
        {
            if (string.IsNullOrWhiteSpace(req.Cliente?.Nombre) || string.IsNullOrWhiteSpace(req.Cliente?.Telefono))
                throw new ValidacionException("El pedido necesita el nombre y el teléfono de quien lo recibe");
            if (string.IsNullOrWhiteSpace(req.Entrega?.Direccion))
                throw new ValidacionException("El pedido necesita la dirección de entrega");
            if (req.Entrega.Envio < 0)
                throw new ValidacionException("El costo de envío no puede ser negativo");
        }

        var tallas = (await config.ListarGruposTallaAsync(ct))
            .SelectMany(g => g.Tallas)
            .ToDictionary(t => t.Valor, StringComparer.OrdinalIgnoreCase);
        var items = new List<VentaItem>();
        foreach (var i in req.Items)
        {
            if (i.Cantidad <= 0)
                throw new ValidacionException("Las cantidades deben ser números enteros mayores a 0");
            var producto =
                await productos.ObtenerParaEditarAsync(i.ProductoId, ct)
                ?? throw new ValidacionException("Uno de los productos ya no existe");
            if (!producto.Activo)
                throw new ValidacionException($"{producto.Nombre} está desactivado y no se puede vender");
            if (!tallas.TryGetValue((i.Talla ?? "").Trim(), out var talla) || producto.Tallas.All(t => t.TallaId != talla.Id))
                throw new ValidacionException($"{producto.Nombre} no viene en talla {i.Talla}");

            // Price and cost as they are now: the receipt and the margin don't change later
            items.Add(
                new VentaItem
                {
                    ProductoId = producto.Id,
                    Talla = talla,
                    NombreProducto = producto.Nombre,
                    Cantidad = i.Cantidad,
                    PrecioUnitario = producto.PrecioVenta,
                    CostoUnitario = producto.CostoActual,
                }
            );
        }

        var subtotal = items.Sum(i => i.Cantidad * i.PrecioUnitario);
        var descuento = Pesos(subtotal * req.DescuentoPorcentaje / 100);
        var envio = esPedido ? Pesos(req.Entrega!.Envio) : 0;
        var venta = new Venta
        {
            Fecha = DateTimeOffset.UtcNow,
            Tipo = req.Tipo,
            VendedorId = vendedorId,
            MetodoPago = req.MetodoPago,
            DescuentoPorcentaje = req.DescuentoPorcentaje,
            Subtotal = subtotal,
            Descuento = descuento,
            Envio = envio,
            Total = subtotal - descuento + envio,
            Estado = EstadoVenta.Registrada,
            Items = items,
        };

        if (esPedido)
            venta.Entrega = new VentaEntrega
            {
                Direccion = Texto(req.Entrega!.Direccion, 250, "La dirección")!,
                Barrio = Texto(req.Entrega.Barrio, 100, "El barrio"),
                Ciudad = Texto(req.Entrega.Ciudad, 100, "La ciudad"),
                FechaEntrega = req.Entrega.FechaEntrega,
                Notas = Texto(req.Entrega.Notas, 500, "Las notas de entrega"),
            };

        if (req.MetodoPago == MetodoPago.Transferencia)
        {
            Banco? banco = null;
            if (!string.IsNullOrWhiteSpace(req.Comprobante?.Banco))
                banco =
                    (await config.ListarBancosAsync(ct)).FirstOrDefault(b =>
                        b.Activo && string.Equals(b.Nombre, req.Comprobante.Banco.Trim(), StringComparison.OrdinalIgnoreCase)
                    ) ?? throw new ValidacionException("Elige un banco o billetera de la lista");
            venta.Comprobante = new ComprobanteTransferencia
            {
                Banco = banco,
                Referencia = Texto(req.Comprobante?.Referencia, 100, "La referencia"),
            };
        }
        return venta;
    }

    /// <summary>
    /// Takes each line's units out of stock in the database itself (Stock = Stock - n, never below 0), so two tills
    /// selling the last unit at once can't both succeed.
    /// </summary>
    private async Task DescontarDelInventarioAsync(Venta venta, int vendedorId, CancellationToken ct)
    {
        foreach (var linea in venta.Items.GroupBy(i => (i.ProductoId, i.Talla.Id)))
        {
            var cantidad = linea.Sum(i => i.Cantidad);
            var item = linea.First();
            if (!await productos.CambiarStockAsync(item.ProductoId, item.Talla.Id, -cantidad, ct))
            {
                var quedan = await productos.StockAsync(item.ProductoId, item.Talla.Id, ct);
                throw new ConflictoException(
                    $"Solo quedan {quedan} {(quedan == 1 ? "unidad" : "unidades")} de {item.NombreProducto} talla {item.Talla.Valor}"
                );
            }
        }

        foreach (var item in venta.Items)
            productos.AgregarMovimiento(
                new MovimientoInventario
                {
                    Fecha = venta.Fecha,
                    Tipo = TipoMovimiento.Salida,
                    ProductoId = item.ProductoId,
                    Talla = item.Talla,
                    Cantidad = -item.Cantidad,
                    Motivo = $"Venta {venta.NumeroFactura}",
                    Venta = venta,
                    UsuarioId = vendedorId,
                }
            );
    }

    /// <summary>
    /// The sale's customer: found by document, or else by phone, and updated with what was typed; created when new.
    /// Null when nothing was typed (Cliente general).
    /// </summary>
    private async Task<Cliente?> ClienteAsync(VentaRequest req, CancellationToken ct)
    {
        var c = req.Cliente;
        var nombre = Texto(c?.Nombre, 150, "El nombre del cliente");
        var documento = Texto(c?.Documento, 30, "El documento del cliente");
        var telefono = Texto(c?.Telefono, 30, "El teléfono del cliente");
        var correo = Texto(c?.Correo, 256, "El correo del cliente");
        if (nombre is null && documento is null && telefono is null && correo is null)
            return null;
        if (correo is not null && !MailAddress.TryCreate(correo, out _))
            throw new ValidacionException("El correo del cliente no es válido");

        TipoDocumento? tipo = null;
        if (documento is not null)
        {
            tipo =
                await config.ObtenerTipoDocumentoAsync((c!.TipoDocumento ?? "").Trim(), ct)
                ?? throw new ValidacionException("Elige el tipo de documento del cliente");
        }

        Cliente? cliente = null;
        if (tipo is not null)
            cliente = await clientes.ObtenerPorDocumentoAsync(tipo.Id, documento!, ct);
        if (cliente is null && telefono is not null)
        {
            // A customer known only by phone gets the document now; one with another document is someone else
            var porTelefono = await clientes.ObtenerPorTelefonoAsync(telefono, ct);
            if (porTelefono is not null && (documento is null || porTelefono.Documento is null))
                cliente = porTelefono;
        }
        if (cliente is null)
        {
            cliente = new Cliente
            {
                Nombre = nombre ?? throw new ValidacionException("Escribe el nombre del cliente"),
                CreadoEn = DateTimeOffset.UtcNow,
            };
            clientes.Agregar(cliente);
        }

        if (nombre is not null)
            cliente.Nombre = nombre;
        if (tipo is not null)
        {
            cliente.TipoDocumento = tipo;
            cliente.Documento = documento;
        }
        if (telefono is not null)
            cliente.Telefono = telefono;
        if (correo is not null)
            cliente.Correo = correo;
        return cliente;
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
