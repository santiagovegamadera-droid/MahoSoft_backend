using MahoSoft.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MahoSoft.Api.Data.Configurations;

public class ClienteConfig : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> e)
    {
        e.Property(x => x.Nombre).HasMaxLength(150);
        e.Property(x => x.Documento).HasMaxLength(30);
        e.Property(x => x.Telefono).HasMaxLength(30);
        e.Property(x => x.Correo).HasMaxLength(256);
        e.HasOne(x => x.TipoDocumento).WithMany().HasForeignKey(x => x.TipoDocumentoId);
        // One customer per document; customers without a document can repeat
        e.HasIndex(x => new { x.TipoDocumentoId, x.Documento }).IsUnique().HasFilter("[Documento] IS NOT NULL");
        e.HasIndex(x => x.Telefono);
    }
}

public class VentaConfig : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> e)
    {
        e.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Ventas_Descuento", "[DescuentoPorcentaje] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_Ventas_Montos", "[Envio] >= 0 AND [Subtotal] >= 0 AND [Descuento] >= 0 AND [Total] >= 0");
            // A voided sale records when it was voided and by whom
            t.HasCheckConstraint(
                "CK_Ventas_Anulacion",
                "[Estado] <> 'Anulada' OR ([AnuladaEn] IS NOT NULL AND [AnuladaPorId] IS NOT NULL)"
            );
        });
        e.Property(x => x.NumeroFactura).HasMaxLength(20);
        e.HasIndex(x => x.NumeroFactura).IsUnique();
        e.HasIndex(x => x.Fecha);
        e.Property(x => x.DescuentoPorcentaje).HasPrecision(5, 2);
        e.Property(x => x.MotivoAnulacion).HasMaxLength(300);
        e.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId);
        e.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId);
        e.HasOne(x => x.AnuladaPor).WithMany().HasForeignKey(x => x.AnuladaPorId);
        e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.VentaId);
        e.HasOne(x => x.Entrega).WithOne().HasForeignKey<VentaEntrega>(x => x.VentaId);
        e.HasOne(x => x.Comprobante).WithOne().HasForeignKey<ComprobanteTransferencia>(x => x.VentaId);
    }
}

public class VentaItemConfig : IEntityTypeConfiguration<VentaItem>
{
    public void Configure(EntityTypeBuilder<VentaItem> e)
    {
        e.ToTable(t =>
        {
            t.HasCheckConstraint("CK_VentaItems_Cantidad", "[Cantidad] > 0");
            t.HasCheckConstraint("CK_VentaItems_Precios", "[PrecioUnitario] >= 0 AND [CostoUnitario] >= 0");
        });
        e.Property(x => x.NombreProducto).HasMaxLength(150);
        e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId);
        e.HasOne(x => x.Talla).WithMany().HasForeignKey(x => x.TallaId);
    }
}

public class VentaEntregaConfig : IEntityTypeConfiguration<VentaEntrega>
{
    public void Configure(EntityTypeBuilder<VentaEntrega> e)
    {
        e.HasKey(x => x.VentaId);
        e.Property(x => x.Direccion).HasMaxLength(250);
        e.Property(x => x.Barrio).HasMaxLength(100);
        e.Property(x => x.Ciudad).HasMaxLength(100);
        e.Property(x => x.Notas).HasMaxLength(500);
    }
}

public class ComprobanteTransferenciaConfig : IEntityTypeConfiguration<ComprobanteTransferencia>
{
    public void Configure(EntityTypeBuilder<ComprobanteTransferencia> e)
    {
        e.HasKey(x => x.VentaId);
        e.Property(x => x.Referencia).HasMaxLength(100);
        e.HasOne(x => x.Banco).WithMany().HasForeignKey(x => x.BancoId);
        e.HasOne(x => x.Archivo).WithMany().HasForeignKey(x => x.ArchivoId);
    }
}

public class MovimientoInventarioConfig : IEntityTypeConfiguration<MovimientoInventario>
{
    public void Configure(EntityTypeBuilder<MovimientoInventario> e)
    {
        e.ToTable("MovimientosInventario", t => t.HasCheckConstraint("CK_MovimientosInventario_Cantidad", "[Cantidad] <> 0"));
        e.Property(x => x.Motivo).HasMaxLength(200);
        e.HasIndex(x => new { x.ProductoId, x.TallaId });
        e.HasIndex(x => x.Fecha);
        e.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId);
        e.HasOne(x => x.Talla).WithMany().HasForeignKey(x => x.TallaId);
        e.HasOne(x => x.Compra).WithMany().HasForeignKey(x => x.CompraId);
        e.HasOne(x => x.Venta).WithMany().HasForeignKey(x => x.VentaId);
        e.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId);
    }
}
