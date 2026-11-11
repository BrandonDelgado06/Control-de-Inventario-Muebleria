using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Features.Solicitudes;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;
using Muebleria.UnitTests.Apoyo;

namespace Muebleria.UnitTests.Servicios;

/// <summary>RF-23 a RF-25.</summary>
public class SolicitudServiceTests
{
    private static (Escenario e, SolicitudService s) Crear()
    {
        var e = new Escenario();
        return (e, new SolicitudService(e.Db, e.Usuario));
    }

    [Fact(DisplayName = "RF-23: la solicitud nueva queda pendiente y a nombre de quien la pidió")]
    public async Task Crear_queda_pendiente()
    {
        var (e, s) = Crear();
        var dto = await s.Como(e, e.Trabajador).CrearAsync(new(e.Lija.Id, 5, "Lijar cómoda"), default);
        dto.Estado.Should().Be(EstadoSolicitud.Pendiente);
        dto.SolicitanteId.Should().Be(e.Trabajador.Id);
        dto.Aprobador.Should().BeNull();
    }

    [Fact(DisplayName = "RF-24: el trabajador solo ve sus propias solicitudes")]
    public async Task Trabajador_ve_solo_las_suyas()
    {
        var (e, s) = Crear();
        await s.Como(e, e.Trabajador).CrearAsync(new(e.Lija.Id, 1, "a"), default);
        await s.Como(e, e.Encargado).CrearAsync(new(e.Lija.Id, 1, "b"), default);

        (await s.Como(e, e.Trabajador).ListarAsync(new(), verTodas: false, default)).Total.Should().Be(1);
        (await s.Como(e, e.Encargado).ListarAsync(new(), verTodas: true, default)).Total.Should().Be(2);
    }

    [Fact(DisplayName = "RF-25: entregar lo aprobado descuenta la existencia y registra la salida")]
    public async Task Aprobar_y_entregar_descuenta_existencia()
    {
        var (e, s) = Crear();
        var sol = await s.Como(e, e.Trabajador).CrearAsync(new(e.Lija.Id, 5, "Lijar"), default);

        (await s.Como(e, e.Encargado).AprobarAsync(sol.Id, default)).Estado.Should().Be(EstadoSolicitud.Aprobada);
        var entregada = await s.EntregarAsync(sol.Id, default);

        entregada.Estado.Should().Be(EstadoSolicitud.Entregada);
        entregada.FechaEntrega.Should().NotBeNull();
        entregada.StockActual.Should().Be(15);
        var mov = await e.Db.Movimientos.SingleAsync(m => m.Motivo == $"Entrega de la solicitud #{sol.Id}");
        mov.Tipo.Should().Be(TipoMovimiento.Salida);
        (mov.StockAnterior, mov.StockNuevo).Should().Be((20m, 15m));
    }

    [Fact(DisplayName = "RF-25: no se puede entregar una solicitud sin aprobar")]
    public async Task Entregar_sin_aprobar_falla()
    {
        var (e, s) = Crear();
        var sol = await s.Como(e, e.Trabajador).CrearAsync(new(e.Lija.Id, 5, "x"), default);
        var accion = () => s.Como(e, e.Encargado).EntregarAsync(sol.Id, default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*no está aprobada*");
        (await e.Db.Articulos.FindAsync(e.Lija.Id))!.StockActual.Should().Be(20);
    }

    [Fact(DisplayName = "RF-25: si no alcanza la existencia, la entrega falla y la solicitud sigue aprobada")]
    public async Task Entregar_sin_existencia_suficiente_falla()
    {
        var (e, s) = Crear();
        var sol = await s.Como(e, e.Trabajador).CrearAsync(new(e.Lija.Id, 50, "x"), default);
        await s.Como(e, e.Encargado).AprobarAsync(sol.Id, default);
        var accion = () => s.EntregarAsync(sol.Id, default);
        await accion.Should().ThrowAsync<StockInsuficienteException>();
        e.Db.ChangeTracker.Clear();
        (await e.Db.Solicitudes.FindAsync(sol.Id))!.Estado.Should().Be(EstadoSolicitud.Aprobada);
    }

    [Fact(DisplayName = "RF-24: el rechazo guarda el motivo y no se puede aprobar después")]
    public async Task Rechazar_guarda_motivo()
    {
        var (e, s) = Crear();
        var sol = await s.Como(e, e.Trabajador).CrearAsync(new(e.Lija.Id, 2, "x"), default);
        var r = await s.Como(e, e.Encargado).RechazarAsync(sol.Id, new("Sin presupuesto"), default);
        r.Estado.Should().Be(EstadoSolicitud.Rechazada);
        r.MotivoRechazo.Should().Be("Sin presupuesto");
        r.Aprobador.Should().Be(e.Encargado.Nombre);

        var accion = () => s.AprobarAsync(sol.Id, default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*ya fue rechazada*");
    }

    [Fact(DisplayName = "RF-23: no se puede pedir un artículo desactivado")]
    public async Task Articulo_desactivado()
    {
        var (e, s) = Crear();
        e.Lija.Activo = false; await e.Db.SaveChangesAsync();
        var accion = () => s.Como(e, e.Trabajador).CrearAsync(new(e.Lija.Id, 1, "x"), default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*desactivado*");
    }
}

internal static class EscenarioExtensions
{
    /// <summary>Permite escribir s.Como(e, usuario).Metodo(...) para cambiar quién está en sesión.</summary>
    public static T Como<T>(this T servicio, Escenario e, Domain.Entities.Usuario u) { e.Como(u); return servicio; }
}
