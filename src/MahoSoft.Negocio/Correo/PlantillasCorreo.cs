using System.Globalization;
using System.Net;
using MahoSoft.Negocio.Configuracion;
using MahoSoft.Negocio.Ventas;

namespace MahoSoft.Negocio.Correo;

/// <summary>HTML of the emails the system sends. Styles are inline: email clients ignore stylesheets.</summary>
public static class PlantillasCorreo
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");
    private const string Morado = "#503459";
    private const string Gris = "#7d6b85";

    private static string H(string? texto) => WebUtility.HtmlEncode(texto ?? "");

    private static string Pesos(decimal valor) => "$" + valor.ToString("#,##0", Es);

    private static string Marco(string negocio, string contenido) =>
        $"""
        <div style="font-family:Arial,Helvetica,sans-serif;background:#f8f5fa;padding:24px;color:{Morado}">
          <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e8dff0;border-radius:16px;padding:28px">
            <p style="margin:0 0 20px;font-size:20px;font-weight:bold">{H(negocio)}</p>
            {contenido}
          </div>
        </div>
        """;

    public static string RecuperarPassword(string negocio, string nombre, string enlace) =>
        Marco(
            negocio,
            $"""
            <p style="margin:0 0 12px">Hola, {H(nombre)}.</p>
            <p style="margin:0 0 20px">Recibimos una solicitud para cambiar tu contraseña. Usa este botón; el enlace sirve una sola vez y vence en 1 hora.</p>
            <p style="margin:0 0 24px"><a href="{H(enlace)}" style="background:{Morado};color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:10px;font-weight:bold;display:inline-block">Crear una contraseña nueva</a></p>
            <p style="margin:0;font-size:12px;color:{Gris}">Si no fuiste tú, ignora este correo: tu contraseña no cambia.</p>
            """
        );

    public static string Factura(ConfiguracionDto negocio, VentaDto venta)
    {
        var fila = (string etiqueta, string valor, bool fuerte) =>
            $"""<tr><td style="padding:3px 0;{(fuerte ? "font-weight:bold;font-size:16px" : $"color:{Gris}")}">{etiqueta}</td><td style="padding:3px 0;text-align:right;{(fuerte ? "font-weight:bold;font-size:16px" : "")}">{valor}</td></tr>""";
        var items = string.Join(
            "",
            venta.Items.Select(i =>
                $"""<tr><td style="padding:6px 0;border-bottom:1px solid #f5f0f7">{H(i.Producto)}<br><span style="font-size:12px;color:{Gris}">Talla {H(i.Talla)} · {i.Cantidad} x {Pesos(i.PrecioUnitario)}</span></td><td style="padding:6px 0;border-bottom:1px solid #f5f0f7;text-align:right">{Pesos(i.Cantidad * i.PrecioUnitario)}</td></tr>"""
            )
        );
        var totales = fila("Subtotal", Pesos(venta.Subtotal), false);
        if (venta.Descuento > 0)
            totales += fila($"Descuento ({venta.DescuentoPorcentaje:0.##}%)", "-" + Pesos(venta.Descuento), false);
        if (venta.Envio > 0)
            totales += fila("Envío", Pesos(venta.Envio), false);
        totales += fila("Total", Pesos(venta.Total), true);

        var datosNegocio = string.Join(
            " · ",
            new[] { negocio.Nit != "" ? $"NIT {negocio.Nit}" : "", negocio.Direccion, negocio.Ciudad, negocio.Telefono }
                .Where(x => x != "")
                .Select(H)
        );
        var fecha = venta.Fecha.ToOffset(Compras.CompraServicio.Colombia).ToString("dd/MM/yyyy hh:mm tt", Es);
        var pago = venta.MetodoPago.ToString();

        return Marco(
            negocio.Nombre,
            $"""
            <p style="margin:-16px 0 20px;font-size:12px;color:{Gris}">{datosNegocio}</p>
            <p style="margin:0 0 4px">Hola, {H(venta.Cliente?.Nombre ?? "")}. Gracias por tu compra.</p>
            <p style="margin:0 0 20px;font-size:13px;color:{Gris}">Comprobante <b style="color:{Morado}">{H(venta.NumeroFactura)}</b> · {fecha} · Pago: {H(pago)}</p>
            <table style="width:100%;border-collapse:collapse;font-size:14px">{items}</table>
            <table style="width:100%;border-collapse:collapse;font-size:14px;margin-top:12px">{totales}</table>
            <p style="margin:24px 0 0;font-size:12px;color:{Gris};text-align:center">{H(negocio.MensajeRecibo)}</p>
            """
        );
    }
}
