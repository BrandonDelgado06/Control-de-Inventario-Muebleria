using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Muebleria.Domain.Entities;

namespace Muebleria.Infrastructure.Persistence.Configurations;

// Restricciones del diccionario de datos y de la propuesta técnica (sección 5).
// Regla general: ninguna llave foránea borra en cascada; los registros se desactivan, no se eliminan.

internal class RolConfig : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}

internal class PermisoConfig : IEntityTypeConfiguration<Permiso>
{
    public void Configure(EntityTypeBuilder<Permiso> b)
    {
        b.Property(x => x.Codigo).HasMaxLength(60).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.Codigo).IsUnique();
    }
}

internal class RolPermisoConfig : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> b)
    {
        b.HasKey(x => new { x.RolId, x.PermisoId });
        b.HasOne(x => x.Rol).WithMany(r => r.Permisos).HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Permiso).WithMany().HasForeignKey(x => x.PermisoId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class UsuarioConfig : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        b.Property(x => x.NombreUsuario).HasColumnName("usuario").HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.NombreUsuario).IsUnique();
        b.Property(x => x.ContrasenaHash).HasColumnName("contrasena").HasMaxLength(256).IsRequired();
        b.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class BitacoraConfig : IEntityTypeConfiguration<Bitacora>
{
    public void Configure(EntityTypeBuilder<Bitacora> b)
    {
        b.Property(x => x.Modulo).HasMaxLength(50).IsRequired();
        b.Property(x => x.Accion).HasMaxLength(100).IsRequired();
        b.Property(x => x.FechaHora).HasDefaultValueSql("GETDATE()");
        b.HasIndex(x => x.FechaHora);
        b.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class ConfiguracionConfig : IEntityTypeConfiguration<Configuracion>
{
    public void Configure(EntityTypeBuilder<Configuracion> b)
    {
        b.HasKey(x => x.Clave);
        b.Property(x => x.Clave).HasMaxLength(60);
        b.Property(x => x.Valor).HasMaxLength(200).IsRequired();
        b.Property(x => x.Descripcion).HasMaxLength(200);
    }
}

internal class RespaldoConfig : IEntityTypeConfiguration<Respaldo>
{
    public void Configure(EntityTypeBuilder<Respaldo> b)
    {
        b.Property(x => x.Archivo).HasMaxLength(260).IsRequired();
        b.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class UbicacionConfig : IEntityTypeConfiguration<Ubicacion>
{
    public void Configure(EntityTypeBuilder<Ubicacion> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(60).IsRequired();
        b.HasIndex(x => x.Nombre).IsUnique();
        b.Property(x => x.Descripcion).HasMaxLength(150);
    }
}

internal class CategoriaConfig : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> b)
    {
        b.Property(x => x.Nombre).HasMaxLength(60).IsRequired();
        b.HasIndex(x => x.Nombre).IsUnique();
    }
}

internal class CampoPersonalizadoConfig : IEntityTypeConfiguration<CampoPersonalizado>
{
    public void Configure(EntityTypeBuilder<CampoPersonalizado> b)
    {
        b.Property(x => x.Id).HasColumnName("id_campo"); // mismo nombre que la FK en ARTICULO_VALOR_CAMPO
        b.Property(x => x.Nombre).HasMaxLength(60).IsRequired();
        b.Property(x => x.Opciones).HasMaxLength(500);
        b.HasIndex(x => new { x.CategoriaId, x.Nombre }).IsUnique();
        b.HasOne(x => x.Categoria).WithMany(c => c.Campos).HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class ArticuloConfig : IEntityTypeConfiguration<Articulo>
{
    public void Configure(EntityTypeBuilder<Articulo> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ARTICULO_stock_actual", "[stock_actual] >= 0");
            t.HasCheckConstraint("CK_ARTICULO_stock_minimo", "[stock_minimo] >= 0");
        });
        b.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Codigo).IsUnique();
        b.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Nombre);
        b.Property(x => x.UnidadMedida).HasMaxLength(20).IsRequired();
        b.Property(x => x.StockActual).HasDefaultValue(0m);
        b.Property(x => x.StockMinimo).HasDefaultValue(0m);
        // Valor por defecto del diccionario. ValueGeneratedNever: EF siempre envía el estado elegido.
        b.Property(x => x.Estado).HasDefaultValueSql("'Buena'").ValueGeneratedNever();
        b.Property(x => x.Lote).HasMaxLength(30);
        b.Property(x => x.Presentacion).HasMaxLength(20);
        b.Property(x => x.FotoRuta).HasMaxLength(260);
        b.Property(x => x.Version).IsRowVersion();
        b.Ignore(x => x.BajoMinimo);
        b.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Ubicacion).WithMany().HasForeignKey(x => x.UbicacionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class ArticuloValorCampoConfig : IEntityTypeConfiguration<ArticuloValorCampo>
{
    public void Configure(EntityTypeBuilder<ArticuloValorCampo> b)
    {
        b.HasKey(x => new { x.ArticuloId, x.CampoId });
        b.Property(x => x.Valor).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.Articulo).WithMany(a => a.Valores).HasForeignKey(x => x.ArticuloId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Campo).WithMany().HasForeignKey(x => x.CampoId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class ArticuloAliasConfig : IEntityTypeConfiguration<ArticuloAlias>
{
    public void Configure(EntityTypeBuilder<ArticuloAlias> b)
    {
        b.Property(x => x.Alias).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Alias);
        b.HasIndex(x => new { x.ArticuloId, x.Alias }).IsUnique();
        b.HasOne(x => x.Articulo).WithMany(a => a.Alias).HasForeignKey(x => x.ArticuloId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class HistorialEstadoConfig : IEntityTypeConfiguration<HistorialEstado>
{
    public void Configure(EntityTypeBuilder<HistorialEstado> b)
    {
        b.Property(x => x.FechaHora).HasDefaultValueSql("GETDATE()");
        b.HasOne(x => x.Articulo).WithMany().HasForeignKey(x => x.ArticuloId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class MovimientoConfig : IEntityTypeConfiguration<Movimiento>
{
    public void Configure(EntityTypeBuilder<Movimiento> b)
    {
        b.ToTable(t => t.HasCheckConstraint("CK_MOVIMIENTO_cantidad", "[cantidad] > 0"));
        b.Property(x => x.Motivo).HasMaxLength(150).IsRequired();
        b.Property(x => x.FechaHora).HasDefaultValueSql("GETDATE()");
        b.HasIndex(x => new { x.ArticuloId, x.FechaHora });
        b.HasIndex(x => x.FechaHora);
        b.HasOne(x => x.Articulo).WithMany().HasForeignKey(x => x.ArticuloId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class SolicitudConfig : IEntityTypeConfiguration<Solicitud>
{
    public void Configure(EntityTypeBuilder<Solicitud> b)
    {
        b.ToTable(t => t.HasCheckConstraint("CK_SOLICITUD_cantidad", "[cantidad] > 0"));
        b.Property(x => x.Motivo).HasMaxLength(150).IsRequired();
        b.Property(x => x.MotivoRechazo).HasMaxLength(150);
        b.Property(x => x.Estado).HasDefaultValueSql("'Pendiente'").ValueGeneratedNever();
        b.Property(x => x.FechaSolicitud).HasDefaultValueSql("GETDATE()");
        b.HasIndex(x => x.Estado);
        b.HasOne(x => x.Articulo).WithMany().HasForeignKey(x => x.ArticuloId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Solicitante).WithMany().HasForeignKey(x => x.SolicitanteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Aprobador).WithMany().HasForeignKey(x => x.AprobadorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class SalidaHerramientaConfig : IEntityTypeConfiguration<SalidaHerramienta>
{
    public void Configure(EntityTypeBuilder<SalidaHerramienta> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_SALIDA_HERRAMIENTA_fechas", "[fecha_prevista] >= [fecha_salida]");
        });
        b.Property(x => x.Id).HasColumnName("id_salida");
        b.Property(x => x.Destino).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.FechaDevolucion);
        b.HasOne(x => x.Articulo).WithMany().HasForeignKey(x => x.ArticuloId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Responsable).WithMany().HasForeignKey(x => x.ResponsableId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class ConteoFisicoConfig : IEntityTypeConfiguration<ConteoFisico>
{
    public void Configure(EntityTypeBuilder<ConteoFisico> b)
    {
        b.Property(x => x.Id).HasColumnName("id_conteo");
        b.HasOne(x => x.Ubicacion).WithMany().HasForeignKey(x => x.UbicacionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Responsable).WithMany().HasForeignKey(x => x.ResponsableId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class ConteoDetalleConfig : IEntityTypeConfiguration<ConteoDetalle>
{
    public void Configure(EntityTypeBuilder<ConteoDetalle> b)
    {
        b.Property(x => x.ConteoFisicoId).HasColumnName("id_conteo");
        b.Property(x => x.Diferencia).HasComputedColumnSql("[cantidad_contada] - [stock_sistema]", stored: true);
        b.HasIndex(x => new { x.ConteoFisicoId, x.ArticuloId }).IsUnique();
        b.HasOne(x => x.ConteoFisico).WithMany(c => c.Detalles).HasForeignKey(x => x.ConteoFisicoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Articulo).WithMany().HasForeignKey(x => x.ArticuloId).OnDelete(DeleteBehavior.Restrict);
    }
}
