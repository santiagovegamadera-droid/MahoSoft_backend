namespace MahoSoft.Api.Data.Entities;

/// <summary>
/// A supplier as it prints itself on its invoices. The categories it supplies aren't stored:
/// they come from the products bought from it.
/// </summary>
public class Proveedor
{
    public int Id { get; set; }

    /// <summary>Name or razón social.</summary>
    public string Nombre { get; set; } = "";

    public int TipoDocumentoId { get; set; }
    public TipoDocumento TipoDocumento { get; set; } = null!;
    public string Documento { get; set; } = "";
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }

    /// <summary>Contact person or salesperson.</summary>
    public string? Contacto { get; set; }

    /// <summary>IVA it usually charges (0, 5 or 19); new purchases start from it.</summary>
    public decimal IvaPorcentaje { get; set; }

    public bool Activo { get; set; } = true;
}
