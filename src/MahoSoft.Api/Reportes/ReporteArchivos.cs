using System.Globalization;
using ClosedXML.Excel;
using MahoSoft.Negocio.Reportes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MahoSoft.Api.Reportes;

/// <summary>The sales report as a file to download: Excel (one sheet per table) or a printable PDF.</summary>
public static class ReporteArchivos
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");
    private const string FormatoPesos = "\"$\" #,##0";
    private const string Morado = "#503459";
    private const string MoradoClaro = "#F5F0F7";

    private static readonly Dictionary<string, string> Metodos = new()
    {
        ["Efectivo"] = "Efectivo",
        ["Tarjeta"] = "Tarjeta",
        ["Transferencia"] = "Transferencia",
    };

    public static string NombreArchivo(ReporteDto r, string extension) =>
        $"reporte-ventas-{r.Desde:yyyy-MM-dd}-a-{r.Hasta:yyyy-MM-dd}.{extension}";

    private static string Periodo(ReporteDto r) =>
        $"{r.Desde.ToString("d 'de' MMMM 'de' yyyy", Es)} al {r.Hasta.ToString("d 'de' MMMM 'de' yyyy", Es)}";

    private static string Pesos(decimal valor) => valor.ToString("$ #,##0", Es);

    private static string Margen(decimal? m) => m is null ? "—" : m.Value.ToString("0.0 %", Es);

    private static string EtiquetaSerie(ReporteDto r, string etiqueta) =>
        r.Agrupacion == "mes"
            ? DateOnly.ParseExact(etiqueta + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("MMMM yyyy", Es)
            : DateOnly.ParseExact(etiqueta, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("ddd d MMM", Es);

    private static (string Nombre, Func<ResumenReporteDto, object> Valor, bool EsPeso)[] Indicadores() =>
        [
            ("Total vendido", r => r.Ventas, true),
            ("Ventas (transacciones)", r => r.Transacciones, false),
            ("Prendas vendidas", r => r.Unidades, false),
            ("Ticket promedio", r => r.TicketPromedio, true),
            ("Ingresos por productos (sin envío)", r => r.Ingresos, true),
            ("Costo de lo vendido", r => r.Costo, true),
            ("Margen bruto", r => r.MargenBruto is null ? "—" : (object)r.MargenBruto.Value, false),
        ];

    public static byte[] Excel(ReporteDto r, string negocio)
    {
        using var libro = new XLWorkbook();

        var resumen = libro.AddWorksheet("Resumen");
        resumen.Cell(1, 1).Value = $"{negocio} — Reporte de ventas";
        resumen.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        resumen.Cell(2, 1).Value = Periodo(r);
        Encabezado(resumen, 4, "Indicador", "Este periodo", "Periodo anterior");
        var fila = 5;
        foreach (var (nombre, valor, esPeso) in Indicadores())
        {
            resumen.Cell(fila, 1).Value = nombre;
            foreach (var (col, datos) in new[] { (2, r.Actual), (3, r.Anterior) })
            {
                var celda = resumen.Cell(fila, col);
                celda.Value = XLCellValue.FromObject(valor(datos));
                if (esPeso)
                    celda.Style.NumberFormat.Format = FormatoPesos;
                else if (nombre == "Margen bruto" && valor(datos) is decimal)
                    celda.Style.NumberFormat.Format = "0.0%";
            }
            fila++;
        }
        resumen.Cell(fila + 1, 1).Value = "No incluye ventas anuladas.";
        resumen.Cell(fila + 1, 1).Style.Font.SetItalic();

        var serie = libro.AddWorksheet(r.Agrupacion == "mes" ? "Por mes" : "Por día");
        Encabezado(serie, 1, r.Agrupacion == "mes" ? "Mes" : "Día", "Vendido", "Ventas");
        fila = 2;
        foreach (var p in r.Serie)
        {
            serie.Cell(fila, 1).Value = EtiquetaSerie(r, p.Etiqueta);
            serie.Cell(fila, 2).Value = p.Ventas;
            serie.Cell(fila, 2).Style.NumberFormat.Format = FormatoPesos;
            serie.Cell(fila, 3).Value = p.Transacciones;
            fila++;
        }

        var productos = libro.AddWorksheet("Productos");
        Encabezado(productos, 1, "#", "Producto", "Categoría", "Prendas", "Ingresos");
        fila = 2;
        foreach (var p in r.TopProductos)
        {
            productos.Cell(fila, 1).Value = fila - 1;
            productos.Cell(fila, 2).Value = p.Producto;
            productos.Cell(fila, 3).Value = p.Categoria;
            productos.Cell(fila, 4).Value = p.Unidades;
            productos.Cell(fila, 5).Value = p.Ingresos;
            productos.Cell(fila, 5).Style.NumberFormat.Format = FormatoPesos;
            fila++;
        }

        var categorias = libro.AddWorksheet("Categorías");
        Encabezado(categorias, 1, "Categoría", "Ingresos");
        fila = 2;
        foreach (var c in r.PorCategoria)
        {
            categorias.Cell(fila, 1).Value = c.Categoria;
            categorias.Cell(fila, 2).Value = c.Ingresos;
            categorias.Cell(fila, 2).Style.NumberFormat.Format = FormatoPesos;
            fila++;
        }

        var pagos = libro.AddWorksheet("Medios de pago");
        Encabezado(pagos, 1, "Medio", "Vendido", "Ventas");
        fila = 2;
        foreach (var m in r.PorMetodoPago)
        {
            pagos.Cell(fila, 1).Value = Metodos.GetValueOrDefault(m.Metodo, m.Metodo);
            pagos.Cell(fila, 2).Value = m.Ventas;
            pagos.Cell(fila, 2).Style.NumberFormat.Format = FormatoPesos;
            pagos.Cell(fila, 3).Value = m.Transacciones;
            fila++;
        }

        foreach (var hoja in libro.Worksheets)
            hoja.Columns().AdjustToContents();
        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return salida.ToArray();
    }

    private static void Encabezado(IXLWorksheet hoja, int fila, params string[] titulos)
    {
        for (var i = 0; i < titulos.Length; i++)
        {
            var celda = hoja.Cell(fila, i + 1);
            celda.Value = titulos[i];
            celda.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml(Morado));
        }
    }

    public static byte[] Pdf(ReporteDto r, string negocio) =>
        Document
            .Create(doc =>
                doc.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(36);
                    page.DefaultTextStyle(t => t.FontSize(9).FontColor(Morado));

                    page.Header()
                        .Column(c =>
                        {
                            c.Item().Text(negocio).FontSize(16).Bold();
                            c.Item().Text($"Reporte de ventas · {Periodo(r)}").FontSize(10);
                            c.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Morado);
                        });

                    page.Content()
                        .PaddingVertical(12)
                        .Column(c =>
                        {
                            c.Spacing(14);
                            c.Item().Element(e => Tabla(e, ["Indicador", "Este periodo", "Periodo anterior"], [3, 2, 2],
                                Indicadores().Select(i => new[]
                                {
                                    i.Nombre,
                                    Formato(i.Valor(r.Actual), i.EsPeso),
                                    Formato(i.Valor(r.Anterior), i.EsPeso),
                                })));
                            c.Item().Text("Productos más vendidos").Bold().FontSize(11);
                            c.Item().Element(e => Tabla(e, ["#", "Producto", "Categoría", "Prendas", "Ingresos"], [0.5f, 4, 2, 1, 2],
                                r.TopProductos.Select((p, i) => new[]
                                {
                                    (i + 1).ToString(), p.Producto, p.Categoria, p.Unidades.ToString(), Pesos(p.Ingresos),
                                })));
                            c.Item().Row(row =>
                            {
                                row.Spacing(14);
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().PaddingBottom(4).Text("Por categoría").Bold().FontSize(11);
                                    col.Item().Element(e => Tabla(e, ["Categoría", "Ingresos"], [2, 1.5f],
                                        r.PorCategoria.Select(x => new[] { x.Categoria, Pesos(x.Ingresos) })));
                                });
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().PaddingBottom(4).Text("Por medio de pago").Bold().FontSize(11);
                                    col.Item().Element(e => Tabla(e, ["Medio", "Vendido", "Ventas"], [2, 1.5f, 1],
                                        r.PorMetodoPago.Select(m => new[]
                                        {
                                            Metodos.GetValueOrDefault(m.Metodo, m.Metodo), Pesos(m.Ventas), m.Transacciones.ToString(),
                                        })));
                                });
                            });
                            c.Item().Text(r.Agrupacion == "mes" ? "Ventas por mes" : "Ventas por día").Bold().FontSize(11);
                            c.Item().Element(e => Tabla(e, [r.Agrupacion == "mes" ? "Mes" : "Día", "Vendido", "Ventas"], [2, 2, 1],
                                r.Serie.Select(p => new[] { EtiquetaSerie(r, p.Etiqueta), Pesos(p.Ventas), p.Transacciones.ToString() })));
                        });

                    page.Footer()
                        .Row(row =>
                        {
                            row.RelativeItem().Text("No incluye ventas anuladas.").FontSize(8).Italic();
                            row.RelativeItem()
                                .AlignRight()
                                .Text(t =>
                                {
                                    t.DefaultTextStyle(s => s.FontSize(8));
                                    t.Span("Página ");
                                    t.CurrentPageNumber();
                                    t.Span(" de ");
                                    t.TotalPages();
                                });
                        });
                })
            )
            .GeneratePdf();

    private static string Formato(object valor, bool esPeso) =>
        valor switch
        {
            decimal d when esPeso => Pesos(d),
            decimal d => Margen(d),
            _ => valor.ToString() ?? "",
        };

    private static void Tabla(IContainer contenedor, string[] titulos, float[] anchos, IEnumerable<string[]> filas)
    {
        contenedor.Table(t =>
        {
            t.ColumnsDefinition(cols =>
            {
                foreach (var ancho in anchos)
                    cols.RelativeColumn(ancho);
            });
            t.Header(h =>
            {
                foreach (var titulo in titulos)
                    h.Cell().Background(MoradoClaro).Padding(4).Text(titulo).Bold();
            });
            var vacia = true;
            foreach (var fila in filas)
            {
                vacia = false;
                foreach (var valor in fila)
                    t.Cell().BorderBottom(0.5f).BorderColor(MoradoClaro).Padding(4).Text(valor);
            }
            if (vacia)
                t.Cell().ColumnSpan((uint)titulos.Length).Padding(4).Text("Sin ventas en este periodo").Italic();
        });
    }
}
