using System.Net;
using System.Net.Http.Json;

namespace MahoSoft.Pruebas;

/// <summary>Who can do what: each role reaches its own screens only.</summary>
[Collection(ApiCollection.Nombre)]
public class PermisosPruebas(ApiFixture api)
{
    [Theory]
    [InlineData("/api/productos")]
    [InlineData("/api/ventas")]
    [InlineData("/api/configuracion")]
    [InlineData("/api/usuarios")]
    public async Task Sin_sesion_nada_responde(string url) =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync(url)).StatusCode);

    [Theory]
    [InlineData("/api/compras")]
    [InlineData("/api/proveedores")]
    [InlineData("/api/usuarios")]
    [InlineData("/api/reportes?desde=2026-01-01&hasta=2026-01-31")]
    [InlineData("/api/tablero")]
    [InlineData("/api/inventario/movimientos")]
    public async Task La_vendedora_no_entra_a_lo_que_no_es_del_POS(string url) =>
        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.VendedoraAsync()).GetAsync(url)).StatusCode);

    [Theory]
    [InlineData("/api/productos")]
    [InlineData("/api/categorias")]
    [InlineData("/api/configuracion")]
    [InlineData("/api/ventas")]
    public async Task La_vendedora_ve_lo_que_necesita_para_vender(string url) =>
        Assert.Equal(HttpStatusCode.OK, (await (await api.VendedoraAsync()).GetAsync(url)).StatusCode);

    [Theory]
    [InlineData("/api/ventas")]
    [InlineData("/api/usuarios")]
    [InlineData("/api/tablero")]
    public async Task Bodega_no_entra_a_ventas_usuarios_ni_inicio(string url) =>
        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.BodegaAsync()).GetAsync(url)).StatusCode);

    [Fact]
    public async Task Bodega_lee_proveedores_pero_no_los_cambia()
    {
        var c = await api.BodegaAsync();
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/proveedores")).StatusCode);
        var crear = await c.PostAsJsonAsync("/api/proveedores", new { nombre = "X", tipoDocumento = "NIT", documento = "1", ivaPorcentaje = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, crear.StatusCode);
    }

    [Fact]
    public async Task Solo_la_administradora_cambia_la_configuracion()
    {
        var cuerpo = new { stockBajoProducto = 5, stockBajoTalla = 3 };
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await (await api.BodegaAsync()).PutAsJsonAsync("/api/configuracion/inventario", cuerpo)).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (await (await api.AdminAsync()).PutAsJsonAsync("/api/configuracion/inventario", cuerpo)).StatusCode
        );
    }

    [Fact]
    public async Task Nadie_se_desactiva_a_si_mismo()
    {
        var c = await api.AdminAsync();
        var r = await c.PutAsJsonAsync(
            "/api/usuarios/1",
            new
            {
                nombre = "Ana Martínez",
                email = "ana@ellaboutique.co",
                rol = "Administradora",
                permisos = new[] { "Dashboard", "POS", "Compras", "Proveedores", "Usuarios", "Reportes" },
                activo = false,
            }
        );
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Sin_Cloudinary_los_productos_se_listan_y_subir_fotos_responde_503()
    {
        var c = await api.AdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/productos")).StatusCode);
        var foto = new MultipartFormDataContent { { new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]), "archivo", "foto.png" } };
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.PostAsync("/api/productos/imagenes", foto)).StatusCode);
    }

    [Fact]
    public async Task Sin_correo_configurado_recuperar_la_contrasena_responde_503()
    {
        var r = await api.CreateClient().PostAsJsonAsync("/api/auth/recuperar-password", new { email = "ana@ellaboutique.co" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, r.StatusCode);
    }
}
