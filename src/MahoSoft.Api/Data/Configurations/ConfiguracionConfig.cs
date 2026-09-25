using MahoSoft.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MahoSoft.Api.Data.Configurations;

public class NegocioConfig : IEntityTypeConfiguration<Negocio>
{
    public void Configure(EntityTypeBuilder<Negocio> e)
    {
        e.ToTable("Negocio", t =>
        {
            // The store's details live in exactly one row
            t.HasCheckConstraint("CK_Negocio_UnaFila", "[Id] = 1");
            t.HasCheckConstraint("CK_Negocio_Umbrales", "[StockBajoProducto] >= 0 AND [StockBajoTalla] >= 0");
        });
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Nombre).HasMaxLength(150);
        e.Property(x => x.Nit).HasMaxLength(30);
        e.Property(x => x.Telefono).HasMaxLength(30);
        e.Property(x => x.Correo).HasMaxLength(256);
        e.Property(x => x.Instagram).HasMaxLength(100);
        e.Property(x => x.MensajeRecibo).HasMaxLength(500);
    }
}

public class TipoDocumentoConfig : IEntityTypeConfiguration<TipoDocumento>
{
    public void Configure(EntityTypeBuilder<TipoDocumento> e)
    {
        e.Property(x => x.Codigo).HasMaxLength(20);
        e.HasIndex(x => x.Codigo).IsUnique();
    }
}

public class GrupoTallaConfig : IEntityTypeConfiguration<GrupoTalla>
{
    public void Configure(EntityTypeBuilder<GrupoTalla> e)
    {
        e.Property(x => x.Nombre).HasMaxLength(50);
        e.HasIndex(x => x.Nombre).IsUnique();
        e.HasMany(x => x.Tallas).WithOne(t => t.GrupoTalla).HasForeignKey(t => t.GrupoTallaId);
    }
}

public class TallaConfig : IEntityTypeConfiguration<Talla>
{
    public void Configure(EntityTypeBuilder<Talla> e)
    {
        e.Property(x => x.Valor).HasMaxLength(20);
        // Stock and sales refer to a size by its label, so a label can't repeat across groups
        e.HasIndex(x => x.Valor).IsUnique();
    }
}

public class BancoConfig : IEntityTypeConfiguration<Banco>
{
    public void Configure(EntityTypeBuilder<Banco> e)
    {
        e.Property(x => x.Nombre).HasMaxLength(100);
        e.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class DescuentoPosConfig : IEntityTypeConfiguration<DescuentoPos>
{
    public void Configure(EntityTypeBuilder<DescuentoPos> e)
    {
        e.ToTable("DescuentosPos", t => t.HasCheckConstraint("CK_DescuentosPos_Rango", "[Porcentaje] BETWEEN 0 AND 100"));
        e.Property(x => x.Porcentaje).HasPrecision(5, 2);
        e.HasIndex(x => x.Porcentaje).IsUnique();
    }
}

public class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> e)
    {
        e.Property(x => x.Nombre).HasMaxLength(150);
        e.Property(x => x.Email).HasMaxLength(256);
        e.HasIndex(x => x.Email).IsUnique();
        e.Property(x => x.PasswordHash).HasMaxLength(500);
        e.Property(x => x.Telefono).HasMaxLength(30);
        e.Property(x => x.Documento).HasMaxLength(30);
        e.HasMany(x => x.Permisos).WithOne().HasForeignKey(p => p.UsuarioId);
    }
}

public class UsuarioPermisoConfig : IEntityTypeConfiguration<UsuarioPermiso>
{
    public void Configure(EntityTypeBuilder<UsuarioPermiso> e) => e.HasKey(x => new { x.UsuarioId, x.Permiso });
}
