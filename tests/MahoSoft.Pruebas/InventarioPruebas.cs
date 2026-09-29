using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace MahoSoft.Pruebas;

/// <summary>Purchases, sales, voids and adjustments: totals, stock and the numbers that must never repeat.</summary>
[Collection(ApiCollection.Nombre)]
public class InventarioPruebas(ApiFixture api)
{
    // Sample data: product 2 is Blusa Seda Negra (sizes XS–XL), 4 is Falda Plisada Beige ($75.000),
    // supplier 2 is ModaCali (active) and 5 is KnitCo (inactive)
    private const int Blusa = 2;
    private const int Falda = 4;

    private static object Compra(string factura, int proveedor = 2, object[]? items = null, decimal descuento = 0, decimal iva = 0) =>
        new
        {
            proveedorId = proveedor,
            tipoComprobante = "Factura electrónica de venta",
            numeroComprobante = factura,
            fecha = DateTime.Today.ToString("yyyy-MM-dd"),
            hora = "",
            condicionPago = "Contado",
            estadoPago = "Pagada",
            ivaPorcentaje = iva,
            preciosIncluyenIva = false,
            descuento,
            items = items ?? [new { productoId = Blusa, talla = "S", cantidad = 1, precioUnitario = 30000 }],
        };

    private static object Venta(int producto, string talla, int cantidad, decimal descuento = 0) =>
        new
        {
            tipo = "Tienda",
            metodoPago = "Efectivo",
            descuentoPorcentaje = descuento,
            items = new[] { new { productoId = producto, talla, cantidad } },
        };

    [Fact]
    public async Task Compra_calcula_totales_suma_stock_y_actualiza_el_costo()
    {
        var c = await api.AdminAsync();
        var antes = await c.StockAsync(Blusa, "M");

        // 3 × 30.000 + 2 × 50.000 = 190.000; less 10.000; 19% IVA on 180.000 = 34.200
        var r = await c.MultipartAsync(
            "/api/compras",
            Compra(
                "T-TOTALES",
                items:
                [
                    new { productoId = Blusa, talla = "M", cantidad = 3, precioUnitario = 30000 },
                    new { productoId = Falda, talla = "M", cantidad = 2, precioUnitario = 50000 },
                ],
                descuento: 10000,
                iva: 19
            ),
            "documento",
            "%PDF-1.4 prueba"u8.ToArray()
        );
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var compra = await r.LeerAsync();
        Assert.Equal(190000, compra["subtotal"]!.GetValue<decimal>());
        Assert.Equal(34200, compra["iva"]!.GetValue<decimal>());
        Assert.Equal(214200, compra["total"]!.GetValue<decimal>());
        Assert.Matches(@"^OC-\d{4}-\d{3}$", compra["numero"]!.GetValue<string>());
        Assert.Equal("application/pdf", compra["documento"]!["tipoMime"]!.GetValue<string>());

        Assert.Equal(antes + 3, await c.StockAsync(Blusa, "M"));
        // Cost without IVA, less its share of the discount: 30.000 × (1 − 10.000 / 190.000)
        Assert.Equal(28421, await c.CostoAsync(Blusa));
    }

    [Fact]
    public async Task Compra_rechaza_factura_repetida_proveedor_inactivo_y_archivo_falso()
    {
        var c = await api.AdminAsync();
        Assert.Equal(HttpStatusCode.Created, (await c.MultipartAsync("/api/compras", Compra("T-DUP"))).StatusCode);

        var repetida = await c.MultipartAsync("/api/compras", Compra("T-DUP"));
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);

        var inactivo = await c.MultipartAsync("/api/compras", Compra("T-INACTIVO", proveedor: 5));
        Assert.Equal(HttpStatusCode.BadRequest, inactivo.StatusCode);

