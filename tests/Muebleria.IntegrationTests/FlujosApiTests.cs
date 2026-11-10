using System.Net;
using FluentAssertions;
using Muebleria.IntegrationTests.Apoyo;

namespace Muebleria.IntegrationTests;

/// <summary>Movimientos, solicitudes, herramientas, alertas, reportes y respaldos contra SQL Server real.</summary>
[Collection(ApiCollection.Nombre)]
public class FlujosApiTests(ApiFixture api)
{
    [Fact(DisplayName = "RNF-06: diez salidas simultáneas nunca dejan la existencia en negativo")]
    public async Task Salidas_simultaneas_no_dejan_negativo()
    {
        var admin = await api.ComoAsync("admin");
        var id = await admin.IdArticuloAsync("MAD-PIN-001");
        // Existencia controlada: 20 piezas.
        (await admin.PostJsonAsync("/api/movimientos", new { articuloId = id, tipo = "Ajuste", cantidad = 20, motivo = "Preparar prueba" })).EnsureSuccessStatusCode();

        var clientes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => api.ComoAsync("encargado")));
        var respuestas = await Task.WhenAll(clientes.Select(c =>
            c.PostJsonAsync("/api/movimientos", new { articuloId = id, tipo = "Salida", cantidad = 3, motivo = "Prueba concurrente" })));

        var exitosas = respuestas.Count(r => r.StatusCode == HttpStatusCode.OK);
        respuestas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.BadRequest || r.StatusCode == HttpStatusCode.Conflict);
        exitosas.Should().BeInRange(1, 6, "solo caben 6 salidas de 3 en 20 piezas");

        var final = await admin.ExistenciaAsync(id);
        final.Should().Be(20 - 3 * exitosas).And.BeGreaterThanOrEqualTo(0);
        // Cada salida guardada tiene su movimiento: existencia y movimientos siempre cuadran.
        (await api.EscalarAsync<int>("SELECT COUNT(*) FROM MOVIMIENTO WHERE id_articulo = @id AND motivo = 'Prueba concurrente'", ("@id", id))).Should().Be(exitosas);
    }

    [Fact(DisplayName = "RNF-06: la base de datos misma impide una existencia negativa")]
    public async Task Check_de_base_de_datos()
    {
        var intento = async () => await api.EscalarAsync<int>("UPDATE ARTICULO SET stock_actual = -1 WHERE codigo = 'LIJ-080'; SELECT 1");
        await intento.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>().WithMessage("*CK_ARTICULO_stock_actual*");
    }

    [Fact(DisplayName = "RF-23 a RF-25: flujo completo de una solicitud entre trabajador y encargado")]
    public async Task Flujo_de_solicitud()
    {
        var trabajador = await api.ComoAsync("trabajador");
        var encargado = await api.ComoAsync("encargado");
        var id = await trabajador.IdArticuloAsync("LIJ-080");
        var antes = await trabajador.ExistenciaAsync(id);

        var creada = await (await trabajador.PostJsonAsync("/api/solicitudes", new { articuloId = id, cantidad = 4, motivo = "Lijar cómoda" })).LeerAsync();
        var sid = creada["id"]!.GetValue<int>();
        creada["estado"]!.GetValue<string>().Should().Be("Pendiente");

        (await trabajador.PostJsonAsync($"/api/solicitudes/{sid}/aprobar")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await encargado.PostJsonAsync($"/api/solicitudes/{sid}/entregar")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await (await encargado.PostJsonAsync($"/api/solicitudes/{sid}/aprobar")).LeerAsync())["estado"]!.GetValue<string>().Should().Be("Aprobada");
        var entregada = await (await encargado.PostJsonAsync($"/api/solicitudes/{sid}/entregar")).LeerAsync();

        entregada["estado"]!.GetValue<string>().Should().Be("Entregada");
        (await trabajador.ExistenciaAsync(id)).Should().Be(antes - 4);
        var mias = await trabajador.GetJsonAsync("/api/solicitudes?estado=Entregada");
        mias["items"]!.AsArray().Should().Contain(s => s!["id"]!.GetValue<int>() == sid);
    }

    [Fact(DisplayName = "RF-16, RF-17 y RF-32: salida, alerta por retraso y devolución de una herramienta")]
    public async Task Herramienta_atrasada_genera_alerta()
    {
        var encargado = await api.ComoAsync("encargado");
        var id = await encargado.IdArticuloAsync("INS-NIV-01");
        var responsable = (await encargado.GetJsonAsync("/api/instalacion/responsables")).AsArray()[0]!["id"]!.GetValue<int>();
        var manana = DateOnly.FromDateTime(DateTime.Today.AddDays(1)).ToString("yyyy-MM-dd");
        var salida = await (await encargado.PostJsonAsync("/api/instalacion", new { articuloId = id, responsableId = responsable, destino = "Casa Mora", fechaPrevista = manana })).LeerAsync();
        var sid = salida["id"]!.GetValue<int>();

        // Simula que pasaron días: la fecha prevista queda en el pasado.
        await api.EscalarAsync<int>("UPDATE SALIDA_HERRAMIENTA SET fecha_salida = DATEADD(day,-6,CAST(GETDATE() AS date)), fecha_prevista = DATEADD(day,-2,CAST(GETDATE() AS date)) WHERE id_salida = @id; SELECT 1", ("@id", sid));
        var alertas = await encargado.GetJsonAsync("/api/alertas");
        alertas["herramientasSinDevolver"]!.AsArray().Should().Contain(h => h!["id"]!.GetValue<int>() == sid && h["diasRetraso"]!.GetValue<int>() == 2);

        var dev = await (await encargado.PostJsonAsync($"/api/instalacion/{sid}/devolucion", new { estado = "Buena" })).LeerAsync();
        dev["fechaDevolucion"].Should().NotBeNull();
        (await encargado.GetJsonAsync("/api/alertas"))["herramientasSinDevolver"]!.AsArray().Should().NotContain(h => h!["id"]!.GetValue<int>() == sid);
    }

    [Theory(DisplayName = "RF-35: los reportes se exportan como PDF y Excel válidos")]
    [InlineData("existencias", "pdf", "application/pdf", "%PDF")]
    [InlineData("movimientos", "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PK")]
    [InlineData("consumo", "pdf", "application/pdf", "%PDF")]
    [InlineData("herramientas-fuera", "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PK")]
    public async Task Exportacion(string reporte, string formato, string tipo, string firma)
    {
        var c = await api.ComoAsync("admin");
        var r = await c.GetAsync($"/api/reportes/{reporte}/exportar?formato={formato}");
        r.StatusCode.Should().Be(HttpStatusCode.OK);
        r.Content.Headers.ContentType!.MediaType.Should().Be(tipo);
        r.Content.Headers.ContentDisposition!.FileName.Should().Contain($"reporte-{reporte}").And.EndWith(formato);
        var bytes = await r.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(bytes, 0, firma.Length).Should().Be(firma);
    }

    [Fact(DisplayName = "RF-33: el reporte de existencias muestra lo mismo que la base de datos")]
    public async Task Reporte_coincide_con_base()
    {
        var c = await api.ComoAsync("admin");
        var r = await c.GetJsonAsync("/api/reportes/existencias");
        r["filas"]!.AsArray().Count.Should().Be(await api.EscalarAsync<int>("SELECT COUNT(*) FROM ARTICULO WHERE activo = 1"));
    }

    [Fact(DisplayName = "RF-38 y RF-39: generar un respaldo y restaurarlo devuelve los datos a ese momento")]
    public async Task Respaldo_y_restauracion()
    {
        var c = await api.ComoAsync("admin");
        var respaldo = await (await c.PostJsonAsync("/api/respaldos")).LeerAsync();
        var rid = respaldo["id"]!.GetValue<int>();
        respaldo["tamanoBytes"]!.GetValue<long>().Should().BeGreaterThan(0);

        (await c.PostJsonAsync("/api/ubicaciones", new { nombre = "Creada después del respaldo", activo = true })).EnsureSuccessStatusCode();
        (await c.PostJsonAsync("/api/respaldos/restaurar", new { respaldoId = rid, confirmacion = "restaurar" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var restaurar = await c.PostJsonAsync("/api/respaldos/restaurar", new { respaldoId = rid, confirmacion = "RESTAURAR" });
        restaurar.StatusCode.Should().Be(HttpStatusCode.NoContent, await restaurar.Content.ReadAsStringAsync());
        (await api.EscalarAsync<int>("SELECT COUNT(*) FROM UBICACION WHERE nombre = 'Creada después del respaldo'")).Should().Be(0);

        var lista = (await c.GetJsonAsync("/api/respaldos"))["respaldos"]!.AsArray();
        lista[0]!["descripcion"]!.GetValue<string>().Should().Be("Automático antes de restaurar");
    }
}
