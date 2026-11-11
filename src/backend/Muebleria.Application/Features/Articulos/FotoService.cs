using Muebleria.Application.Common.Interfaces;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Application.Features.Articulos;

/// <summary>Foto lista para enviarse al navegador.</summary>
public record FotoArchivo(Stream Contenido, string TipoContenido);

/// <summary>
/// RF-08: fotografía del artículo. Se acepta JPG, PNG o WEBP de hasta 3 MB, y el tipo se comprueba
/// por los primeros bytes del archivo (no por la extensión, que se puede cambiar).
/// </summary>
public class FotoService(IAppDbContext db, IAlmacenFotos almacen)
{
    public const long TamanoMaximo = 3 * 1024 * 1024;

    public async Task SubirAsync(int articuloId, Stream contenido, long tamano, CancellationToken ct)
    {
        var articulo = await db.Articulos.FindAsync([articuloId], ct) ?? throw new NoEncontradoException("Artículo", articuloId);
        if (tamano <= 0) throw new DomainException("El archivo está vacío.");
        if (tamano > TamanoMaximo) throw new DomainException("La foto pesa más de 3 MB. Redúzcala o tómela con menos resolución.");

        // Se copia a memoria para poder leer la cabecera y luego guardarla completa.
        using var memoria = new MemoryStream();
        await contenido.CopyToAsync(memoria, ct);
        var extension = Detectar(memoria.GetBuffer().AsSpan(0, (int)memoria.Length))
            ?? throw new DomainException("La foto debe ser JPG, PNG o WEBP.");

        memoria.Position = 0;
        var nombre = await almacen.GuardarAsync(memoria, extension, ct);
        var anterior = articulo.FotoRuta;
        articulo.FotoRuta = nombre;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            almacen.Eliminar(nombre);   // no dejar archivos sueltos si no se pudo guardar
            throw;
        }
        if (anterior is not null) almacen.Eliminar(anterior);
    }

    public async Task<FotoArchivo> ObtenerAsync(int articuloId, CancellationToken ct)
    {
        var articulo = await db.Articulos.FindAsync([articuloId], ct) ?? throw new NoEncontradoException("Artículo", articuloId);
        var stream = articulo.FotoRuta is { } n ? almacen.Abrir(n) : null;
        if (stream is null) throw new NoEncontradoException("Foto del artículo", articuloId);
        return new FotoArchivo(stream, TipoContenido(articulo.FotoRuta!));
    }

    public async Task EliminarAsync(int articuloId, CancellationToken ct)
    {
        var articulo = await db.Articulos.FindAsync([articuloId], ct) ?? throw new NoEncontradoException("Artículo", articuloId);
        if (articulo.FotoRuta is not { } nombre) return;
        articulo.FotoRuta = null;
        await db.SaveChangesAsync(ct);
        almacen.Eliminar(nombre);
    }

    /// <summary>Reconoce la imagen por su "número mágico".</summary>
    internal static string? Detectar(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
        if (b.Length >= 8 && b[..8].SequenceEqual((byte[])[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return ".png";
        if (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b[8..12].SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }

    private static string TipoContenido(string nombre) => Path.GetExtension(nombre).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };
}
