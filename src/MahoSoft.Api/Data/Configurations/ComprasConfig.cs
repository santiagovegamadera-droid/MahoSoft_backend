using MahoSoft.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MahoSoft.Api.Data.Configurations;

public class ProveedorConfig : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> e)
    {
        e.ToTable("Proveedores", t => t.HasCheckConstraint("CK_Proveedores_Iva", "[IvaPorcentaje] BETWEEN 0 AND 100"));
        e.Property(x => x.Documento).HasMaxLength(30);
        e.Property(x => x.Telefono).HasMaxLength(30);
        e.Property(x => x.Email).HasMaxLength(256);
        e.Property(x => x.Ciudad).HasMaxLength(100);
        e.Property(x => x.Direccion).HasMaxLength(250);
        e.Property(x => x.Contacto).HasMaxLength(150);
        e.Property(x => x.IvaPorcentaje).HasPrecision(5, 2);
        e.HasOne(x => x.TipoDocumento).WithMany().HasForeignKey(x => x.TipoDocumentoId);
        e.HasIndex(x => new { x.TipoDocumentoId, x.Documento }).IsUnique();
    }
}

public class CompraConfig : IEntityTypeConfiguration<Compra>
{
    public void Configure(EntityTypeBuilder<Compra> e)
    {
        e.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Compras_Iva", "[IvaPorcentaje] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_Compras_Montos", "[Descuento] >= 0 AND [Subtotal] >= 0 AND [Iva] >= 0 AND [Total] >= 0");
            // Credit purchases need a due date
            t.HasCheckConstraint(
                "CK_Compras_Vencimiento",
                "[CondicionPago] <> 'Credito' OR [FechaVencimiento] IS NOT NULL"
            );
        });
        e.Property(x => x.Numero).HasMaxLength(20);
        e.HasIndex(x => x.Numero).IsUnique();
        e.Property(x => x.TipoComprobante).HasMaxLength(60);
        e.Property(x => x.NumeroComprobante).HasMaxLength(50);
        // The same supplier invoice can't be registered twice
        e.HasIndex(x => new { x.ProveedorId, x.NumeroComprobante }).IsUnique();
        e.Property(x => x.VendedorProveedor).HasMaxLength(150);
        e.Property(x => x.Cufe).HasMaxLength(200);
        e.Property(x => x.IvaPorcentaje).HasPrecision(5, 2);
        e.Property(x => x.Notas).HasMaxLength(1000);
        e.HasIndex(x => x.FechaComprobante);
        e.HasOne(x => x.Proveedor).WithMany().HasForeignKey(x => x.ProveedorId);
        e.HasOne(x => x.Documento).WithMany().HasForeignKey(x => x.DocumentoId);
        e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId);
        e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.CompraId);
    }
}

public class CompraItemConfig : IEntityTypeConfiguration<CompraItem>
{
    public void Configure(EntityTypeBuilder<CompraItem> e)
    {
        e.ToTable(t =>
        {
            t.HasCheckConstraint("CK_CompraItems_Cantidad", "[Cantidad] > 0");
            t.HasCheckConstraint("CK_CompraItems_Precios", "[PrecioUnitario] >= 0 AND [CostoUnitario] >= 0");
        });
        e.Property(x => x.ReferenciaProveedor).HasMaxLength(60);
        e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId);
        e.HasOne(x => x.Talla).WithMany().HasForeignKey(x => x.TallaId);
    }
}
