using System.Net.Mail;
using MahoSoft.Datos.Correo;
using MahoSoft.Negocio.Configuracion;
using MahoSoft.Negocio.Ventas;

namespace MahoSoft.Negocio.Correo;

/// <summary>Settings of the "App" section: where the frontend lives, for links sent by email.</summary>
public class AppOptions
{
    public const string Seccion = "App";

    public string UrlFrontend { get; set; } = "http://localhost:8443";
}

public interface ICorreoServicio
{
    /// <summary>Emails a sale's receipt to <paramref name="correo"/>.</summary>
    Task EnviarFacturaAsync(int ventaId, string correo, CancellationToken ct = default);
}

public class CorreoServicio(IVentaServicio ventas, IConfiguracionServicio config, IEnviadorCorreo enviador)
    : ICorreoServicio
{
    public async Task EnviarFacturaAsync(int ventaId, string correo, CancellationToken ct = default)
    {
        correo = (correo ?? "").Trim();
        if (!MailAddress.TryCreate(correo, out _))
            throw new ValidacionException("El correo no es válido");
        if (!enviador.Configurado)
            throw new ServicioNoDisponibleException("El envío de correos todavía no está configurado");

        var venta = await ventas.ObtenerAsync(ventaId, ct);
        var negocio = await config.ObtenerAsync(ct);
        await Enviar(
            () =>
                enviador.EnviarAsync(
                    correo,
                    $"Tu compra en {negocio.Nombre} · {venta.NumeroFactura}",
                    PlantillasCorreo.Factura(negocio, venta),
                    ct
                )
        );
    }

    /// <summary>Turns a refused email into a message for the user.</summary>
    public static async Task Enviar(Func<Task> envio)
    {
        try
        {
            await envio();
        }
        catch (CorreoException e)
        {
            throw new ServicioNoDisponibleException(e.Message);
        }
    }
}
