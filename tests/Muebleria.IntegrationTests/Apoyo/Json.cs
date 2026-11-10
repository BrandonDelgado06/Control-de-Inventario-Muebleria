using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Muebleria.IntegrationTests.Apoyo;

/// <summary>Atajos para leer las respuestas JSON de la API sin depender de sus clases.</summary>
public static class Json
{
    public static async Task<JsonNode> LeerAsync(this HttpResponseMessage r) =>
        JsonNode.Parse(await r.Content.ReadAsStringAsync()) ?? throw new InvalidOperationException("Respuesta vacía");

    public static async Task<JsonNode> GetJsonAsync(this HttpClient c, string url)
    {
        var r = await c.GetAsync(url);
        r.EnsureSuccessStatusCode();
        return await r.LeerAsync();
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient c, string url, object? cuerpo = null) =>
        c.PostAsJsonAsync(url, cuerpo ?? new { });

    /// <summary>Título del ProblemDetails (mensaje de error en español).</summary>
    public static async Task<string> TituloAsync(this HttpResponseMessage r) => (await r.LeerAsync())["title"]?.GetValue<string>() ?? "";

    /// <summary>Id del artículo de ejemplo con el código indicado.</summary>
    public static async Task<int> IdArticuloAsync(this HttpClient c, string codigo) =>
        (await c.GetJsonAsync($"/api/articulos?texto={codigo}"))["items"]!.AsArray()
            .First(a => a!["codigo"]!.GetValue<string>() == codigo)!["id"]!.GetValue<int>();

    public static async Task<decimal> ExistenciaAsync(this HttpClient c, int id) =>
        (await c.GetJsonAsync($"/api/articulos/{id}"))["articulo"]!["stockActual"]!.GetValue<decimal>();

    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);
}
