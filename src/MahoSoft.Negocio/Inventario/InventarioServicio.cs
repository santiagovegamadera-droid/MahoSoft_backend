using MahoSoft.Datos.Repositorios;
using MahoSoft.Entidades;

namespace MahoSoft.Negocio.Inventario;

/// <summary>Why the stock is changed by hand. Each reason decides the movement type and the sign.</summary>
public enum MotivoAjuste
{
    /// <summary>Physical count: the size ends with <see cref="AjusteRequest.Cantidad"/> units (adjustment).</summary>
    Conteo,

    /// <summary>Damaged or lost garments: takes units out (salida).</summary>
    Danado,

    /// <summary>Garments sent back to the supplier: takes units out (salida).</summary>
    DevolucionProveedor,

    /// <summary>Garments that come in without a purchase (a gift, a customer return outside a voided sale): adds units.</summary>
    Ingreso,
}

/// <summary>
/// A stock change made by hand. For <see cref="MotivoAjuste.Conteo"/> Cantidad is the units counted; for the other
/// reasons it is how many units go out or come in.
/// </summary>
public record AjusteRequest(int ProductoId, string Talla, MotivoAjuste Motivo, int Cantidad, string? Nota);

public record MovimientoDto(
    int Id,
    DateTimeOffset Fecha,
    TipoMovimiento Tipo,
    int ProductoId,
    string Producto,
    string Talla,
    int Cantidad,
    string Motivo,
    string Usuario,
    string? Compra,
    string? Venta
)
{
    public static MovimientoDto De(MovimientoDetalle m) =>
        new(m.Id, m.Fecha, m.Tipo, m.ProductoId, m.Producto, m.Talla, m.Cantidad, m.Motivo ?? "", m.Usuario, m.Compra, m.Venta);
}

public interface IInventarioServicio
{
    Task<List<MovimientoDto>> MovimientosAsync(int? productoId, TipoMovimiento? tipo, CancellationToken ct = default);

    /// <summary>Records a stock change by hand as a movement and applies it to the size's stock.</summary>
    Task<MovimientoDto> AjustarAsync(AjusteRequest req, int usuarioId, CancellationToken ct = default);
}

public class InventarioServicio(
    IInventarioRepositorio inventario,
    IProductoRepositorio productos,
    IUnidadDeTrabajo unidad
) : IInventarioServicio
{
    private const int Limite = 300;

    private static readonly Dictionary<MotivoAjuste, string> Nombres = new()
    {
        [MotivoAjuste.Conteo] = "Conteo físico",
        [MotivoAjuste.Danado] = "Prenda dañada o perdida",
        [MotivoAjuste.DevolucionProveedor] = "Devolución a proveedor",
        [MotivoAjuste.Ingreso] = "Ingreso sin compra",
    };

    public async Task<List<MovimientoDto>> MovimientosAsync(
        int? productoId,
        TipoMovimiento? tipo,
        CancellationToken ct = default
    ) => (await inventario.ListarAsync(productoId, tipo, Limite, ct)).Select(MovimientoDto.De).ToList();

    public async Task<MovimientoDto> AjustarAsync(AjusteRequest req, int usuarioId, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(req.Motivo))
            throw new ValidacionException("Elige el motivo del ajuste");
        if (req.Motivo == MotivoAjuste.Conteo ? req.Cantidad < 0 : req.Cantidad <= 0)
            throw new ValidacionException(
                req.Motivo == MotivoAjuste.Conteo
                    ? "Las unidades contadas no pueden ser negativas"
                    : "La cantidad debe ser un número entero mayor a 0"
            );
        var nota = string.IsNullOrWhiteSpace(req.Nota) ? null : req.Nota.Trim();
        if (nota?.Length > 150)
            throw new ValidacionException("La nota admite hasta 150 caracteres");

        var producto =
            await productos.ObtenerParaEditarAsync(req.ProductoId, ct) ?? throw new ValidacionException("El producto no existe");
        var talla =
            producto.Tallas.FirstOrDefault(t => string.Equals(t.Talla.Valor, (req.Talla ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidacionException($"{producto.Nombre} no viene en talla {req.Talla}");

        var movimiento = await unidad.EnTransaccionAsync(
            async () =>
            {
                // Read inside the transaction so a count is measured against the stock right now
                var actual = await productos.StockAsync(producto.Id, talla.TallaId, ct);
                var cambio = req.Motivo switch
                {
                    MotivoAjuste.Conteo => req.Cantidad - actual,
                    MotivoAjuste.Ingreso => req.Cantidad,
                    _ => -req.Cantidad,
                };
                if (cambio == 0)
                    throw new ValidacionException($"El conteo coincide con el stock ({actual}): no hay nada que ajustar");
                if (!await productos.CambiarStockAsync(producto.Id, talla.TallaId, cambio, ct))
                    throw new ConflictoException(
                        $"Solo hay {actual} {(actual == 1 ? "unidad" : "unidades")} de {producto.Nombre} talla {talla.Talla.Valor}"
                    );

                var m = new MovimientoInventario
                {
                    Fecha = DateTimeOffset.UtcNow,
                    Tipo = req.Motivo switch
                    {
                        MotivoAjuste.Conteo => TipoMovimiento.Ajuste,
                        MotivoAjuste.Ingreso => TipoMovimiento.Entrada,
                        _ => TipoMovimiento.Salida,
                    },
                    ProductoId = producto.Id,
                    TallaId = talla.TallaId,
                    Cantidad = cambio,
                    Motivo = nota is null ? Nombres[req.Motivo] : $"{Nombres[req.Motivo]}: {nota}",
                    UsuarioId = usuarioId,
                };
                productos.AgregarMovimiento(m);
                await unidad.GuardarCambiosAsync(ct);
                return m;
            },
            ct
        );
        return MovimientoDto.De((await inventario.ObtenerAsync(movimiento.Id, ct))!);
    }
}
