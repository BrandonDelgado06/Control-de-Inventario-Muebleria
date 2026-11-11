using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;

namespace Muebleria.IntegrationTests.Apoyo;

/// <summary>
/// Levanta la API completa contra una base de datos de pruebas en SQL Server, separada de la de desarrollo.
/// La base se borra y se vuelve a crear (migración + datos de ejemplo) en cada ejecución.
/// Cadena de conexión: variable de entorno PRUEBAS_SQL o, si no existe, la instancia local.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Contraseñas solo para la base de pruebas.
    public const string ClaveAdmin = "Admin123!";
    public const string ClaveEncargado = "Bodega123!";
    public const string ClaveTrabajador = "Taller123!";

    public string Conexion { get; } = Environment.GetEnvironmentVariable("PRUEBAS_SQL")
        ?? "Server=.;Database=MuebleriaInventario_Pruebas;Trusted_Connection=True;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Pruebas");
        builder.UseSetting("ConnectionStrings:Inventario", Conexion);
        builder.UseSetting("Database:MigrarAlIniciar", "true");
        builder.UseSetting("Seed:DatosEjemplo", "true");
        builder.UseSetting("Seed:AdminContrasena", ClaveAdmin);
        builder.UseSetting("Seed:EncargadoContrasena", ClaveEncargado);
        builder.UseSetting("Seed:TrabajadorContrasena", ClaveTrabajador);
    }

    public async Task InitializeAsync()
    {
        var b = new SqlConnectionStringBuilder(Conexion);
        var nombre = b.InitialCatalog;
        b.InitialCatalog = "master";
        await using var cn = new SqlConnection(b.ConnectionString);
        await cn.OpenAsync();
        await using var cmd = new SqlCommand("""
            IF DB_ID(@db) IS NOT NULL
            BEGIN
                DECLARE @q nvarchar(300) = QUOTENAME(@db);
                EXEC (N'ALTER DATABASE ' + @q + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + @q + N';');
            END
            """, cn);
        cmd.Parameters.AddWithValue("@db", nombre);
        await cmd.ExecuteNonQueryAsync();
        SqlConnection.ClearAllPools();
        _ = Server;   // arranca la API: migra la base y carga los datos de ejemplo
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    /// <summary>Cliente HTTP con su propia cookie, ya autenticado con el usuario indicado.</summary>
    public async Task<HttpClient> ComoAsync(string usuario)
    {
        var clave = usuario switch { "admin" => ClaveAdmin, "encargado" => ClaveEncargado, "trabajador" => ClaveTrabajador, _ => throw new ArgumentException(usuario) };
        var c = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var r = await c.PostAsJsonAsync("/api/auth/login", new { usuario, contrasena = clave });
        r.EnsureSuccessStatusCode();
        return c;
    }

    public HttpClient SinSesion() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    /// <summary>Consulta directa a la base de pruebas, para comprobar lo que quedó guardado.</summary>
    public async Task<T> EscalarAsync<T>(string sql, params (string, object)[] parametros)
    {
        await using var cn = new SqlConnection(Conexion);
        await cn.OpenAsync();
        await using var cmd = new SqlCommand(sql, cn);
        foreach (var (n, v) in parametros) cmd.Parameters.AddWithValue(n, v);
        return (T)Convert.ChangeType((await cmd.ExecuteScalarAsync())!, typeof(T));
    }
}

[CollectionDefinition(Nombre)]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Nombre = "API con SQL Server";
}
