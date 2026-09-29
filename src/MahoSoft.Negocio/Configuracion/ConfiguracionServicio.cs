using MahoSoft.Datos.Repositorios;
using MahoSoft.Entidades;

namespace MahoSoft.Negocio.Configuracion;

public interface IConfiguracionServicio
{
    Task<ConfiguracionDto> ObtenerAsync(CancellationToken ct = default);

    Task<ConfiguracionDto> GuardarNegocioAsync(NegocioRequest req, CancellationToken ct = default);

    Task<ConfiguracionDto> GuardarInventarioAsync(InventarioRequest req, CancellationToken ct = default);

    Task<ConfiguracionDto> GuardarPosAsync(PosRequest req, CancellationToken ct = default);

    Task<ConfiguracionDto> GuardarTallasAsync(TallasRequest req, CancellationToken ct = default);

    Task<ConfiguracionDto> GuardarTiposDocumentoAsync(TiposDocumentoRequest req, CancellationToken ct = default);
}

/// <summary>
/// Each section is saved whole, as the list the user sees. Items that left the list are deleted, except
/// those with history: a bank or document type already used is deactivated instead, and a size in use
/// can't be removed.
/// </summary>
public class ConfiguracionServicio(IConfiguracionRepositorio config, IUnidadDeTrabajo unidad) : IConfiguracionServicio
{
    // Names are matched ignoring case, as the database's unique indexes do
    private static readonly StringComparer Nombres = StringComparer.OrdinalIgnoreCase;

    public async Task<ConfiguracionDto> ObtenerAsync(CancellationToken ct = default)
    {
        var negocio = await config.ObtenerNegocioAsync(ct);
        var descuentos = await config.ListarDescuentosAsync(ct);
        var bancos = await config.ListarBancosAsync(ct);
        var grupos = await config.ListarGruposTallaAsync(ct);
        var tipos = await config.ListarTiposDocumentoAsync(ct);

        return new ConfiguracionDto(
            negocio.Nombre,
            negocio.Nit ?? "",
            negocio.Direccion ?? "",
            negocio.Ciudad ?? "",
            negocio.Telefono ?? "",
            negocio.Correo ?? "",
            negocio.Instagram ?? "",
            negocio.MensajeRecibo ?? "",
            negocio.StockBajoProducto,
            negocio.StockBajoTalla,
            descuentos.Select(d => (int)d.Porcentaje).ToArray(),
            bancos.Where(b => b.Activo).Select(b => b.Nombre).ToArray(),
            grupos.Select(g => new GrupoTallaDto(g.Nombre, g.Tallas.Select(t => t.Valor).ToArray())).ToArray(),
            tipos.Where(t => t.Activo).Select(t => t.Codigo).ToArray()
        );
    }

    public async Task<ConfiguracionDto> GuardarNegocioAsync(NegocioRequest req, CancellationToken ct = default)
    {
        var nombre = req.Nombre.Trim();
        if (nombre.Length == 0)
            throw new ValidacionException("El nombre es obligatorio");

        var negocio = await config.ObtenerNegocioAsync(ct);
        negocio.Nombre = nombre;
        negocio.Nit = Texto(req.Nit);
        negocio.Direccion = Texto(req.Direccion);
        negocio.Ciudad = Texto(req.Ciudad);
        negocio.Telefono = Texto(req.Telefono);
        negocio.Correo = Texto(req.Correo);
        negocio.Instagram = Texto(req.Instagram);
        negocio.MensajeRecibo = Texto(req.MensajeRecibo);
        return await GuardarAsync(ct);
    }

    public async Task<ConfiguracionDto> GuardarInventarioAsync(InventarioRequest req, CancellationToken ct = default)
    {
        var negocio = await config.ObtenerNegocioAsync(ct);
        negocio.StockBajoProducto = req.StockBajoProducto;
        negocio.StockBajoTalla = req.StockBajoTalla;
        return await GuardarAsync(ct);
    }

    public async Task<ConfiguracionDto> GuardarPosAsync(PosRequest req, CancellationToken ct = default)
    {
        if (req.Descuentos.Any(d => d is < 0 or > 100))
            throw new ValidacionException("Cada descuento debe ser un número entero entre 0 y 100");
        var nuevos = req.Descuentos.Distinct().ToHashSet();
        var descuentos = await config.ListarDescuentosAsync(ct);
        foreach (var d in descuentos.Where(d => !nuevos.Contains((int)d.Porcentaje)))
            config.Eliminar(d);
        foreach (var p in nuevos.Except(descuentos.Select(d => (int)d.Porcentaje)))
            config.Agregar(new DescuentoPos { Porcentaje = p });

        var nombres = Lista(req.Bancos, "banco o billetera", 100);
        var bancos = await config.ListarBancosAsync(ct);
        await SincronizarAsync(
            bancos,
            nombres,
            b => b.Nombre,
            nombre => new Banco { Nombre = nombre },
            (b, nombre, orden) =>
            {
                b.Nombre = nombre;
                b.Orden = orden;
                b.Activo = true;
            },
            b => config.BancoEnUsoAsync(b.Id, ct),
            b => b.Activo = false
        );
        return await GuardarAsync(ct);
    }

