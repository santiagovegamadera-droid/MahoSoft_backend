using System.Globalization;
using MahoSoft.Datos.Repositorios;
using MahoSoft.Negocio.Compras;

namespace MahoSoft.Negocio.Reportes;

public interface IReporteServicio
{
    Task<TableroDto> TableroAsync(CancellationToken ct = default);

    /// <summary>Sales between two local dates, both included; grouped by day, or by month past two months.</summary>
    Task<ReporteDto> ReporteAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default);
}

/// <summary>
/// Figures computed from registered sales (voided ones don't count), in Colombian local time. A line's revenue is
/// its price times quantity less its share of the sale discount, so categories and products add up to the sales
/// without shipping.
/// </summary>
public class ReporteServicio(IReporteRepositorio reportes, IConfiguracionRepositorio config) : IReporteServicio
{
    private static readonly TimeSpan Colombia = CompraServicio.Colombia;
    private const int MaximoDias = 731;

    public async Task<TableroDto> TableroAsync(CancellationToken ct = default)
    {
        var hoy = Hoy();
        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var inicioSerie = inicioMes.AddMonths(-5);
        // The previous month up to the same day, so a half month isn't compared with a whole one
        var inicioMesAnterior = inicioMes.AddMonths(-1);
        var finMesAnterior = DateOnly.FromDayNumber(
            Math.Min(inicioMesAnterior.AddDays(hoy.Day - 1).DayNumber, inicioMes.AddDays(-1).DayNumber)
        );

        var ventas = await reportes.VentasAsync(Inicio(inicioSerie), Inicio(hoy.AddDays(1)), ct);
        List<VentaParaReporte> Entre(DateOnly desde, DateOnly hasta) =>
            ventas.Where(v => Dia(v.Fecha) >= desde && Dia(v.Fecha) <= hasta).ToList();

        var delMes = Entre(inicioMes, hoy);
        var top = TopProductos(delMes, 5);
        var stock = await reportes.StockPorProductoAsync(top.Select(p => p.ProductoId), ct);
        var umbral = (await config.ObtenerNegocioAsync(ct)).StockBajoTalla;
        var alertas = await reportes.StockBajoAsync(umbral, ct);

        return new TableroDto(
            Resumen(Entre(hoy, hoy)),
            Resumen(Entre(hoy.AddDays(-1), hoy.AddDays(-1))),
            Resumen(delMes),
            Resumen(Entre(inicioMesAnterior, finMesAnterior)),
            Enumerable.Range(0, 6)
                .Select(i => inicioSerie.AddMonths(i))
                .Select(m => new IngresoMesDto(
                    m.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                    Entre(m, m.AddMonths(1).AddDays(-1)).Sum(v => v.Total)
                ))
                .ToArray(),
            PorCategoria(delMes),
            top.Select(p => p with { Stock = stock.GetValueOrDefault(p.ProductoId) }).ToArray(),
            alertas.Take(8).Select(a => new AlertaStockDto(a.ProductoId, a.Producto, a.Talla, a.Stock, umbral)).ToArray(),
            alertas.Count
        );
    }

    public async Task<ReporteDto> ReporteAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        if (hasta < desde)
            throw new ValidacionException("La fecha final no puede ser anterior a la inicial");
        var dias = hasta.DayNumber - desde.DayNumber + 1;
        if (dias > MaximoDias)
            throw new ValidacionException("El reporte puede cubrir hasta dos años");

        var anteriorDesde = desde.AddDays(-dias);
        var ventas = await reportes.VentasAsync(Inicio(anteriorDesde), Inicio(hasta.AddDays(1)), ct);
        var actuales = ventas.Where(v => Dia(v.Fecha) >= desde).ToList();
        var anteriores = ventas.Where(v => Dia(v.Fecha) < desde).ToList();

