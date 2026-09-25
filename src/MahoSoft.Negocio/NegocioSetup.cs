using MahoSoft.Datos;
using MahoSoft.Entidades;
using MahoSoft.Negocio.Auth;
using MahoSoft.Negocio.Categorias;
using MahoSoft.Negocio.Configuracion;
using MahoSoft.Negocio.Proveedores;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace MahoSoft.Negocio;

public static class NegocioSetup
{
    /// <summary>The business services and, beneath them, the data layer. The API only registers this.</summary>
    public static IServiceCollection AddNegocio(this IServiceCollection services, string? connectionString)
    {
        services.AddDatos(connectionString);
        services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

        services.AddScoped<IAuthServicio, AuthServicio>();
        services.AddScoped<ICategoriaServicio, CategoriaServicio>();
        services.AddScoped<IConfiguracionServicio, ConfiguracionServicio>();
        services.AddScoped<IProveedorServicio, ProveedorServicio>();

        return services;
    }

    /// <summary>Brings the database up to date and loads the sample data if it is empty (local development only).</summary>
    public static Task PrepararBaseDeDatosAsync(this IServiceProvider services) => services.MigrarYSembrarAsync();
}
