using MahoSoft.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MahoSoft.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Configuración
    public DbSet<Negocio> Negocio => Set<Negocio>();
    public DbSet<TipoDocumento> TiposDocumento => Set<TipoDocumento>();
    public DbSet<GrupoTalla> GruposTalla => Set<GrupoTalla>();
    public DbSet<Talla> Tallas => Set<Talla>();
    public DbSet<Banco> Bancos => Set<Banco>();
    public DbSet<DescuentoPos> DescuentosPos => Set<DescuentoPos>();

    // Usuarios
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<UsuarioPermiso> UsuarioPermisos => Set<UsuarioPermiso>();

    // Catálogo
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ProductoColor> ProductoColores => Set<ProductoColor>();
    public DbSet<ProductoTalla> ProductoTallas => Set<ProductoTalla>();

    // Compras
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<CompraItem> CompraItems => Set<CompraItem>();

    // Ventas
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<VentaItem> VentaItems => Set<VentaItem>();
    public DbSet<VentaEntrega> VentaEntregas => Set<VentaEntrega>();
    public DbSet<ComprobanteTransferencia> ComprobantesTransferencia => Set<ComprobanteTransferencia>();

    // Inventario y archivos
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();
    public DbSet<Archivo> Archivos => Set<Archivo>();

    // Children that belong to their parent and go away with it; every other relationship is Restrict,
    // so nothing with history (a product sold, a supplier with purchases…) can be deleted by accident
    private static readonly HashSet<(Type Dependent, Type Principal)> CascadeDeletes =
    [
        (typeof(CompraItem), typeof(Compra)),
        (typeof(VentaItem), typeof(Venta)),
        (typeof(VentaEntrega), typeof(Venta)),
        (typeof(ComprobanteTransferencia), typeof(Venta)),
        (typeof(ProductoColor), typeof(Producto)),
        (typeof(ProductoTalla), typeof(Producto)),
        (typeof(UsuarioPermiso), typeof(Usuario)),
    ];

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Money in pesos with cents; percentages override this to (5,2)
        builder.Properties<decimal>().HavePrecision(18, 2);
        // Explicit limits instead of nvarchar(max); long fields raise this in their configuration
        builder.Properties<string>().HaveMaxLength(200);

        // Enums are stored as their name so the tables read like the app
        builder.Properties<Rol>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<Permiso>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<CondicionPago>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<EstadoPago>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<TipoVenta>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<MetodoPago>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<EstadoVenta>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<TipoMovimiento>().HaveConversion<string>().HaveMaxLength(30);
        builder.Properties<AlmacenArchivo>().HaveConversion<string>().HaveMaxLength(30);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            var pair = (fk.DeclaringEntityType.ClrType, fk.PrincipalEntityType.ClrType);
            fk.DeleteBehavior = CascadeDeletes.Contains(pair) ? DeleteBehavior.Cascade : DeleteBehavior.Restrict;
        }
    }
}
