using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Muebleria.Infrastructure.Persistence;

/// <summary>
/// Aplica la nomenclatura del diccionario de datos sin configurar columna por columna:
/// tablas en MAYÚSCULAS_CON_GUION (ARTICULO, SALIDA_HERRAMIENTA), columnas en snake_case,
/// la llave primaria "Id" como id_&lt;tabla&gt; y las llaves foráneas "XxxId" como id_xxx.
/// Un nombre fijado a mano en una configuración tiene prioridad.
/// </summary>
internal static class NombresDelDiccionario
{
    public static void Aplicar(ModelBuilder modelBuilder)
    {
        foreach (var entidad in modelBuilder.Model.GetEntityTypes())
        {
            var tablaSnake = Snake(entidad.ClrType.Name);
            // El nombre que EF toma del DbSet ("Articulos") cuenta como convención y se reemplaza.
            if (((IConventionEntityType)entidad).GetTableNameConfigurationSource() != ConfigurationSource.Explicit)
                entidad.SetTableName(tablaSnake.ToUpperInvariant());

            foreach (var prop in entidad.GetProperties())
            {
                if (((IConventionProperty)prop).GetColumnNameConfigurationSource() == ConfigurationSource.Explicit) continue; // nombre manual

                var nombre = prop.Name;
                string columna;
                if (nombre == "Id")
                    columna = "id_" + tablaSnake;
                else if (nombre.EndsWith("Id") && nombre.Length > 2)
                    columna = "id_" + Snake(nombre[..^2]);
                else
                    columna = Snake(nombre);
                prop.SetColumnName(columna);
            }
        }
    }

    private static string Snake(string pascal)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (char.IsUpper(c) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
