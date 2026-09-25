using System.Security.Claims;
using System.Threading.RateLimiting;
using MahoSoft.Api.Data;
using MahoSoft.Api.Data.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace MahoSoft.Api.Auth;

public static class RateLimits
{
    public const string Login = "login";
}

public static class AuthSetup
{
    /// <summary>
    /// JWT sign-in, one authorization policy per <see cref="Permiso"/> (use <c>[Authorize(Policy = nameof(Permiso.POS))]</c>)
    /// and a limit on login attempts. Every endpoint requires a signed-in user unless marked [AllowAnonymous].
    /// </summary>
    public static IServiceCollection AddMahoAuth(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        var jwt = config.GetSection(JwtOptions.Seccion).Get<JwtOptions>() ?? new JwtOptions();
        services.AddSingleton<TokenService>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false; // keep "sub", "role", "permiso" as they are in the token
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = TokenService.SigningKey(jwt),
                    NameClaimType = Claims.Nombre,
                    RoleClaimType = Claims.Rol,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
                o.Events = new JwtBearerEvents { OnTokenValidated = RefrescarDesdeBaseDeDatos };
            });

        services.AddAuthorization(o =>
        {
            o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            foreach (var permiso in Enum.GetValues<Permiso>())
                o.AddPolicy(permiso.ToString(), p => p.RequireClaim(Claims.Permiso, permiso.ToString()));
        });

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // 5 login attempts per minute from the same address
            o.AddPolicy(
                RateLimits.Login,
                http =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        http.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }
                    )
            );
            o.OnRejected = async (ctx, ct) =>
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "Demasiados intentos. Espera un minuto y vuelve a intentarlo.",
                };
                await ctx.HttpContext.Response.WriteAsJsonAsync(problem, ct);
            };
        });

        return services;
    }

    /// <summary>
    /// A token keeps working until it expires, so on every request the user is re-read: a deactivated user is
    /// signed out right away, and permission changes apply without logging in again.
    /// </summary>
    private static async Task RefrescarDesdeBaseDeDatos(TokenValidatedContext ctx)
    {
        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var id = int.Parse(ctx.Principal!.FindFirstValue(Claims.Id)!);
        var usuario = await db.Usuarios.AsNoTracking().Include(u => u.Permisos).SingleOrDefaultAsync(u => u.Id == id);
        if (usuario is null || !usuario.Activo)
        {
            ctx.Fail("Usuario inexistente o desactivado");
            return;
        }
        var identity = new ClaimsIdentity(
            TokenService.ClaimsDe(usuario),
            JwtBearerDefaults.AuthenticationScheme,
            Claims.Nombre,
            Claims.Rol
        );
        ctx.Principal = new ClaimsPrincipal(identity);
    }
}
