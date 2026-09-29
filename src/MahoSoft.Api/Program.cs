using System.Text.Json.Serialization;
using MahoSoft.Api.Auth;
using MahoSoft.Api.Errores;
using MahoSoft.Negocio;

// PDF reports: QuestPDF's free Community license (businesses under USD 1M yearly revenue)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Outside development the settings come from environment variables (see README, "Publicar"). Stop right away when
// one is missing instead of starting half configured.
if (!builder.Environment.IsDevelopment())
{
    string[] faltan =
    [
        .. string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("MahoSoft")) ? ["ConnectionStrings__MahoSoft"] : Array.Empty<string>(),
        .. (builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? []).Length == 0 ? ["Cors__Origenes__0"] : Array.Empty<string>(),
        .. string.IsNullOrWhiteSpace(builder.Configuration["App:UrlFrontend"]) ? ["App__UrlFrontend"] : Array.Empty<string>(),
        // Hosts like Render wipe the disk on every deploy: invoices and receipts must go to Supabase Storage
        .. string.IsNullOrWhiteSpace(builder.Configuration["Supabase:Url"]) ? ["Supabase__Url"] : Array.Empty<string>(),
        .. string.IsNullOrWhiteSpace(builder.Configuration["Supabase:ServiceKey"]) ? ["Supabase__ServiceKey"] : Array.Empty<string>(),
    ];
    if (faltan.Length > 0)
        throw new InvalidOperationException($"Faltan variables de entorno de producción: {string.Join(", ", faltan)}");
}

builder.Services.AddNegocio(builder.Configuration);
builder.Services.AddMahoAuth(builder.Configuration);

// The web app runs on its own origin (Vite dev server, later its hosting)
builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p =>
        p.WithOrigins(builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()
    )
);

builder
    .Services.AddControllers(o => o.Filters.Add<NegocioExceptionFilter>())
    // Enums travel by name ("Credito", "Pagada"), the same text stored in the database
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();

    // Local development: bring the database up to date and load the sample data on first run
    await app.Services.PrepararBaseDeDatosAsync();
}
else
{
    // Production: bring the database up to date; a new one gets the basic settings and the first administrator
    await app.Services.PrepararProduccionAsync(app.Configuration);
    // Browsers only talk to this API over HTTPS from now on
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();

// Lets the tests start the API (WebApplicationFactory<Program>)
public partial class Program;
