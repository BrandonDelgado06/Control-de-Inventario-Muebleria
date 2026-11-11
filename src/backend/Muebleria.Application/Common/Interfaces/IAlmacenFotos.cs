namespace Muebleria.Application.Common.Interfaces;

/// <summary>
/// RF-08: las fotografías se guardan en disco; la base de datos solo guarda el nombre del archivo
/// (columna foto_ruta). Lo implementa la API, que conoce la carpeta de la aplicación.
/// </summary>
public interface IAlmacenFotos
{
    /// <summary>Guarda la imagen y devuelve el nombre con que quedó.</summary>
    Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct);
    /// <summary>Abre la imagen para leerla, o null si el archivo ya no existe.</summary>
    Stream? Abrir(string nombre);
    void Eliminar(string nombre);
}
