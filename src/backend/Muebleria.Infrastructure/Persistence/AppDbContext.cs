using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

    public void OlvidarCambios() => ChangeTracker.Clear();

    /// <summary>
    /// Estados con los textos exactos del diccionario de datos: "Dañada" y "De baja"
    /// no pueden ser nombres de un enum de C#, así que se traducen al guardar y al leer.
    /// </summary>
    private static readonly ValueConverter<EstadoArticulo, string> ConvertidorEstado = new(
        e => e == EstadoArticulo.Danada ? "Dañada" : e == EstadoArticulo.DeBaja ? "De baja" : e.ToString(),
        s => s == "Dañada" ? EstadoArticulo.Danada : s == "De baja" ? EstadoArticulo.DeBaja : Enum.Parse<EstadoArticulo>(s));

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Tipos del diccionario de datos: VARCHAR (no NVARCHAR), DATETIME y DECIMAL(10,2).
        b.Properties<string>().AreUnicode(false);
        b.Properties<DateTime>().HaveColumnType("datetime");
        b.Properties<DateTime?>().HaveColumnType("datetime");
        b.Properties<decimal>().HavePrecision(10, 2);
        // Los enums se guardan como texto VARCHAR(20): "Entrada", "Buena", "Pendiente"…
        b.Properties<TipoInventario>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<TipoDato>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<TipoMovimiento>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<EstadoSolicitud>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<EstadoConteo>().HaveConversion<string>().HaveMaxLength(20);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entidad in modelBuilder.Model.GetEntityTypes())
            foreach (var p in entidad.GetProperties().Where(p => p.ClrType == typeof(EstadoArticulo) || p.ClrType == typeof(EstadoArticulo?)))
            {
                p.SetValueConverter(ConvertidorEstado);
                p.SetMaxLength(20);
            }

        // Todo texto es VARCHAR, también los enums que se guardan como texto (estado, tipo…).
        foreach (var p in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
            if ((p.GetValueConverter()?.ProviderClrType ?? p.GetProviderClrType() ?? p.ClrType) == typeof(string))
                p.SetIsUnicode(false);

        NombresDelDiccionario.Aplicar(modelBuilder);
    }

    // ---------------------------------------------------------------------------------
    // Bitácora automática (RF-03) dentro de la misma transacción que el cambio (RNF-06).
    // Guarda solo lo que pide el diccionario: usuario, módulo, acción y fecha-hora.
    // ---------------------------------------------------------------------------------
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Las acciones escritas a mano (respaldos, reportes, alertas) también respetan el VARCHAR(100) del diccionario.
        foreach (var b in ChangeTracker.Entries<Bitacora>().Where(b => b.State == EntityState.Added))
            b.Entity.Accion = Recortar(b.Entity.Accion, 100);

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
                // Se arma después de guardar, cuando los registros nuevos ya tienen su id.
                Accion = Recortar(DescribirAccion(c), 100),
                FechaHora = DateTime.Now,
            });
        }
        await base.SaveChangesAsync(ct);

        if (tx is not null) await tx.CommitAsync(ct);
        return filas;
    }

    private record Cambio(EntityEntry Entrada, string Modulo, string Verbo);

    private List<Cambio> CapturarCambios()
    {
        var lista = new List<Cambio>();
        foreach (var e in ChangeTracker.Entries())
        {
            if (e.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (e.Entity is not (IAuditable or Movimiento or Solicitud or SalidaHerramienta)) continue;

            string verbo;
            if (e.Entity is Movimiento m)
                verbo = $"Registrar {m.Tipo.ToString().ToLowerInvariant()}";
            else if (e.Entity is Solicitud s)
                verbo = e.State == EntityState.Added ? "Crear" : s.Estado switch
                {
                    EstadoSolicitud.Aprobada => "Aprobar",
                    EstadoSolicitud.Rechazada => "Rechazar",
                    EstadoSolicitud.Entregada => "Entregar",
                    _ => "Editar",
                };
            else if (e.Entity is SalidaHerramienta)
                verbo = e.State == EntityState.Added ? "Registrar salida de" : "Registrar devolución de";
            else if (e.State == EntityState.Modified)
            {
                var modificadas = e.Properties.Where(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)).ToList();
                if (modificadas.Count == 0) continue;
                verbo = modificadas.Count == 1 && modificadas[0].Metadata.Name == "Activo"
                    ? (Equals(modificadas[0].CurrentValue, true) ? "Activar" : "Desactivar")
                    : modificadas.All(p => p.Metadata.Name is "StockActual" or "Version") ? "Actualizar existencia de" : "Editar";
            }
            else
                verbo = e.State == EntityState.Added ? "Crear" : "Eliminar";

            lista.Add(new Cambio(e, ModuloDe(e.Entity), verbo));
        }
        return lista;
    }

    private static string DescribirAccion(Cambio c)
    {
        var id = c.Entrada.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue;
        return c.Entrada.Entity switch
        {
            Movimiento m => $"{c.Verbo} de {m.Cantidad.ToString("0.##", CultureInfo.InvariantCulture)} en artículo #{m.ArticuloId}",
            Solicitud s => $"{c.Verbo} solicitud #{id} ({s.Cantidad.ToString("0.##", CultureInfo.InvariantCulture)} del artículo #{s.ArticuloId})",
            SalidaHerramienta h => $"{c.Verbo} herramienta #{h.ArticuloId} (salida #{id})",
            Articulo a => $"{c.Verbo} artículo {a.Codigo} (#{id})",
            Usuario u => $"{c.Verbo} usuario {u.NombreUsuario} (#{id})",
            Ubicacion u => $"{c.Verbo} ubicación {u.Nombre} (#{id})",
            Categoria cat => $"{c.Verbo} categoría {cat.Nombre} (#{id})",
            CampoPersonalizado f => $"{c.Verbo} campo {f.Nombre} (#{id})",
            _ => $"{c.Verbo} {c.Entrada.Metadata.ClrType.Name} (#{id})",
        };
    }

    private static string ModuloDe(object entidad) => entidad switch
    {
        Usuario => "Usuarios",
        Ubicacion => "Ubicaciones",
        Categoria or CampoPersonalizado => "Categorías",
        Articulo => "Catálogo",
        Movimiento => "Movimientos",
        Solicitud => "Solicitudes",
        SalidaHerramienta => "Herramientas de instalación",
        _ => entidad.GetType().Name,
    };

    private static string Recortar(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
