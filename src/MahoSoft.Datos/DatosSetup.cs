using MahoSoft.Datos.Repositorios;
using MahoSoft.Datos.Seed;
using MahoSoft.Entidades;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MahoSoft.Datos;

public static class DatosSetup
{
    /// <summary>The database context, the repositories and the unit of work.</summary>
    public static IServiceCollection AddDatos(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
        services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();

        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddScoped<ICategoriaRepositorio, CategoriaRepositorio>();
        services.AddScoped<IConfiguracionRepositorio, ConfiguracionRepositorio>();

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
