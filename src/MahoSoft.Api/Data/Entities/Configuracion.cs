namespace MahoSoft.Api.Data.Entities;

/// <summary>The store's own details (a single row): printed on receipts and shown as the buyer on purchases.</summary>
public class Negocio
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Nit { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? Instagram { get; set; }
    public string? MensajeRecibo { get; set; }

    /// <summary>A product at or below this many units counts as low stock.</summary>
    public int StockBajoProducto { get; set; }

    /// <summary>A single size at or below this many units counts as low stock.</summary>
    public int StockBajoTalla { get; set; }
}

/// <summary>ID document types offered in the forms (CC, NIT, …).</summary>
public class TipoDocumento
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>A set of sizes shown together, e.g. "Letras" (XS…XXL) or "Numéricas" (25…32).</summary>
public class GrupoTalla
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int Orden { get; set; }
    public List<Talla> Tallas { get; set; } = [];
}

public class Talla
{
    public int Id { get; set; }
    public int GrupoTallaId { get; set; }
    public GrupoTalla GrupoTalla { get; set; } = null!;

    /// <summary>The label (XS, M, 28…); unique across all groups.</summary>
    public string Valor { get; set; } = "";
    public int Orden { get; set; }
}

/// <summary>Banks and wallets offered for transfer payments at the POS.</summary>
public class Banco
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Quick discount buttons in the POS.</summary>
public class DescuentoPos
{
    public int Id { get; set; }
    public decimal Porcentaje { get; set; }
}