        var falso = await c.MultipartAsync("/api/compras", Compra("T-FALSO"), "documento", "no soy un pdf"u8.ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, falso.StatusCode);
    }

    [Fact]
    public async Task Compras_simultaneas_suman_todo_el_stock_con_numeros_distintos()
    {
        var c = await api.AdminAsync();
        var antes = await c.StockAsync(Blusa, "L");

        var respuestas = await Task.WhenAll(
            Enumerable
                .Range(0, 6)
                .Select(i =>
                    c.MultipartAsync(
                        "/api/compras",
                        Compra($"T-PAR-{i}", items: [new { productoId = Blusa, talla = "L", cantidad = 1, precioUnitario = 30000 }])
                    )
                )
        );

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var numeros = await Task.WhenAll(respuestas.Select(async r => (await r.LeerAsync())["numero"]!.GetValue<string>()));
        Assert.Equal(6, numeros.Distinct().Count());
        Assert.Equal(antes + 6, await c.StockAsync(Blusa, "L"));
    }

    [Fact]
    public async Task Venta_usa_el_precio_del_servidor_descuenta_stock_y_valida_el_descuento()
    {
        var c = await api.AdminAsync();
        var antes = await c.StockAsync(Falda, "S");

        var r = await c.MultipartAsync("/api/ventas", Venta(Falda, "S", 2, descuento: 10));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var venta = await r.LeerAsync();
        Assert.Equal(150000, venta["subtotal"]!.GetValue<decimal>());
        Assert.Equal(15000, venta["descuento"]!.GetValue<decimal>());
        Assert.Equal(135000, venta["total"]!.GetValue<decimal>());
        Assert.Matches(@"^VTA-\d{4}-\d{4}$", venta["numeroFactura"]!.GetValue<string>());
        Assert.Equal(antes - 2, await c.StockAsync(Falda, "S"));

        // 7% isn't one of the POS discounts in Configuración
        Assert.Equal(HttpStatusCode.BadRequest, (await c.MultipartAsync("/api/ventas", Venta(Falda, "S", 1, descuento: 7))).StatusCode);
    }

    [Fact]
    public async Task No_se_vende_mas_de_lo_que_hay_aunque_varias_cajas_cobren_a_la_vez()
    {
        var admin = await api.AdminAsync();
        var caja = await api.AdminAsync();
        // Leave exactly 2 units of Falda XS
        var ajuste = await admin.PostAsJsonAsync(
            "/api/inventario/ajustes",
            new { productoId = Falda, talla = "XS", motivo = "Conteo", cantidad = 2, nota = "prueba" }
        );
        Assert.True(ajuste.IsSuccessStatusCode || ajuste.StatusCode == HttpStatusCode.BadRequest); // 400 if it already had 2
        Assert.Equal(2, await caja.StockAsync(Falda, "XS"));

        var respuestas = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => caja.MultipartAsync("/api/ventas", Venta(Falda, "XS", 1)))
        );

        Assert.Equal(2, respuestas.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(3, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(0, await caja.StockAsync(Falda, "XS"));
    }

    [Fact]
    public async Task Anular_devuelve_el_stock_y_no_se_anula_dos_veces()
    {
        var c = await api.AdminAsync();
        var antes = await c.StockAsync(Falda, "L");
        var venta = await (await c.MultipartAsync("/api/ventas", Venta(Falda, "L", 1))).LeerAsync();
        var id = venta["id"]!.GetValue<int>();
        Assert.Equal(antes - 1, await c.StockAsync(Falda, "L"));

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/ventas/{id}/anular", new { motivo = " " })).StatusCode);

        var anulada = await (await c.PostAsJsonAsync($"/api/ventas/{id}/anular", new { motivo = "Prueba" })).LeerAsync();
        Assert.Equal("Anulada", anulada["estado"]!.GetValue<string>());
        Assert.Equal("Ana Martínez", anulada["anuladaPor"]!.GetValue<string>());
        Assert.Equal(antes, await c.StockAsync(Falda, "L"));

        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/ventas/{id}/anular", new { motivo = "Otra" })).StatusCode);
    }

    [Fact]
    public async Task Ajustes_de_inventario_por_conteo_y_salida()
    {
        var c = await api.AdminAsync();
        async Task<HttpResponseMessage> Ajustar(string motivo, int cantidad) =>
            await c.PostAsJsonAsync("/api/inventario/ajustes", new { productoId = Blusa, talla = "XL", motivo, cantidad });

        Assert.Equal(HttpStatusCode.Created, (await Ajustar("Conteo", 7)).StatusCode);
        Assert.Equal(7, await c.StockAsync(Blusa, "XL"));

        var movimiento = await (await Ajustar("Danado", 2)).LeerAsync();
        Assert.Equal("Salida", movimiento["tipo"]!.GetValue<string>());
        Assert.Equal(-2, movimiento["cantidad"]!.GetValue<int>());
        Assert.Equal(5, await c.StockAsync(Blusa, "XL"));

        Assert.Equal(HttpStatusCode.Conflict, (await Ajustar("DevolucionProveedor", 99)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ajustar("Conteo", 5)).StatusCode); // matches the stock
    }

    [Fact]
    public async Task El_stock_de_cada_talla_es_la_suma_de_sus_movimientos()
    {
        // Runs after whatever the other tests did in this run
        await api.AdminAsync();
        var descuadres = await ApiFixture.SqlAsync<int>(
            """
            SELECT COUNT(*) FROM ProductoTallas pt
            WHERE pt.Stock <> (SELECT ISNULL(SUM(m.Cantidad), 0) FROM MovimientosInventario m
                               WHERE m.ProductoId = pt.ProductoId AND m.TallaId = pt.TallaId)
            """
        );
        Assert.Equal(0, descuadres);
    }
}
