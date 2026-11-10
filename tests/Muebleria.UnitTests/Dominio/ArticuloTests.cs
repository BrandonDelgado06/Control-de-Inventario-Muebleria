using FluentAssertions;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;

namespace Muebleria.UnitTests.Dominio;

/// <summary>Regla central del inventario: cómo cambia la existencia (RF-19, RF-20, RF-21).</summary>
public class ArticuloTests
{
    private static Articulo ConExistencia(decimal n)
    {
        var a = new Articulo { Nombre = "Lija grano 220", StockMinimo = 5 };
        if (n > 0) a.AplicarMovimiento(TipoMovimiento.Entrada, n);
        return a;
    }

    [Fact(DisplayName = "RF-19: una entrada suma a la existencia")]
    public void Entrada_suma()
    {
        var a = ConExistencia(10);
        var (anterior, nuevo) = a.AplicarMovimiento(TipoMovimiento.Entrada, 2.5m);
        anterior.Should().Be(10);
        nuevo.Should().Be(12.5m);
        a.StockActual.Should().Be(12.5m);
    }

    [Theory(DisplayName = "RF-20: salidas, mermas y bajas restan")]
    [InlineData(TipoMovimiento.Salida)]
    [InlineData(TipoMovimiento.Merma)]
    [InlineData(TipoMovimiento.Baja)]
    public void Salida_merma_y_baja_restan(TipoMovimiento tipo)
    {
        var a = ConExistencia(10);
        a.AplicarMovimiento(tipo, 4).nuevo.Should().Be(6);
    }

    [Fact(DisplayName = "RF-20: no permite una salida mayor a la existencia")]
    public void Salida_mayor_a_existencia_falla_y_no_cambia_el_stock()
    {
        var a = ConExistencia(3);
        var accion = () => a.AplicarMovimiento(TipoMovimiento.Salida, 5);
        accion.Should().Throw<StockInsuficienteException>().WithMessage("*disponible 3, solicitado 5*");
        a.StockActual.Should().Be(3);
    }

    [Fact(DisplayName = "RF-20: se puede sacar exactamente todo lo que hay")]
    public void Salida_de_todo_deja_cero()
    {
        ConExistencia(3).AplicarMovimiento(TipoMovimiento.Salida, 3).nuevo.Should().Be(0);
    }

    [Fact(DisplayName = "RF-21: el ajuste fija la existencia en el valor indicado")]
    public void Ajuste_fija_valor()
    {
        var a = ConExistencia(10);
        var (anterior, nuevo) = a.AplicarMovimiento(TipoMovimiento.Ajuste, 7);
        (anterior, nuevo).Should().Be((10m, 7m));
    }

    [Fact(DisplayName = "RF-21: un ajuste a cero es válido")]
    public void Ajuste_a_cero() => ConExistencia(4).AplicarMovimiento(TipoMovimiento.Ajuste, 0).nuevo.Should().Be(0);

    [Theory(DisplayName = "Cantidad cero o negativa no es válida")]
    [InlineData(TipoMovimiento.Entrada, 0)]
    [InlineData(TipoMovimiento.Salida, 0)]
    [InlineData(TipoMovimiento.Entrada, -1)]
    [InlineData(TipoMovimiento.Ajuste, -1)]
    public void Cantidad_invalida(TipoMovimiento tipo, decimal cantidad)
    {
        var accion = () => ConExistencia(5).AplicarMovimiento(tipo, cantidad);
        accion.Should().Throw<DomainException>().WithMessage("*mayor que cero*");
    }

    [Fact(DisplayName = "RF-09: un artículo desactivado no admite movimientos")]
    public void Desactivado_no_admite_movimientos()
    {
        var a = ConExistencia(5);
        a.Activo = false;
        var accion = () => a.AplicarMovimiento(TipoMovimiento.Entrada, 1);
        accion.Should().Throw<DomainException>().WithMessage("*desactivado*");
    }

    [Theory(DisplayName = "RF-30: está bajo mínimo cuando la existencia es igual o menor al mínimo")]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Bajo_minimo(decimal existencia, bool esperado) => ConExistencia(existencia).BajoMinimo.Should().Be(esperado);
}
