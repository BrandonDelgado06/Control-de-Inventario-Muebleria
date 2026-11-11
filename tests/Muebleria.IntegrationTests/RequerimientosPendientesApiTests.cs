using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Muebleria.IntegrationTests.Apoyo;

namespace Muebleria.IntegrationTests;

/// <summary>RF-08, RF-11, RF-13, RF-15, RF-26 a RF-29 y RF-33 contra SQL Server real.</summary>
[Collection(ApiCollection.Nombre)]
public class RequerimientosPendientesApiTests(ApiFixture api)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];

    private static MultipartFormDataContent Archivo(byte[] bytes, string nombre, string tipo)
    {
        var contenido = new ByteArrayContent(bytes);
        contenido.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        return new MultipartFormDataContent { { contenido, "archivo", nombre } };
    }

    [Fact(DisplayName = "RF-08: el encargado sube la foto, todos la ven y el trabajador no la puede cambiar")]
    public async Task Foto_del_articulo()
    {
        var encargado = await api.ComoAsync("encargado");
        var trabajador = await api.ComoAsync("trabajador");
        var id = await encargado.IdArticuloAsync("HER-BIS-35");

        (await encargado.PostAsync($"/api/articulos/{id}/foto", Archivo(Png, "bisagra.png", "image/png"))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await trabajador.GetJsonAsync($"/api/articulos/{id}"))["tieneFoto"]!.GetValue<bool>().Should().BeTrue();
        var foto = await trabajador.GetAsync($"/api/articulos/{id}/foto");
        foto.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await foto.Content.ReadAsByteArrayAsync()).Should().Equal(Png);

        (await trabajador.PostAsync($"/api/articulos/{id}/foto", Archivo(Png, "x.png", "image/png"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var falsa = await encargado.PostAsync($"/api/articulos/{id}/foto", Archivo("no soy una foto"u8.ToArray(), "virus.jpg", "image/jpeg"));
        falsa.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await falsa.TituloAsync()).Should().Contain("JPG, PNG o WEBP");

        (await encargado.DeleteAsync($"/api/articulos/{id}/foto")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await trabajador.GetAsync($"/api/articulos/{id}/foto")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "RF-26: la merma resta existencia y el trabajador no la puede registrar")]
    public async Task Merma()
    {
        var encargado = await api.ComoAsync("encargado");
        var trabajador = await api.ComoAsync("trabajador");
        var id = await encargado.IdArticuloAsync("LIJ-220");
        var antes = await encargado.ExistenciaAsync(id);

        (await trabajador.PostJsonAsync("/api/bajas/merma", new { articuloId = id, cantidad = 1, causa = "Desperdicio" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var m = await (await encargado.PostJsonAsync("/api/bajas/merma", new { articuloId = id, cantidad = 2, causa = "Danio", detalle = "rotas" })).LeerAsync();
        m["tipo"]!.GetValue<string>().Should().Be("Merma");
        m["motivo"]!.GetValue<string>().Should().Be("Merma por daño: rotas");
        (await encargado.ExistenciaAsync(id)).Should().Be(antes - 2);
        (await encargado.GetJsonAsync("/api/bajas?tipo=Merma"))["items"]!.AsArray().Should().Contain(x => x!["id"]!.GetValue<int>() == m["id"]!.GetValue<int>());
        (await api.EscalarAsync<int>("SELECT COUNT(*) FROM BITACORA WHERE modulo = 'Bajas y mermas'")).Should().BePositive();
    }

    [Fact(DisplayName = "RF-27 y RF-11: al dar de baja la última unidad, el estado cambia y queda en el historial")]
    public async Task Baja_de_herramienta()
    {
        var encargado = await api.ComoAsync("encargado");
        var id = await encargado.IdArticuloAsync("HMA-FOR-01");
        var existencia = await encargado.ExistenciaAsync(id);

        (await encargado.PostJsonAsync("/api/bajas/herramienta", new { articuloId = id, cantidad = existencia, motivo = "Hoja quebrada" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var detalle = await encargado.GetJsonAsync($"/api/articulos/{id}");
        detalle["articulo"]!["estado"]!.GetValue<string>().Should().Be("DeBaja");
        detalle["articulo"]!["activo"]!.GetValue<bool>().Should().BeFalse();
        var estados = (await encargado.GetJsonAsync($"/api/articulos/{id}/estados")).AsArray();
        estados[0]!["estadoNuevo"]!.GetValue<string>().Should().Be("DeBaja");
        // En la base queda con el texto del diccionario de datos.
        (await api.EscalarAsync<string>("SELECT estado FROM ARTICULO WHERE id_articulo = @id", ("@id", id))).Should().Be("De baja");

        var material = await encargado.IdArticuloAsync("TAB-MDF-15");
        (await encargado.PostJsonAsync("/api/bajas/herramienta", new { articuloId = material, cantidad = 1, motivo = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "RF-28 y RF-29: conteo físico, diferencia calculada en SQL Server y ajuste solo por el administrador")]
    public async Task Conteo_fisico()
    {
        var encargado = await api.ComoAsync("encargado");
        var admin = await api.ComoAsync("admin");
        var herrajes = (await encargado.GetJsonAsync("/api/categorias")).AsArray()
            .First(c => c!["nombre"]!.GetValue<string>() == "Herrajes")!["id"]!.GetValue<int>();

        var conteo = await (await encargado.PostJsonAsync("/api/conteos", new { categoriaId = herrajes })).LeerAsync();
        var cid = conteo["conteo"]!["id"]!.GetValue<int>();
        var detalles = conteo["detalles"]!.AsArray();
        detalles.Should().HaveCount(2);
        (await encargado.PostJsonAsync("/api/conteos", new { categoriaId = herrajes })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var bisagra = detalles.First(d => d!["codigo"]!.GetValue<string>() == "HER-BIS-35")!;
        var corredera = detalles.First(d => d!["codigo"]!.GetValue<string>() == "HER-COR-45")!;
        var cantidades = new[]
        {
            new { detalleId = bisagra["id"]!.GetValue<int>(), cantidad = bisagra["stockSistema"]!.GetValue<decimal>() - 5 },
            new { detalleId = corredera["id"]!.GetValue<int>(), cantidad = corredera["stockSistema"]!.GetValue<decimal>() },
        };
        (await encargado.PutAsJsonAsync($"/api/conteos/{cid}", new { cantidades })).EnsureSuccessStatusCode();
        (await api.EscalarAsync<decimal>("SELECT diferencia FROM CONTEO_DETALLE WHERE id_conteo_detalle = @id", ("@id", bisagra["id"]!.GetValue<int>())))
            .Should().Be(-5);

        (await encargado.PostJsonAsync($"/api/conteos/{cid}/aplicar")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var idBisagra = bisagra["articuloId"]!.GetValue<int>();
        var antes = await admin.ExistenciaAsync(idBisagra);
        var aplicado = await (await admin.PostJsonAsync($"/api/conteos/{cid}/aplicar")).LeerAsync();
        aplicado["conteo"]!["estado"]!.GetValue<string>().Should().Be("Aplicado");
        (await admin.ExistenciaAsync(idBisagra)).Should().Be(antes - 5);
        (await api.EscalarAsync<int>("SELECT COUNT(*) FROM MOVIMIENTO WHERE tipo = 'Ajuste' AND motivo LIKE @m", ("@m", $"Conteo físico #{cid}:%"))).Should().Be(1);
        (await api.EscalarAsync<int>("SELECT COUNT(*) FROM BITACORA WHERE modulo = 'Inventario físico' AND accion = @a", ("@a", $"Aplicar conteo físico #{cid}")))
            .Should().Be(1);
    }

    [Fact(DisplayName = "RF-13: los acabados vienen agrupados por producto con sus lotes")]
    public async Task Acabados_por_lote()
    {
        var trabajador = await api.ComoAsync("trabajador");
        var grupos = (await trabajador.GetJsonAsync("/api/articulos/acabados")).AsArray();
        grupos.Should().NotBeEmpty().And.OnlyContain(g => g!["lotes"]!.AsArray().Count > 0);
        grupos.SelectMany(g => g!["lotes"]!.AsArray()).Should().Contain(l => l!["lote"]!.GetValue<string>() == "L-2410");
    }

    [Fact(DisplayName = "RF-15 y RF-33: filtro por unidad de medida y reporte de movimientos por artículo y responsable")]
    public async Task Filtros_unidad_y_reporte()
    {
        var admin = await api.ComoAsync("admin");
        var planchas = (await admin.GetJsonAsync("/api/articulos?unidadMedida=plancha"))["items"]!.AsArray();
        planchas.Should().NotBeEmpty().And.OnlyContain(a => a!["unidadMedida"]!.GetValue<string>() == "plancha");

        var id = await admin.IdArticuloAsync("TAB-MEL-18");
        (await admin.PostJsonAsync("/api/movimientos", new { articuloId = id, tipo = "Entrada", cantidad = 1, motivo = "Compra para reporte" })).EnsureSuccessStatusCode();
        var adminId = (await admin.GetJsonAsync("/api/auth/sesion"))["id"]!.GetValue<int>();
        (await admin.GetJsonAsync("/api/reportes/responsables")).AsArray().Should().Contain(r => r!["id"]!.GetValue<int>() == adminId);

        var reporte = await admin.GetJsonAsync($"/api/reportes/movimientos?articuloId={id}&usuarioId={adminId}");
        reporte["filas"]!.AsArray().Should().NotBeEmpty();
        reporte["filtros"]!.AsArray().Select(f => f!.GetValue<string>()).Should()
            .Contain(f => f.StartsWith("Artículo:")).And.Contain(f => f.StartsWith("Responsable:"));
    }
}