        var porMes = dias > 62;
        string Etiqueta(DateOnly d) =>
            d.ToString(porMes ? "yyyy-MM" : "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var puntos = porMes
            ? Enumerable.Range(0, 1 + (hasta.Year - desde.Year) * 12 + hasta.Month - desde.Month)
                .Select(i => new DateOnly(desde.Year, desde.Month, 1).AddMonths(i))
            : Enumerable.Range(0, dias).Select(desde.AddDays);
        var grupos = actuales.GroupBy(v => Etiqueta(Dia(v.Fecha))).ToDictionary(g => g.Key);

        return new ReporteDto(
            desde,
            hasta,
            porMes ? "mes" : "dia",
            ResumenCompleto(actuales),
            ResumenCompleto(anteriores),
            puntos
                .Select(Etiqueta)
                .Select(e =>
                    grupos.TryGetValue(e, out var g)
                        ? new PuntoDto(e, g.Sum(v => v.Total), g.Count())
                        : new PuntoDto(e, 0, 0)
                )
                .ToArray(),
            TopProductos(actuales, 10).ToArray(),
            PorCategoria(actuales),
            actuales
                .GroupBy(v => v.MetodoPago)
                .OrderByDescending(g => g.Sum(v => v.Total))
                .Select(g => new MetodoPagoDto(g.Key.ToString(), g.Sum(v => v.Total), g.Count()))
                .ToArray()
        );
    }

    private static ResumenDto Resumen(List<VentaParaReporte> ventas) => new(ventas.Sum(v => v.Total), ventas.Count);

    private static ResumenReporteDto ResumenCompleto(List<VentaParaReporte> ventas)
    {
        var total = ventas.Sum(v => v.Total);
        var ingresos = ventas.Sum(v => v.Subtotal - v.Descuento);
        var costo = ventas.SelectMany(v => v.Lineas).Sum(l => l.Cantidad * l.CostoUnitario);
        return new ResumenReporteDto(
            total,
            ventas.Count,
            ventas.SelectMany(v => v.Lineas).Sum(l => l.Cantidad),
            ingresos,
            costo,
            ingresos > 0 ? Math.Round((ingresos - costo) / ingresos, 4) : null,
            ventas.Count > 0 ? Math.Round(total / ventas.Count, 0) : 0
        );
    }

    /// <summary>Each line with its revenue net of its share of the sale discount.</summary>
    private static IEnumerable<(LineaVendida Linea, decimal Ingreso)> Lineas(List<VentaParaReporte> ventas) =>
        ventas.SelectMany(v =>
        {
            var proporcion = v.Subtotal > 0 ? 1 - v.Descuento / v.Subtotal : 1;
            return v.Lineas.Select(l => (l, l.Cantidad * l.PrecioUnitario * proporcion));
        });

    private static List<ProductoVendidoDto> TopProductos(List<VentaParaReporte> ventas, int cuantos) =>
        Lineas(ventas)
            .GroupBy(x => x.Linea.ProductoId)
            .Select(g => new ProductoVendidoDto(
                g.Key,
                g.Last().Linea.Producto,
                g.Last().Linea.Categoria,
                g.Sum(x => x.Linea.Cantidad),
                Math.Round(g.Sum(x => x.Ingreso), 0),
                0
            ))
            .OrderByDescending(p => p.Ingresos)
            .ThenByDescending(p => p.Unidades)
            .Take(cuantos)
            .ToList();

    private static CategoriaVentaDto[] PorCategoria(List<VentaParaReporte> ventas) =>
        Lineas(ventas)
            .GroupBy(x => x.Linea.Categoria)
            .Select(g => new CategoriaVentaDto(g.Key, Math.Round(g.Sum(x => x.Ingreso), 0)))
            .OrderByDescending(c => c.Ingresos)
            .ToArray();

    private static DateOnly Hoy() => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(Colombia).DateTime);

    private static DateOnly Dia(DateTimeOffset fecha) => DateOnly.FromDateTime(fecha.ToOffset(Colombia).DateTime);

    private static DateTimeOffset Inicio(DateOnly dia) => new(dia.ToDateTime(TimeOnly.MinValue), Colombia);
}
