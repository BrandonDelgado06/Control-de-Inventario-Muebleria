using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Muebleria.Domain.Entities;

namespace Muebleria.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<Rol> Roles { get; }
    DbSet<Permiso> Permisos { get; }
    DbSet<RolPermiso> RolPermisos { get; }
    DbSet<Usuario> Usuarios { get; }
    DbSet<Bitacora> Bitacoras { get; }
    DbSet<Configuracion> Configuraciones { get; }
    DbSet<Respaldo> Respaldos { get; }
    DbSet<Ubicacion> Ubicaciones { get; }
    DbSet<Categoria> Categorias { get; }
    DbSet<CampoPersonalizado> CamposPersonalizados { get; }
    DbSet<Articulo> Articulos { get; }
    DbSet<ArticuloValorCampo> ArticuloValores { get; }
    DbSet<ArticuloAlias> ArticuloAlias { get; }
    DbSet<HistorialEstado> HistorialEstados { get; }
    DbSet<Movimiento> Movimientos { get; }
    DbSet<Solicitud> Solicitudes { get; }
    DbSet<SalidaHerramienta> SalidasHerramienta { get; }
    DbSet<ConteoFisico> ConteosFisicos { get; }
    DbSet<ConteoDetalle> ConteoDetalles { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    /// <summary>Descarta las entidades en memoria (por ejemplo, después de restaurar la base).</summary>
    void OlvidarCambios();
}

/// <summary>Usuario que hace la petición actual; null fuera de una petición (p. ej. datos semilla).</summary>
public interface IUsuarioActual
{
    int? Id { get; }
}

public interface IHasherContrasenas
{
    string Hash(string contrasena);
    bool Verificar(string hash, string contrasena);
}
