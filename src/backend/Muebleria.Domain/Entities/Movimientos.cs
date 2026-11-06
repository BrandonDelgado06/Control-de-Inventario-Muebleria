using Muebleria.Domain.Enums;

namespace Muebleria.Domain.Entities;

public class Movimiento
{
    public int Id { get; set; }
    public int ArticuloId { get; set; }
    public Articulo Articulo { get; set; } = null!;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public TipoMovimiento Tipo { get; set; }
    public decimal Cantidad { get; set; }
    public decimal StockAnterior { get; set; }
    public decimal StockNuevo { get; set; }
    public string Motivo { get; set; } = "";
    public DateTime FechaHora { get; set; }
    /// <summary>Solicitud o conteo físico que originó el movimiento, si aplica.</summary>
    public int? ReferenciaId { get; set; }
}

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
    public string Motivo { get; set; } = "";
    public string? MotivoRechazo { get; set; }
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;
    public DateTime FechaSolicitud { get; set; }
    public DateTime? FechaResolucion { get; set; }
    public DateTime? FechaEntrega { get; set; }
}

public class SalidaHerramienta
{
    public int Id { get; set; }
    public int ArticuloId { get; set; }
    public Articulo Articulo { get; set; } = null!;
    public int ResponsableId { get; set; }
    public Usuario Responsable { get; set; } = null!;
    public decimal Cantidad { get; set; } = 1;
    public string Destino { get; set; } = "";
    public DateOnly FechaSalida { get; set; }
    public DateOnly FechaPrevista { get; set; }
    public DateOnly? FechaDevolucion { get; set; }
    public EstadoArticulo? EstadoDevolucion { get; set; }
}

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
