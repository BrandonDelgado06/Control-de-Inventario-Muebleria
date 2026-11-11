using Muebleria.Application.Common.Interfaces;

namespace Muebleria.Api.Servicios;

/// <summary>
/// Guarda las fotografías de los artículos en App_Data/fotos (o en la carpeta de "Fotos:Carpeta").
/// Los nombres los genera el sistema: nunca se usa el nombre que trae el archivo subido.
/// </summary>
public class AlmacenFotosDisco : IAlmacenFotos
{
    private readonly string carpeta;

    public AlmacenFotosDisco(IConfiguration config, IWebHostEnvironment env)
    {
        carpeta = config["Fotos:Carpeta"] is { Length: > 0 } c ? c : Path.Combine(env.ContentRootPath, "App_Data", "fotos");
        Directory.CreateDirectory(carpeta);
    }

    public async Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct)
    {
        var nombre = $"{Guid.NewGuid():N}{extension}";
        await using var destino = File.Create(Ruta(nombre));
        await contenido.CopyToAsync(destino, ct);
        return nombre;
    }

    public Stream? Abrir(string nombre)
    {
        var ruta = Ruta(nombre);
        return File.Exists(ruta) ? File.OpenRead(ruta) : null;
    }

    public void Eliminar(string nombre)
    {
        var ruta = Ruta(nombre);
        if (File.Exists(ruta)) File.Delete(ruta);
    }

    /// <summary>Solo el nombre del archivo: evita rutas como "..\..\web.config".</summary>
    private string Ruta(string nombre) => Path.Combine(carpeta, Path.GetFileName(nombre));
}