    public async Task<ConfiguracionDto> GuardarTiposDocumentoAsync(
        TiposDocumentoRequest req,
        CancellationToken ct = default
    )
    {
        var codigos = Lista(req.TiposDocumento, "tipo de documento", 20);
        var tipos = await config.ListarTiposDocumentoAsync(ct);
        await SincronizarAsync(
            tipos,
            codigos,
            t => t.Codigo,
            codigo => new TipoDocumento { Codigo = codigo },
            (t, codigo, orden) =>
            {
                t.Codigo = codigo;
                t.Orden = orden;
                t.Activo = true;
            },
            t => config.TipoDocumentoEnUsoAsync(t.Id, ct),
            t => t.Activo = false
        );
        return await GuardarAsync(ct);
    }

    public async Task<ConfiguracionDto> GuardarTallasAsync(TallasRequest req, CancellationToken ct = default)
    {
        var pedidos = req
            .Tallas.Select(g => new
            {
                Nombre = (g.Nombre ?? "").Trim(),
                Valores = (g.Valores ?? []).Select(v => (v ?? "").Trim().ToUpperInvariant()).ToList(),
            })
            .ToList();
        if (pedidos.Count == 0)
            throw new ValidacionException("Agrega al menos un grupo de tallas");
        if (pedidos.Any(g => g.Nombre.Length == 0))
            throw new ValidacionException("Cada grupo necesita un nombre");
        if (pedidos.Any(g => g.Nombre.Length > 50))
            throw new ValidacionException("El nombre de un grupo admite hasta 50 caracteres");
        if (pedidos.Select(g => g.Nombre).Distinct(Nombres).Count() != pedidos.Count)
            throw new ValidacionException("Hay dos grupos con el mismo nombre");
        if (pedidos.Any(g => g.Valores.Count == 0))
            throw new ValidacionException("Cada grupo necesita al menos una talla");
        if (pedidos.SelectMany(g => g.Valores).Any(v => v.Length is 0 or > 20))
            throw new ValidacionException("Cada talla debe tener entre 1 y 20 caracteres");
        var repetida = pedidos.SelectMany(g => g.Valores).GroupBy(v => v).FirstOrDefault(v => v.Count() > 1);
        if (repetida is not null)
            throw new ValidacionException($"La talla {repetida.Key} está repetida");

        var grupos = await config.ListarGruposTallaAsync(ct);
        var tallas = grupos.SelectMany(g => g.Tallas).ToDictionary(t => t.Valor, Nombres);
        var quedan = pedidos.SelectMany(g => g.Valores).ToHashSet(Nombres);

        // Sizes that left every group: only if nothing refers to them yet
        foreach (var talla in tallas.Values.Where(t => !quedan.Contains(t.Valor)))
        {
            if (await config.TallaEnUsoAsync(talla.Id, ct))
                throw new ConflictoException(
                    $"No se puede quitar la talla {talla.Valor}: ya la usan productos, compras o ventas."
                );
            config.Eliminar(talla);
        }

        var porNombre = grupos.ToDictionary(g => g.Nombre, Nombres);
        for (var i = 0; i < pedidos.Count; i++)
        {
            var pedido = pedidos[i];
            if (!porNombre.Remove(pedido.Nombre, out var grupo))
            {
                grupo = new GrupoTalla();
                config.Agregar(grupo);
            }
            grupo.Nombre = pedido.Nombre;
            grupo.Orden = i;

            // A size keeps its row (and its stock) even when it moves to another group
            for (var j = 0; j < pedido.Valores.Count; j++)
            {
                if (!tallas.TryGetValue(pedido.Valores[j], out var talla))
                {
                    talla = new Talla();
                    config.Agregar(talla);
                }
                talla.Valor = pedido.Valores[j];
                talla.GrupoTalla = grupo;
                talla.Orden = j;
            }
        }
        // Groups no longer in the list; their sizes were deleted or moved above
        foreach (var grupo in porNombre.Values)
            config.Eliminar(grupo);

        return await GuardarAsync(ct);
    }

    private async Task<ConfiguracionDto> GuardarAsync(CancellationToken ct)
    {
        await unidad.GuardarCambiosAsync(ct);
        return await ObtenerAsync(ct);
    }

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>Trims a list of names and checks it isn't empty, too long or repeated.</summary>
    private static List<string> Lista(string[] valores, string que, int largoMaximo)
    {
        var lista = valores.Select(v => (v ?? "").Trim()).ToList();
        if (lista.Count == 0)
            throw new ValidacionException($"Agrega al menos un {que}");
        if (lista.Any(v => v.Length == 0 || v.Length > largoMaximo))
            throw new ValidacionException($"Cada {que} debe tener entre 1 y {largoMaximo} caracteres");
        var repetido = lista.GroupBy(v => v, Nombres).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            throw new ValidacionException($"{repetido.Key} está repetido");
        return lista;
    }

    /// <summary>
    /// Makes the stored rows match <paramref name="nombres"/>: existing ones are updated (and reactivated) in
    /// the new order, new ones added, and missing ones deleted, or deactivated when something refers to them.
    /// </summary>
    private async Task SincronizarAsync<T>(
        List<T> actuales,
        List<string> nombres,
        Func<T, string> nombreDe,
        Func<string, T> crear,
        Action<T, string, int> aplicar,
        Func<T, Task<bool>> enUso,
        Action<T> desactivar
    )
        where T : class
    {
        var porNombre = actuales.ToDictionary(nombreDe, Nombres);
        for (var i = 0; i < nombres.Count; i++)
        {
            if (!porNombre.Remove(nombres[i], out var fila))
            {
                fila = crear(nombres[i]);
                config.Agregar(fila);
            }
            aplicar(fila, nombres[i], i);
        }
        foreach (var fila in porNombre.Values)
        {
            if (await enUso(fila))
                desactivar(fila);
            else
                config.Eliminar(fila);
        }
    }
}
