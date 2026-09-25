using MahoSoft.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MahoSoft.Api.Data.Configurations;

public class CategoriaConfig : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> e)
    {
        e.Property(x => x.Nombre).HasMaxLength(100);
        e.HasIndex(x => x.Nombre).IsUnique();
        e.Property(x => x.Descripcion).HasMaxLength(500);
    }
}

public class ProductoConfig : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> e)
    {
        e.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Productos_PrecioVenta", "[PrecioVenta] >= 0");
            t.HasCheckConstraint("CK_Productos_CostoActual", "[CostoActual] >= 0");
        });
        e.Property(x => x.Nombre).HasMaxLength(150);
        e.Property(x => x.Descripcion).HasMaxLength(1000);
        e.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId);
        e.HasOne(x => x.Imagen).WithMany().HasForeignKey(x => x.ImagenId);
        e.HasMany(x => x.Colores).WithOne().HasForeignKey(c => c.ProductoId);
        e.HasMany(x => x.Tallas).WithOne().HasForeignKey(t => t.ProductoId);
    }
}

public class ProductoColorConfig : IEntityTypeConfiguration<ProductoColor>
{
    public void Configure(EntityTypeBuilder<ProductoColor> e)
    {
        e.Property(x => x.Color).HasMaxLength(50);
        e.HasIndex(x => new { x.ProductoId, x.Color }).IsUnique();
    }
}

public class ProductoTallaConfig : IEntityTypeConfiguration<ProductoTalla>
{
    public void Configure(EntityTypeBuilder<ProductoTalla> e)
    {
        e.ToTable(t => t.HasCheckConstraint("CK_ProductoTallas_Stock", "[Stock] >= 0"));
        e.HasKey(x => new { x.ProductoId, x.TallaId });
        e.HasOne(x => x.Talla).WithMany().HasForeignKey(x => x.TallaId);
    }
}

public class ArchivoConfig : IEntityTypeConfiguration<Archivo>
{
    public void Configure(EntityTypeBuilder<Archivo> e)
    {
        e.ToTable(t => t.HasCheckConstraint("CK_Archivos_Tamano", "[Tamano] >= 0"));
        e.Property(x => x.Nombre).HasMaxLength(255);
        e.Property(x => x.TipoMime).HasMaxLength(100);
        e.Property(x => x.Ubicacion).HasMaxLength(1000);
        e.Property(x => x.PublicId).HasMaxLength(255);
    }
}
