namespace Muebleria.Application.Common;

/// <summary>Códigos de permiso. Cada uno es una política de autorización en la API (RNF-02).</summary>
public static class Permisos
{
    public const string ArticulosVer = "Articulos.Ver";
    public const string ArticulosEditar = "Articulos.Editar";
    public const string MovimientosRegistrar = "Movimientos.Registrar";
    public const string MovimientosAjustar = "Movimientos.Ajustar";
    public const string SolicitudesCrear = "Solicitudes.Crear";
    public const string SolicitudesAprobar = "Solicitudes.Aprobar";
    public const string HerramientasPrestar = "Herramientas.Prestar";
    public const string InventarioContar = "Inventario.Contar";
    public const string AlertasVer = "Alertas.Ver";
    public const string CatalogosGestionar = "Catalogos.Gestionar";
    public const string UsuariosGestionar = "Usuarios.Gestionar";
    public const string ReportesVer = "Reportes.Ver";
    public const string BitacoraVer = "Bitacora.Ver";
    public const string RespaldosGestionar = "Respaldos.Gestionar";

    public static readonly IReadOnlyDictionary<string, string> Descripciones = new Dictionary<string, string>
    {
        [ArticulosVer] = "Consultar artículos y existencias",
        [ArticulosEditar] = "Registrar y editar artículos",
        [MovimientosRegistrar] = "Registrar entradas, salidas, mermas y bajas",
        [MovimientosAjustar] = "Ajustar existencias",
        [SolicitudesCrear] = "Crear solicitudes de materiales",
        [SolicitudesAprobar] = "Aprobar o rechazar solicitudes",
        [HerramientasPrestar] = "Salida y devolución de herramientas de instalación",
        [InventarioContar] = "Toma de inventario físico",
        [AlertasVer] = "Consultar alertas",
        [CatalogosGestionar] = "Gestionar ubicaciones y categorías",
        [UsuariosGestionar] = "Gestionar usuarios y roles",
        [ReportesVer] = "Consultar y exportar reportes",
        [BitacoraVer] = "Consultar la bitácora",
        [RespaldosGestionar] = "Generar y restaurar respaldos",
    };

    public static IEnumerable<string> Todos => Descripciones.Keys;
}

public static class Roles
{
    public const string Administrador = "Administrador";
    public const string Encargado = "Encargado de bodega";
    public const string Trabajador = "Trabajador";

    /// <summary>Matriz de permisos por rol (propuesta técnica, sección 7).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> PermisosPorRol = new Dictionary<string, string[]>
    {
        [Trabajador] = [Permisos.ArticulosVer, Permisos.SolicitudesCrear],
        [Encargado] =
        [
            Permisos.ArticulosVer, Permisos.SolicitudesCrear, Permisos.ArticulosEditar,
            Permisos.MovimientosRegistrar, Permisos.SolicitudesAprobar, Permisos.HerramientasPrestar,
            Permisos.InventarioContar, Permisos.AlertasVer,
        ],
        [Administrador] = Permisos.Todos.ToArray(),
    };
}
