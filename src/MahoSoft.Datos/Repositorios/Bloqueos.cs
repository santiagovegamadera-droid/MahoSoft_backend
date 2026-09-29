namespace MahoSoft.Datos.Repositorios;

/// <summary>Keys of the PostgreSQL advisory locks (<c>pg_advisory_xact_lock</c>) that serialize numbering.</summary>
internal static class Bloqueos
{
    public const long NumeroCompra = 1001;
    public const long NumeroVenta = 1002;
}
