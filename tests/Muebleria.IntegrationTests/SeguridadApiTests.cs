using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Muebleria.IntegrationTests.Apoyo;

namespace Muebleria.IntegrationTests;

/// <summary>RF-01, RF-02, RNF-02 y RNF-03 probados contra la API real.</summary>
[Collection(ApiCollection.Nombre)]
public class SeguridadApiTests(ApiFixture api)
{
    [Fact(DisplayName = "RF-01: sin sesión la API responde 401")]
    public async Task Sin_sesion()
    {
        var c = api.SinSesion();
        (await c.GetAsync("/api/articulos")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await c.GetAsync("/api/panel")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "RF-01: contraseña incorrecta da un mensaje que no revela si el usuario existe")]
    public async Task Contrasena_incorrecta()
    {
        var c = api.SinSesion();
        var malUsuario = await c.PostAsJsonAsync("/api/auth/login", new { usuario = "noexiste", contrasena = "x" });
        var malaClave = await c.PostAsJsonAsync("/api/auth/login", new { usuario = "admin", contrasena = "x" });
        malUsuario.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await malUsuario.TituloAsync()).Should().Be(await malaClave.TituloAsync()).And.Be("Usuario o contraseña incorrectos.");
    }

    [Fact(DisplayName = "RF-01: cerrar sesión invalida la cookie")]
    public async Task Cerrar_sesion()
    {
        var c = await api.ComoAsync("encargado");
        (await c.GetAsync("/api/auth/sesion")).StatusCode.Should().Be(HttpStatusCode.OK);
        await c.PostJsonAsync("/api/auth/logout");
        (await c.GetAsync("/api/auth/sesion")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory(DisplayName = "RNF-02: el trabajador recibe 403 en funciones que no le corresponden, aunque llame directo a la API")]
    [InlineData("GET", "/api/usuarios")]
    [InlineData("GET", "/api/bitacora")]
    [InlineData("GET", "/api/alertas")]
    [InlineData("GET", "/api/reportes")]
    [InlineData("GET", "/api/respaldos")]
    [InlineData("POST", "/api/respaldos")]
    [InlineData("GET", "/api/instalacion")]
    [InlineData("POST", "/api/ubicaciones")]
    [InlineData("POST", "/api/solicitudes/1/aprobar")]
    public async Task Trabajador_sin_permiso(string metodo, string url)
    {
        var c = await api.ComoAsync("trabajador");
        var r = metodo == "GET" ? await c.GetAsync(url) : await c.PostJsonAsync(url, new { nombre = "x" });
        r.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "RF-21/RNF-02: el encargado registra entradas pero no ajustes")]
    public async Task Encargado_no_ajusta()
    {
        var c = await api.ComoAsync("encargado");
        var id = await c.IdArticuloAsync("HER-BIS-35");
        (await c.PostJsonAsync("/api/movimientos", new { articuloId = id, tipo = "Entrada", cantidad = 1, motivo = "Compra" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var ajuste = await c.PostJsonAsync("/api/movimientos", new { articuloId = id, tipo = "Ajuste", cantidad = 5, motivo = "x" });
        ajuste.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ajuste.TituloAsync()).Should().Contain("Solo el administrador");
    }

    [Fact(DisplayName = "RNF-03: los datos inválidos se rechazan con mensajes en español")]
    public async Task Validacion_en_espanol()
    {
        var c = await api.ComoAsync("admin");
        var r = await c.PostJsonAsync("/api/usuarios", new { nombre = "", usuario = "con espacios", contrasena = "123", rolId = 0 });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var cuerpo = (await r.Content.ReadAsStringAsync());
        cuerpo.Should().Contain("Revisa los datos del formulario").And.Contain("al menos 8 caracteres").And.Contain("Elige un rol");
    }

    [Fact(DisplayName = "RNF-03: un intento de inyección SQL en la búsqueda se trata como texto")]
    public async Task Inyeccion_sql()
    {
        var c = await api.ComoAsync("trabajador");
        var r = await c.GetAsync("/api/articulos?texto=" + Uri.EscapeDataString("x'; DROP TABLE ARTICULO; --"));
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        (await r.LeerAsync())["total"]!.GetValue<int>().Should().Be(0);
        (await api.EscalarAsync<int>("SELECT COUNT(*) FROM ARTICULO")).Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "RF-03: la bitácora registra el inicio de sesión y los cambios con su usuario")]
    public async Task Bitacora_registra()
    {
        var c = await api.ComoAsync("admin");
        var r = await c.PostJsonAsync("/api/ubicaciones", new { nombre = "Estante prueba bitácora", activo = true });
        r.EnsureSuccessStatusCode();
        var acciones = (await c.GetJsonAsync("/api/bitacora?tamano=20"))["items"]!.AsArray().Select(b => b!["accion"]!.GetValue<string>()).ToList();
        acciones.Should().Contain(a => a.StartsWith("Crear ubicación Estante prueba bitácora"));
        acciones.Should().Contain("Iniciar sesión");
    }
}
