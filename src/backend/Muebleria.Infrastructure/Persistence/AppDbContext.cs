using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Domain.Common;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;

namespace Muebleria.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, IUsuarioActual usuarioActual)
    : DbContext(options), IAppDbContext
{
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Bitacora> Bitacoras => Set<Bitacora>();
    public DbSet<Configuracion> Configuraciones => Set<Configuracion>();
    public DbSet<Respaldo> Respaldos => Set<Respaldo>();
    public DbSet<Ubicacion> Ubicaciones => Set<Ubicacion>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<CampoPersonalizado> CamposPersonalizados => Set<CampoPersonalizado>();
    public DbSet<Articulo> Articulos => Set<Articulo>();
    public DbSet<ArticuloValorCampo> ArticuloValores => Set<ArticuloValorCampo>();
    public DbSet<ArticuloAlias> ArticuloAlias => Set<ArticuloAlias>();
    public DbSet<HistorialEstado> HistorialEstados => Set<HistorialEstado>();
    public DbSet<Movimiento> Movimientos => Set<Movimiento>();
    public DbSet<Solicitud> Solicitudes => Set<Solicitud>();
    public DbSet<SalidaHerramienta> SalidasHerramienta => Set<SalidaHerramienta>();
    public DbSet<ConteoFisico> ConteosFisicos => Set<ConteoFisico>();
    public DbSet<ConteoDetalle> ConteoDetalles => Set<ConteoDetalle>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Montos y cantidades: DECIMAL(10,2) como en el diccionario de datos.
        b.Properties<decimal>().HavePrecision(10, 2);
        // Los enums se guardan como texto (VARCHAR(20)): "Entrada", "Buena", "Pendiente"…
        b.Properties<TipoInventario>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<TipoDato>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<EstadoArticulo>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<TipoMovimiento>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<EstadoSolicitud>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<EstadoConteo>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        NombresDelDiccionario.Aplicar(modelBuilder);
    }

    // ---------------------------------------------------------------------------------
    // Bitácora automática (RF-03) dentro de la misma transacción que el cambio (RNF-06).
    // ---------------------------------------------------------------------------------
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var usuarioId = usuarioActual.Id;
        var cambios = usuarioId is null ? [] : CapturarCambios();
        if (cambios.Count == 0) return await base.SaveChangesAsync(ct);

        var transaccionPropia = Database.CurrentTransaction is null;
        await using var tx = transaccionPropia ? await Database.BeginTransactionAsync(ct) : null;

        var filas = await base.SaveChangesAsync(ct);
        foreach (var c in cambios)
        {
            Bitacoras.Add(new Bitacora
            {
                UsuarioId = usuarioId!.Value,
                Modulo = c.Modulo,
                Accion = c.Accion,
                Entidad = c.Entrada.Metadata.ClrType.Name,
                IdEntidad = c.Entrada.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString(),
                // Los movimientos se describen después de guardar, cuando ya tienen su artículo asignado.
                Detalle = c.Entrada.Entity is Movimiento m
                    ? JsonSerializer.Serialize(new { articulo = m.ArticuloId, m.Cantidad, m.StockAnterior, m.StockNuevo, m.Motivo })
                    : c.Detalle,
                FechaHora = DateTime.Now,
            });
        }
        await base.SaveChangesAsync(ct);

        if (tx is not null) await tx.CommitAsync(ct);
        return filas;
    }

    private record Cambio(EntityEntry Entrada, string Modulo, string Accion, string? Detalle);

    private static readonly HashSet<string> PropiedadesOcultas = ["ContrasenaHash", "Version"];

    private List<Cambio> CapturarCambios()
    {
        var lista = new List<Cambio>();
        foreach (var e in ChangeTracker.Entries())
        {
            if (e.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (e.Entity is not (IAuditable or Movimiento)) continue;

            var modulo = ModuloDe(e.Entity);
            string accion;
            string? detalle;

            if (e.Entity is Movimiento m)
            {
                accion = $"Registrar {m.Tipo.ToString().ToLowerInvariant()}";
                detalle = null;
            }
            else if (e.State == EntityState.Modified)
            {
                var props = e.Properties
                    .Where(p => p.IsModified && !PropiedadesOcultas.Contains(p.Metadata.Name) && !Equals(p.OriginalValue, p.CurrentValue))
                    .ToDictionary(p => p.Metadata.Name, p => new[] { Texto(p.OriginalValue), Texto(p.CurrentValue) });
                if (props.Count == 0 && !e.Properties.Any(p => p.Metadata.Name == "ContrasenaHash" && p.IsModified)) continue;
                if (e.Properties.Any(p => p.Metadata.Name == "ContrasenaHash" && p.IsModified))
                    props["Contrasena"] = ["***", "***"];
                accion = props.TryGetValue("Activo", out var act) && props.Count == 1
                    ? (act[1] == "True" ? "Activar" : "Desactivar")
                    : "Editar";
                detalle = JsonSerializer.Serialize(props);
            }
            else
            {
                accion = e.State == EntityState.Added ? "Crear" : "Eliminar";
                detalle = Resumen(e);
            }

            lista.Add(new Cambio(e, modulo, $"{accion} {Nombre(e.Entity)}".Trim(), detalle));
        }
        return lista;
    }

    private static string ModuloDe(object entidad) => entidad switch
    {
        Usuario => "Usuarios",
        Ubicacion => "Ubicaciones",
        Categoria or CampoPersonalizado => "Categorías",
        Articulo => "Catálogo",
        Movimiento => "Movimientos",
        _ => entidad.GetType().Name,
    };

    private static string Nombre(object entidad) => entidad switch
    {
        Usuario => "usuario",
        Ubicacion => "ubicación",
        Categoria => "categoría",
        CampoPersonalizado => "campo personalizado",
        Articulo => "artículo",
        _ => "",
    };

    private static string? Resumen(EntityEntry e)
    {
        var sb = new StringBuilder();
        foreach (var nombre in new[] { "Codigo", "Nombre", "NombreUsuario" })
        {
            var p = e.Properties.FirstOrDefault(x => x.Metadata.Name == nombre);
            if (p?.CurrentValue is not null) sb.Append(sb.Length > 0 ? " · " : "").Append(p.CurrentValue);
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    private static string? Texto(object? v) => v switch
    {
        null => null,
        DateOnly d => d.ToString("yyyy-MM-dd"),
        decimal dec => dec.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
        _ => v.ToString(),
    };
}
