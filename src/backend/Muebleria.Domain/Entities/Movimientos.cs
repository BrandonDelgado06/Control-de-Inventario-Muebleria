using Muebleria.Domain.Enums;

namespace Muebleria.Domain.Entities;

// El orden de las propiedades define el orden de las columnas en SQL Server:
// primero las del diccionario de datos, en su orden; al final las que agrega algún RF.

/// <summary>Tabla 6 del diccionario de datos.</summary>
public class Movimiento
{
    public int Id { get; set; }
    public int ArticuloId { get; set; }
    public Articulo Articulo { get; set; } = null!;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public TipoMovimiento Tipo { get; set; }
    public decimal Cantidad { get; set; }
    public string Motivo { get; set; } = "";
    public DateTime FechaHora { get; set; }
    // RF-21: valor anterior y valor nuevo del ajuste.
    public decimal StockAnterior { get; set; }
    public decimal StockNuevo { get; set; }
}

/// <summary>Tabla 7 del diccionario de datos.</summary>
public class Solicitud
{
    public int Id { get; set; }
    public int ArticuloId { get; set; }
    public Articulo Articulo { get; set; } = null!;
    public int SolicitanteId { get; set; }
    public Usuario Solicitante { get; set; } = null!;
    public int? AprobadorId { get; set; }
    public Usuario? Aprobador { get; set; }
    public decimal Cantidad { get; set; }
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;
    public DateTime FechaSolicitud { get; set; }
    public DateTime? FechaResolucion { get; set; }
    // RF-23: motivo de la solicitud. RF-24: motivo del rechazo. RF-25: fecha de entrega.
    public string Motivo { get; set; } = "";
    public string? MotivoRechazo { get; set; }
    public DateTime? FechaEntrega { get; set; }
}

/// <summary>Tabla 8 del diccionario de datos. Cada registro es la salida de una herramienta.</summary>
public class SalidaHerramienta
{
    public int Id { get; set; }
    public int ArticuloId { get; set; }
    public Articulo Articulo { get; set; } = null!;
    public int ResponsableId { get; set; }
    public Usuario Responsable { get; set; } = null!;
    public string Destino { get; set; } = "";
    public DateOnly FechaSalida { get; set; }
    public DateOnly FechaPrevista { get; set; }
    public DateOnly? FechaDevolucion { get; set; }
    public EstadoArticulo? EstadoDevolucion { get; set; }
}

/// <summary>RF-28: cabecera del conteo físico (tabla nueva, no está en el documento).</summary>
public class ConteoFisico
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public int? UbicacionId { get; set; }
    public Ubicacion? Ubicacion { get; set; }
    public int? CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }
    public int ResponsableId { get; set; }
    public Usuario Responsable { get; set; } = null!;
    public EstadoConteo Estado { get; set; } = EstadoConteo.Abierto;
    public List<ConteoDetalle> Detalles { get; set; } = [];
}

/// <summary>RF-28 y RF-29: detalle del conteo físico (tabla nueva, no está en el documento).</summary>
public class ConteoDetalle
{
    public int Id { get; set; }
    public int ConteoFisicoId { get; set; }
    public ConteoFisico ConteoFisico { get; set; } = null!;
    public int ArticuloId { get; set; }
    public Articulo Articulo { get; set; } = null!;
    public decimal StockSistema { get; set; }
    public decimal CantidadContada { get; set; }
    /// <summary>Columna calculada en la base de datos: contada − sistema.</summary>
    public decimal Diferencia { get; private set; }
}
