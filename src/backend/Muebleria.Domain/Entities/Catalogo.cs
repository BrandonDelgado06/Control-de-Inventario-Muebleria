using Muebleria.Domain.Common;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Domain.Entities;

public class Ubicacion : IAuditable
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
}

public class Categoria : IAuditable
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public TipoInventario TipoInventario { get; set; }
    public bool Activo { get; set; } = true;
    public List<CampoPersonalizado> Campos { get; set; } = [];
}

public class CampoPersonalizado : IAuditable
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;
    public string Nombre { get; set; } = "";
    public TipoDato TipoDato { get; set; }
    public bool Obligatorio { get; set; }
    /// <summary>Opciones separadas por "|" cuando el tipo es Lista.</summary>
    public string? Opciones { get; set; }
    public int Orden { get; set; }
}

public class Articulo : IAuditable
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;
    public int UbicacionId { get; set; }
    public Ubicacion Ubicacion { get; set; } = null!;
    public string UnidadMedida { get; set; } = "pieza";
    public decimal StockActual { get; private set; }
    public decimal StockMinimo { get; set; }
    public EstadoArticulo Estado { get; set; } = EstadoArticulo.Buena;
    public string? Lote { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public string? Presentacion { get; set; }
    public string? FotoRuta { get; set; }
    public bool Activo { get; set; } = true;
    /// <summary>rowversion: si dos usuarios cambian el stock a la vez, el segundo recibe un conflicto.</summary>
    public byte[] Version { get; set; } = [];
    public List<ArticuloValorCampo> Valores { get; set; } = [];
    public List<ArticuloAlias> Alias { get; set; } = [];

    public bool BajoMinimo => StockActual <= StockMinimo;

    /// <summary>
    /// Único punto donde cambia la existencia. Entrada suma; Salida, Baja y Merma restan;
    /// Ajuste fija la existencia en <paramref name="cantidad"/>. Devuelve el stock anterior y el nuevo.
    /// </summary>
    public (decimal anterior, decimal nuevo) AplicarMovimiento(TipoMovimiento tipo, decimal cantidad)
    {
        if (cantidad < 0 || (tipo != TipoMovimiento.Ajuste && cantidad == 0))
            throw new DomainException("La cantidad debe ser mayor que cero.");
        if (!Activo)
            throw new DomainException($"El artículo \"{Nombre}\" está desactivado.");

        var anterior = StockActual;
        var nuevo = tipo switch
        {
            TipoMovimiento.Entrada => anterior + cantidad,
            TipoMovimiento.Ajuste => cantidad,
            _ => anterior - cantidad,
        };
        if (nuevo < 0)
            throw new StockInsuficienteException(Nombre, anterior, cantidad);

        StockActual = nuevo;
        return (anterior, nuevo);
    }
}

public class ArticuloValorCampo
{
    public int ArticuloId { get; set; }
    public Articulo Articulo { get; set; } = null!;
    public int CampoId { get; set; }
    public CampoPersonalizado Campo { get; set; } = null!;
    public string Valor { get; set; } = "";
}

/// <summary>Otros nombres con que se conoce un artículo en el taller (limitación 2 del documento).</summary>
public class ArticuloAlias
{
    public int Id { get; set; }
    public int ArticuloId { get; set; }
    public Articulo Articulo { get; set; } = null!;
    public string Alias { get; set; } = "";
}

public class HistorialEstado
{
    public int Id { get; set; }
    public int ArticuloId { get; set; }
    public Articulo Articulo { get; set; } = null!;
    public EstadoArticulo EstadoAnterior { get; set; }
    public EstadoArticulo EstadoNuevo { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public DateTime FechaHora { get; set; }
}
