using System.Net.Http.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MahoSoft.Datos.Correo;

/// <summary>
/// Settings of the "Correo" section. Two ways to send:
/// <list type="bullet">
/// <item>SMTP, e.g. Gmail: Host smtp.gmail.com, Puerto 587, Usuario the Gmail address and Password an app password.</item>
/// <item>Brevo's HTTP API (BrevoApiKey + Remitente, a sender verified in Brevo), for hosts that block SMTP such as
/// Render's free plan.</item>
/// </list>
/// Secrets go in user-secrets in development and environment variables in production.
/// </summary>
public class CorreoOptions
{
    public const string Seccion = "Correo";

    public string Host { get; set; } = "smtp.gmail.com";
    public int Puerto { get; set; } = 587;
    public string Usuario { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>Address the emails come from; the Usuario when empty.</summary>
    public string Remitente { get; set; } = "";

    /// <summary>Name people see as the sender.</summary>
    public string NombreRemitente { get; set; } = "Maho Boutique";

    /// <summary>Brevo API key (xkeysib-…). When set, emails go through Brevo instead of SMTP.</summary>
    public string BrevoApiKey { get; set; } = "";

    public bool UsaBrevo => !string.IsNullOrWhiteSpace(BrevoApiKey);

    /// <summary>
    /// Set when a sender is known: SMTP with or without login (e.g. a local test server), or Brevo with its
    /// verified sender.
    /// </summary>
    public bool Configurado =>
        UsaBrevo
            ? !string.IsNullOrWhiteSpace(Remitente)
            : !string.IsNullOrWhiteSpace(Remitente) || !string.IsNullOrWhiteSpace(Usuario);
}

public interface IEnviadorCorreo
{
    /// <summary>Whether sending is set up; without it the features that email are unavailable.</summary>
    bool Configurado { get; }

    /// <summary>Sends an HTML email; throws <see cref="CorreoException"/> when the server refuses it.</summary>
    Task EnviarAsync(string para, string asunto, string html, CancellationToken ct = default);
}

public class CorreoException(string mensaje, Exception? causa = null) : Exception(mensaje, causa);

public class SmtpEnviador(IOptions<CorreoOptions> options, ILogger<SmtpEnviador> logger) : IEnviadorCorreo
{
    private readonly CorreoOptions _opciones = options.Value;

    public bool Configurado => _opciones.Configurado;

    public async Task EnviarAsync(string para, string asunto, string html, CancellationToken ct = default)
    {
        if (!Configurado)
            throw new CorreoException("El envío de correos no está configurado");

        var mensaje = new MimeMessage();
        var remitente = string.IsNullOrWhiteSpace(_opciones.Remitente) ? _opciones.Usuario : _opciones.Remitente;
        mensaje.From.Add(new MailboxAddress(_opciones.NombreRemitente, remitente));
        mensaje.To.Add(MailboxAddress.Parse(para));
        mensaje.Subject = asunto;
        mensaje.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

        try
        {
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_opciones.Host, _opciones.Puerto, SecureSocketOptions.StartTlsWhenAvailable, ct);
            if (!string.IsNullOrWhiteSpace(_opciones.Usuario))
                await smtp.AuthenticateAsync(_opciones.Usuario, _opciones.Password, ct);
            await smtp.SendAsync(mensaje, ct);
            await smtp.DisconnectAsync(true, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "No se pudo enviar el correo \"{Asunto}\" a {Para}", asunto, para);
            throw new CorreoException("No se pudo enviar el correo. Inténtalo de nuevo más tarde.", e);
        }
    }
}

/// <summary>Emails through Brevo's transactional API (HTTPS, so it works where SMTP ports are blocked).</summary>
public class BrevoEnviador(IOptions<CorreoOptions> options, ILogger<BrevoEnviador> logger) : IEnviadorCorreo
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.brevo.com/v3/"),
        Timeout = TimeSpan.FromSeconds(30),
    };

    private readonly CorreoOptions _opciones = options.Value;

    public bool Configurado => _opciones.Configurado;

    public async Task EnviarAsync(string para, string asunto, string html, CancellationToken ct = default)
    {
        if (!Configurado)
            throw new CorreoException("El envío de correos no está configurado");

        using var pedido = new HttpRequestMessage(HttpMethod.Post, "smtp/email")
        {
            Content = JsonContent.Create(
                new
                {
                    sender = new { name = _opciones.NombreRemitente, email = _opciones.Remitente },
                    to = new[] { new { email = para } },
                    subject = asunto,
                    htmlContent = html,
                }
            ),
        };
        pedido.Headers.Add("api-key", _opciones.BrevoApiKey);

        try
        {
            using var r = await Http.SendAsync(pedido, ct);
            if (!r.IsSuccessStatusCode)
                throw new HttpRequestException($"Brevo respondió {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync(ct)}");
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(e, "No se pudo enviar el correo \"{Asunto}\" a {Para}", asunto, para);
            throw new CorreoException("No se pudo enviar el correo. Inténtalo de nuevo más tarde.", e);
        }
    }
}
