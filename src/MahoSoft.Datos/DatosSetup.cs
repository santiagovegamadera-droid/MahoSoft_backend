using MahoSoft.Datos.Archivos;
using MahoSoft.Datos.Repositorios;
using MahoSoft.Datos.Seed;
using MahoSoft.Entidades;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MahoSoft.Datos;

public static class DatosSetup
{
    /// <summary>
    /// The database context, the repositories, the unit of work and the image storage. Cloudinary's settings are
    /// checked when the first image is uploaded, so the API still starts on a machine without them.
    /// </summary>
    public static IServiceCollection AddDatos(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(config.GetConnectionString("MahoSoft")));
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

        services
            .AddOptions<CloudinaryOptions>()
            .Bind(config.GetSection(CloudinaryOptions.Seccion))
            .ValidateDataAnnotations();
        services.AddSingleton<IAlmacenImagenes, CloudinaryAlmacen>();

        services.AddOptions<ArchivosOptions>().Bind(config.GetSection(ArchivosOptions.Seccion));
        services.AddSingleton<IAlmacenDocumentos, DiscoAlmacen>();

        return services;
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
