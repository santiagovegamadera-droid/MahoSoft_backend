namespace MahoSoft.Negocio;

/// <summary>
/// A business rule stopped the operation. The message is shown to the user as is; the API turns each kind
/// into its HTTP status.
/// </summary>
public abstract class NegocioException(string mensaje) : Exception(mensaje);

/// <summary>The data sent breaks a rule (400).</summary>
public class ValidacionException(string mensaje) : NegocioException(mensaje);

/// <summary>Wrong or missing credentials (401).</summary>
public class NoAutenticadoException(string mensaje) : NegocioException(mensaje);

/// <summary>Signed in, but not allowed to do this (403).</summary>
public class AccesoDenegadoException(string mensaje) : NegocioException(mensaje);

/// <summary>The record does not exist (404).</summary>
public class NoEncontradoException(string mensaje) : NegocioException(mensaje);

/// <summary>Clashes with data already stored, e.g. deleting a category that has products (409).</summary>
public class ConflictoException(string mensaje) : NegocioException(mensaje);
