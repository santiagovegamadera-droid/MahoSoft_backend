using MahoSoft.Datos.Archivos;
using MahoSoft.Datos.Correo;
using MahoSoft.Datos.Repositorios;
using MahoSoft.Datos.Seed;
using MahoSoft.Entidades;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MahoSoft.Datos;

public static class DatosSetup
{
    /// <summary>
    /// The database context, the repositories, the unit of work, the image and document storage and email. Cloudinary
    /// and email are optional: without their settings only uploading photos and sending emails are unavailable.
    /// </summary>
    public static IServiceCollection AddDatos(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(config.GetConnectionString("MahoSoft")));
        services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();

        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddScoped<ICategoriaRepositorio, CategoriaRepositorio>();
        services.AddScoped<IConfiguracionRepositorio, ConfiguracionRepositorio>();
        services.AddScoped<IProveedorRepositorio, ProveedorRepositorio>();
        services.AddScoped<IProductoRepositorio, ProductoRepositorio>();
        services.AddScoped<ICompraRepositorio, CompraRepositorio>();
        services.AddScoped<IVentaRepositorio, VentaRepositorio>();
        services.AddScoped<IClienteRepositorio, ClienteRepositorio>();
        services.AddScoped<IReporteRepositorio, ReporteRepositorio>();
        services.AddScoped<IInventarioRepositorio, InventarioRepositorio>();

        services.AddOptions<CloudinaryOptions>().Bind(config.GetSection(CloudinaryOptions.Seccion));
        services.AddSingleton<IAlmacenImagenes, CloudinaryAlmacen>();

        // Documents go to Supabase Storage when it is configured (production), otherwise to the server disk
        services.AddOptions<ArchivosOptions>().Bind(config.GetSection(ArchivosOptions.Seccion));
        services.AddOptions<SupabaseOptions>().Bind(config.GetSection(SupabaseOptions.Seccion));
        if (config.GetSection(SupabaseOptions.Seccion).Get<SupabaseOptions>() is { Configurado: true })
            services.AddSingleton<IAlmacenDocumentos, SupabaseAlmacen>();
        else
            services.AddSingleton<IAlmacenDocumentos, DiscoAlmacen>();

        services.AddOptions<CorreoOptions>().Bind(config.GetSection(CorreoOptions.Seccion));
        if (config.GetSection(CorreoOptions.Seccion).Get<CorreoOptions>() is { UsaBrevo: true })
            services.AddSingleton<IEnviadorCorreo, BrevoEnviador>();
        else
            services.AddSingleton<IEnviadorCorreo, SmtpEnviador>();

        return services;
    }

    /// <summary>
    /// Production start: brings the database up to date and, if it is new, creates the basic settings and the first
    /// administrator from the "Inicial" section. Never loads sample data.
    /// </summary>
    public static async Task MigrarProduccionAsync(this IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inicial = config.GetSection(InicialOptions.Seccion).Get<InicialOptions>() ?? new InicialOptions();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(InicioProduccion));
        await InicioProduccion.PrepararAsync(db, new PasswordHasher<Usuario>(), inicial, logger);
    }

    /// <summary>Brings the database up to date and loads the sample data if it is empty (local development only).</summary>
    public static async Task MigrarYSembrarAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db, new PasswordHasher<Usuario>());
    }
}
