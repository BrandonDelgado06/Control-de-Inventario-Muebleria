namespace Muebleria.Domain.Enums;

/// <summary>Separa los módulos de inventario (5 a 8) sin crear tablas distintas.</summary>
public enum TipoInventario { General, Herramienta, Acabado, MateriaPrima, Instalacion }

public enum TipoDato { Texto, Numero, Fecha, Lista }

public enum EstadoArticulo { Nueva, Buena, Desgastada, Danada, DeBaja }

public enum TipoMovimiento { Entrada, Salida, Ajuste, Baja, Merma }

public enum EstadoSolicitud { Pendiente, Aprobada, Rechazada, Entregada }

public enum EstadoConteo { Abierto, Aplicado }
