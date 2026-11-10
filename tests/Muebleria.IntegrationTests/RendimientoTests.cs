using System.Diagnostics;
using FluentAssertions;
using Muebleria.IntegrationTests.Apoyo;
using Xunit.Abstractions;

namespace Muebleria.IntegrationTests;

/// <summary>
/// RNF-04: las consultas y registros habituales responden en 3 segundos o menos con 10 usuarios a la vez.
/// Cada usuario hace 15 operaciones mezcladas (buscar, ver detalle, ver el panel, registrar una entrada).
/// </summary>
[Collection(ApiCollection.Nombre)]
public class RendimientoTests(ApiFixture api, ITestOutputHelper salida)
{
    [Fact(DisplayName = "RNF-04: 10 usuarios simultáneos, ninguna respuesta tarda más de 3 segundos")]
    public async Task Diez_usuarios_simultaneos()
    {
        var clientes = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => api.ComoAsync(i % 2 == 0 ? "encargado" : "admin")));
        var id = await clientes[0].IdArticuloAsync("HER-BIS-35");
        var tiempos = new System.Collections.Concurrent.ConcurrentBag<(string operacion, double ms, bool ok)>();
        var fallos = new System.Collections.Concurrent.ConcurrentBag<string>();

        async Task Medir(string operacion, Func<Task<HttpResponseMessage>> llamada)
        {
            var sw = Stopwatch.StartNew();
            var r = await llamada();
            sw.Stop();
            tiempos.Add((operacion, sw.Elapsed.TotalMilliseconds, r.IsSuccessStatusCode));
            if (!r.IsSuccessStatusCode) fallos.Add($"{operacion}: {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
        }

        // Calentamiento: la primera llamada compila consultas y carga el modelo; no es uso normal.
        await clientes[0].GetAsync("/api/panel");

        await Task.WhenAll(clientes.Select(async (c, i) =>
        {
            for (var n = 0; n < 15; n++)
            {
                switch (n % 4)
                {
                    case 0: await Medir("Buscar artículo", () => c.GetAsync("/api/articulos?texto=lija")); break;
                    case 1: await Medir("Ver artículo", () => c.GetAsync($"/api/articulos/{id}")); break;
                    case 2: await Medir("Panel principal", () => c.GetAsync("/api/panel")); break;
                    default: await Medir("Registrar entrada", () => c.PostJsonAsync("/api/movimientos", new { articuloId = id, tipo = "Entrada", cantidad = 1, motivo = $"Carga {i}-{n}" })); break;
                }
            }
        }));

        var lista = tiempos.ToList();
        foreach (var g in lista.GroupBy(t => t.operacion).OrderBy(g => g.Key))
        {
            var ms = g.Select(t => t.ms).OrderBy(x => x).ToList();
            salida.WriteLine($"{g.Key,-18} n={ms.Count,3}  promedio={ms.Average(),7:N0} ms  p95={ms[(int)Math.Ceiling(ms.Count * 0.95) - 1],7:N0} ms  máximo={ms.Max(),7:N0} ms");
        }
        salida.WriteLine($"Total: {lista.Count} peticiones, máximo {lista.Max(t => t.ms):N0} ms");
        foreach (var f in fallos.Distinct().Take(5)) salida.WriteLine("FALLO " + f);

        lista.Should().HaveCount(150);
        lista.Should().OnlyContain(t => t.ok, "todas las operaciones deben completarse");
        lista.Max(t => t.ms).Should().BeLessThan(3000, "RNF-04 exige 3 segundos como máximo");
    }
}
