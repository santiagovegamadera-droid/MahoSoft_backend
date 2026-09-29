namespace MahoSoft.Negocio.Reportes;

/// <summary>Sales of a period: amount charged and number of sales.</summary>
public record ResumenDto(decimal Ventas, int Transacciones);

public record IngresoMesDto(string Mes, decimal Ventas);

/// <summary>Revenue of a category's products, net of the sale discounts (shipping isn't included).</summary>
public record CategoriaVentaDto(string Categoria, decimal Ingresos);

public record ProductoVendidoDto(
    int ProductoId,
    string Producto,
    string Categoria,
    int Unidades,
    decimal Ingresos,
    int Stock
);

public record AlertaStockDto(int ProductoId, string Producto, string Talla, int Stock, int Minimo);

/// <summary>The home screen: today and this month against the previous ones, trends and what needs restocking.</summary>
public record TableroDto(
    ResumenDto Hoy,
    ResumenDto Ayer,
    ResumenDto Mes,
    ResumenDto MesAnteriorALaFecha,
    IngresoMesDto[] IngresosPorMes,
    CategoriaVentaDto[] VentasPorCategoria,
    ProductoVendidoDto[] TopProductos,
    AlertaStockDto[] Alertas,
    int TotalAlertas
);

/// <summary>
/// Totals of a period. Ingresos is product revenue net of discounts (Ventas minus shipping); MargenBruto is
/// (Ingresos - Costo) / Ingresos, null without sales.
/// </summary>
public record ResumenReporteDto(
    decimal Ventas,
    int Transacciones,
    int Unidades,
    decimal Ingresos,
    decimal Costo,
    decimal? MargenBruto,
    decimal TicketPromedio
);

/// <summary>One day (yyyy-MM-dd) or month (yyyy-MM) of the series.</summary>
public record PuntoDto(string Etiqueta, decimal Ventas, int Transacciones);

public record MetodoPagoDto(string Metodo, decimal Ventas, int Transacciones);

/// <summary>A sales report between two dates (both included), compared with the period of equal length before it.</summary>
public record ReporteDto(
    DateOnly Desde,
    DateOnly Hasta,
    string Agrupacion,
    ResumenReporteDto Actual,
    ResumenReporteDto Anterior,
    PuntoDto[] Serie,
    ProductoVendidoDto[] TopProductos,
    CategoriaVentaDto[] PorCategoria,
    MetodoPagoDto[] PorMetodoPago
);
