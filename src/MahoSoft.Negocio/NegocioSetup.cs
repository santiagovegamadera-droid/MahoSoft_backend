using MahoSoft.Datos;
using MahoSoft.Entidades;
using MahoSoft.Negocio.Auth;
using MahoSoft.Negocio.Categorias;
using MahoSoft.Negocio.Compras;
using MahoSoft.Negocio.Configuracion;
using MahoSoft.Negocio.Correo;
using MahoSoft.Negocio.Inventario;
using MahoSoft.Negocio.Productos;
using MahoSoft.Negocio.Proveedores;
using MahoSoft.Negocio.Reportes;
using MahoSoft.Negocio.Usuarios;
using MahoSoft.Negocio.Ventas;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MahoSoft.Negocio;

public static class NegocioSetup
{
    /// <summary>The business services and, beneath them, the data layer. The API only registers this.</summary>
    public static IServiceCollection AddNegocio(this IServiceCollection services, IConfiguration config)
    {
        services.AddDatos(config);
        services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

        services.AddScoped<IAuthServicio, AuthServicio>();
        services.AddScoped<ICategoriaServicio, CategoriaServicio>();
        services.AddScoped<IConfiguracionServicio, ConfiguracionServicio>();
        services.AddScoped<IProveedorServicio, ProveedorServicio>();
        services.AddScoped<IProductoServicio, ProductoServicio>();
        services.AddScoped<ICompraServicio, CompraServicio>();
        services.AddScoped<IVentaServicio, VentaServicio>();
        services.AddScoped<IUsuarioServicio, UsuarioServicio>();
        services.AddScoped<IReporteServicio, ReporteServicio>();
        services.AddScoped<IInventarioServicio, InventarioServicio>();
        services.AddScoped<ICorreoServicio, CorreoServicio>();
        services.AddOptions<AppOptions>().Bind(config.GetSection(AppOptions.Seccion));

        return services;
    }

    /// <summary>Brings the database up to date and loads the sample data if it is empty (local development only).</summary>
    public static Task PrepararBaseDeDatosAsync(this IServiceProvider services) => services.MigrarYSembrarAsync();
}
