using Muebleria.Domain.Common;

namespace Muebleria.Domain.Entities;

public class Rol
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public List<RolPermiso> Permisos { get; set; } = [];
}

public class Permiso
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Descripcion { get; set; } = "";
}

public class RolPermiso
{
    public int RolId { get; set; }
    public Rol Rol { get; set; } = null!;
    public int PermisoId { get; set; }
    public Permiso Permiso { get; set; } = null!;
}

public class Usuario : IAuditable
{
    public int Id { get; set; }
    public int RolId { get; set; }
    public Rol Rol { get; set; } = null!;
    public string Nombre { get; set; } = "";
    /// <summary>Nombre de acceso (columna "usuario" del diccionario de datos).</summary>
    public string NombreUsuario { get; set; } = "";
    /// <summary>Columna "contrasena" del diagrama ER. Guarda solo el hash PBKDF2, nunca el texto (RNF-01).</summary>
    public string ContrasenaHash { get; set; } = "";
    public bool Activo { get; set; } = true;
}

/// <summary>Tabla 9 del diccionario de datos: exactamente usuario, módulo, acción y fecha-hora (RF-03).</summary>
public class Bitacora
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public string Modulo { get; set; } = "";
    public string Accion { get; set; } = "";
    public DateTime FechaHora { get; set; }
}

public class Configuracion
{
    public string Clave { get; set; } = "";
    public string Valor { get; set; } = "";
    public string? Descripcion { get; set; }
}

public class Respaldo
{
    public int Id { get; set; }
    public string Archivo { get; set; } = "";
    public DateTime FechaHora { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public long TamanoBytes { get; set; }
}
