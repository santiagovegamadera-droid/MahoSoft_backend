using System.Net;
using System.Net.Http.Json;

namespace MahoSoft.Pruebas;

/// <summary>
/// Access: the system has a single user, the administrator, who reaches every screen; without a session nothing
/// answers, and there is no way to create other users.
/// </summary>
[Collection(ApiCollection.Nombre)]
public class PermisosPruebas(ApiFixture api)
{
    [Theory]
    [InlineData("/api/productos")]
    [InlineData("/api/ventas")]
    [InlineData("/api/configuracion")]
    [InlineData("/api/perfil")]
    public async Task Sin_sesion_nada_responde(string url) =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync(url)).StatusCode);

    [Theory]
    [InlineData("/api/tablero")]
    [InlineData("/api/productos")]
    [InlineData("/api/categorias")]
    [InlineData("/api/proveedores")]
    [InlineData("/api/compras")]
    [InlineData("/api/ventas")]
    [InlineData("/api/reportes?desde=2026-01-01&hasta=2026-01-31")]
    [InlineData("/api/inventario/movimientos")]
    [InlineData("/api/configuracion")]
    [InlineData("/api/perfil")]
    public async Task El_administrador_entra_a_todo(string url) =>
        Assert.Equal(HttpStatusCode.OK, (await (await api.AdminAsync()).GetAsync(url)).StatusCode);

    [Fact]
    public async Task No_se_pueden_crear_ni_listar_otros_usuarios()
    {
        var c = await api.AdminAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/usuarios")).StatusCode);
        var crear = await c.PostAsJsonAsync(
            "/api/usuarios",
            new { nombre = "Otra", email = "otra@tienda.com", rol = "Administradora", password = "Una-Clave-Larga-1" }
        );
        Assert.Equal(HttpStatusCode.NotFound, crear.StatusCode);
        Assert.Equal(1, await ApiFixture.SqlAsync<int>("SELECT COUNT(*) FROM \"Usuarios\""));
    }

    [Fact]
    public async Task El_administrador_edita_su_perfil()
    {
        var c = await api.AdminAsync();
        var r = await c.PutAsJsonAsync(
            "/api/perfil",
            new { nombre = "Ana Martínez", email = "ana@ellaboutique.co", telefono = "3001234567" }
        );
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("3001234567", (await r.LeerAsync())["telefono"]!.GetValue<string>());
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
