using MahoSoft.Api.Auth;
using MahoSoft.Api.Errores;
using MahoSoft.Negocio;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNegocio(builder.Configuration.GetConnectionString("MahoSoft"));
builder.Services.AddMahoAuth(builder.Configuration);

// The web app runs on its own origin (Vite dev server, later its hosting)
builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p =>
        p.WithOrigins(builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()
    )
);

builder.Services.AddControllers(o => o.Filters.Add<NegocioExceptionFilter>());
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

app.UseHttpsRedirection();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();
