using System.ComponentModel.DataAnnotations;

namespace MahoSoft.Api.Auth;

/// <summary>Settings of the "Jwt" section. The key is secret: user-secrets in development, an environment variable in production.</summary>
public class JwtOptions
{
    public const string Seccion = "Jwt";

    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";

    /// <summary>Signing key (HMAC-SHA256); at least 32 characters.</summary>
    [Required, MinLength(32)]
    public string Key { get; set; } = "";

    /// <summary>How long a session lasts before logging in again; a store shift by default.</summary>
    [Range(1, 168)]
    public int ExpiraHoras { get; set; } = 12;
}
