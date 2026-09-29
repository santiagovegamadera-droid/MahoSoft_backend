using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MahoSoft.Negocio.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MahoSoft.Api.Auth;

/// <summary>Claim names in the token.</summary>
public static class Claims
{
    public const string Id = JwtRegisteredClaimNames.Sub;
    public const string Nombre = "name";
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Rol = "role";
    public const string Permiso = "permiso";
}

public record SesionResponse(string Token, DateTimeOffset ExpiraEn, UsuarioSesion Usuario);

public class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _jwt = options.Value;

    public static SymmetricSecurityKey SigningKey(JwtOptions jwt) => new(Encoding.UTF8.GetBytes(jwt.Key));

    public SesionResponse Crear(UsuarioSesion usuario)
    {
        var expira = DateTimeOffset.UtcNow.AddHours(_jwt.ExpiraHoras);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: ClaimsDe(usuario),
            expires: expira.UtcDateTime,
            signingCredentials: new SigningCredentials(SigningKey(_jwt), SecurityAlgorithms.HmacSha256)
        );
        return new SesionResponse(new JwtSecurityTokenHandler().WriteToken(token), expira, usuario);
    }

    /// <summary>Identity claims of a user; also used to refresh them from the database on every request.</summary>
    public static IEnumerable<Claim> ClaimsDe(UsuarioSesion u) =>
        [
            new(Claims.Id, u.Id.ToString()),
            new(Claims.Nombre, u.Nombre),
            new(Claims.Email, u.Email),
            new(Claims.Rol, u.Rol),
            .. u.Permisos.Select(p => new Claim(Claims.Permiso, p)),
        ];
}

public static class ClaimsPrincipalExtensions
{
    public static int UsuarioId(this ClaimsPrincipal user) => int.Parse(user.FindFirstValue(Claims.Id)!);
}
