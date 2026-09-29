namespace MahoSoft.Entidades;

// Stored as text in the database so rows read the same as in the app

public enum Rol
{
    Administradora,
    Vendedora,
    Bodega,
}

/// <summary>Modules a user can open; matches the permissions in the Usuarios screen.</summary>
public enum Permiso
{
    Dashboard,
    POS,
    Compras,
    Proveedores,
    Usuarios,
    Reportes,
}

public enum CondicionPago
{
    Contado,
    Credito,
}

public enum EstadoPago
{
    Pagada,
    Pendiente,
}

public enum TipoVenta
{
    Tienda,
    Pedido,
}

public enum MetodoPago
{
    Efectivo,
    Tarjeta,
    Transferencia,
}

public enum EstadoVenta
{
    Registrada,
    Anulada,
}

public enum TipoMovimiento
{
    Entrada,
    Salida,
    Ajuste,
}

/// <summary>
/// Where a file lives: product photos in Cloudinary; invoice PDFs and transfer receipts in Supabase Storage, or on the
/// server disk when it is not configured (local development).
/// </summary>
public enum AlmacenArchivo
{
    Cloudinary,
    Local,
    Supabase,
    /// <summary>A plain external URL (only the sample data uses it).</summary>
    Externo,
}
