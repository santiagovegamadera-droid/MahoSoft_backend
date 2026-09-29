using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using MahoSoft.Entidades;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MahoSoft.Datos.Seed;

/// <summary>
/// Settings of the "Inicial" section, read only when the database has no users yet: who the first administrator
/// is. Set them as environment variables for the first start (Inicial__AdminEmail…) and remove them afterwards.
/// </summary>
public class InicialOptions
{
    public const string Seccion = "Inicial";

    public string NombreNegocio { get; set; } = "Maho Boutique";
    public string AdminNombre { get; set; } = "Administradora";
    public string AdminEmail { get; set; } = "";
    public string AdminPassword { get; set; } = "";
}

/// <summary>
/// What a real (non-development) database needs to work: the settings the forms rely on and a first administrator.
/// No sample products, sales or users. Safe to run on every start: it only adds what is missing.
/// </summary>
public static class InicioProduccion
{
    public const int PasswordMinimo = 12;

    public static async Task PrepararAsync(
        AppDbContext db,
        IPasswordHasher<Usuario> hasher,
        InicialOptions inicial,
        ILogger logger,
        CancellationToken ct = default
    )
    {
        await db.Database.MigrateAsync(ct);

        if (!await db.Negocio.AnyAsync(ct))
        {
            logger.LogInformation("Base de datos nueva: se crea la configuración inicial del negocio");
            AgregarConfiguracion(db, inicial.NombreNegocio);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Usuarios.AnyAsync(ct))
        {
            var admin = PrimeraAdministradora(inicial);
            admin.PasswordHash = hasher.HashPassword(admin, inicial.AdminPassword);
            db.Usuarios.Add(admin);
            await db.SaveChangesAsync(ct);
            logger.LogWarning(
                "Se creó la primera administradora ({Email}). Quita las variables Inicial__AdminEmail e Inicial__AdminPassword.",
                admin.Email
            );
        }
    }

    /// <summary>Default settings, the same as the sample data; all editable later in Configuración.</summary>
    private static void AgregarConfiguracion(AppDbContext db, string nombreNegocio)
    {
        db.Negocio.Add(
            new Negocio
            {
                Id = 1,
                Nombre = string.IsNullOrWhiteSpace(nombreNegocio) ? "Mi negocio" : nombreNegocio.Trim(),
                MensajeRecibo = "Gracias por tu compra",
                StockBajoProducto = 5,
                StockBajoTalla = 3,
            }
        );
        if (!db.TiposDocumento.Any())
            db.TiposDocumento.AddRange(
                new[] { "CC", "CE", "NIT", "TI", "Pasaporte" }.Select((c, i) => new TipoDocumento { Codigo = c, Orden = i })
            );
        if (!db.GruposTalla.Any())
            db.GruposTalla.AddRange(
                new[]
                {
                    ("Letras", new[] { "XS", "S", "M", "L", "XL", "XXL" }),
                    ("Numéricas", new[] { "25", "26", "27", "28", "29", "30", "32" }),
                }.Select(
                    (g, i) =>
                        new GrupoTalla
                        {
                            Nombre = g.Item1,
                            Orden = i,
                            Tallas = g.Item2.Select((v, j) => new Talla { Valor = v, Orden = j }).ToList(),
                        }
                )
            );
        if (!db.Bancos.Any())
            db.Bancos.AddRange(
                new[] { "Nequi", "Daviplata", "Bancolombia", "Davivienda", "Banco de Bogotá", "BBVA", "Otro" }.Select(
                    (b, i) => new Banco { Nombre = b, Orden = i }
                )
            );
        if (!db.DescuentosPos.Any())
            db.DescuentosPos.AddRange(new[] { 0m, 5, 10, 15, 20, 30 }.Select(p => new DescuentoPos { Porcentaje = p }));
    }

    /// <summary>Refuses to start with a weak or missing first password: the system would be open to anyone.</summary>
    private static Usuario PrimeraAdministradora(InicialOptions inicial)
    {
        var email = inicial.AdminEmail.Trim().ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out _))
            throw new ValidationException(
                "La base de datos no tiene usuarios. Define Inicial__AdminEmail e Inicial__AdminPassword para crear la primera administradora."
            );
        if (inicial.AdminPassword.Length < PasswordMinimo || inicial.AdminPassword == "Maho2026!")
            throw new ValidationException(
                $"Inicial__AdminPassword debe tener al menos {PasswordMinimo} caracteres y no puede ser la contraseña de ejemplo."
            );

        return new Usuario
        {
            Nombre = string.IsNullOrWhiteSpace(inicial.AdminNombre) ? "Administradora" : inicial.AdminNombre.Trim(),
            Email = email,
            Rol = Rol.Administradora,
            Activo = true,
            CreadoEn = DateTimeOffset.UtcNow,
            Permisos = Enum.GetValues<Permiso>().Select(p => new UsuarioPermiso { Permiso = p }).ToList(),
        };
    }
}
