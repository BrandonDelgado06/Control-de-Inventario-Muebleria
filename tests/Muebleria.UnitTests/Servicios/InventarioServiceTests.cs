using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Features.Articulos;
using Muebleria.Application.Features.Instalacion;
using Muebleria.Application.Features.Movimientos;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;
using Muebleria.UnitTests.Apoyo;

namespace Muebleria.UnitTests.Servicios;

/// <summary>RF-19 a RF-22: entradas, salidas, ajustes e historial.</summary>
public class MovimientoServiceTests
{
    [Fact(DisplayName = "RF-19/RF-22: la entrada queda en el historial con existencia anterior y nueva")]
    public async Task Entrada_queda_en_historial()
    {
        using var e = new Escenario();
        var s = new MovimientoService(e.Db, e.Como(e.Encargado).Usuario);
        var m = await s.RegistrarAsync(new(e.Lija.Id, TipoMovimiento.Entrada, 10, "Compra"), default);
        (m.StockAnterior, m.StockNuevo, m.Usuario).Should().Be((20m, 30m, e.Encargado.Nombre));
        (await s.ListarAsync(new(ArticuloId: e.Lija.Id), default)).Items.Should().ContainSingle(x => x.Motivo == "Compra");
    }

    [Fact(DisplayName = "RF-20: la salida mayor a la existencia no se guarda")]
    public async Task Salida_mayor_no_se_guarda()
    {
        using var e = new Escenario();
        var s = new MovimientoService(e.Db, e.Como(e.Encargado).Usuario);
        var accion = () => s.RegistrarAsync(new(e.Lija.Id, TipoMovimiento.Salida, 21, "x"), default);
        await accion.Should().ThrowAsync<StockInsuficienteException>();
        (await e.Db.Movimientos.CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "RF-21: un ajuste que no cambia la existencia se rechaza")]
    public async Task Ajuste_sin_cambio()
    {
        using var e = new Escenario();
        var s = new MovimientoService(e.Db, e.Como(e.Admin).Usuario);
        var accion = () => s.RegistrarAsync(new(e.Lija.Id, TipoMovimiento.Ajuste, 20, "Conteo"), default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*no cambia nada*");
    }

    [Fact(DisplayName = "RF-21: el ajuste guarda la diferencia como cantidad")]
    public async Task Ajuste_guarda_diferencia()
    {
        using var e = new Escenario();
        var s = new MovimientoService(e.Db, e.Como(e.Admin).Usuario);
        var m = await s.RegistrarAsync(new(e.Lija.Id, TipoMovimiento.Ajuste, 17, "Conteo físico"), default);
        (m.Cantidad, m.StockAnterior, m.StockNuevo).Should().Be((3m, 20m, 17m));
    }

    [Fact(DisplayName = "Mermas y bajas no se registran desde Entradas y salidas")]
    public async Task Merma_no_desde_movimientos()
    {
        using var e = new Escenario();
        var s = new MovimientoService(e.Db, e.Como(e.Admin).Usuario);
        var accion = () => s.RegistrarAsync(new(e.Lija.Id, TipoMovimiento.Merma, 1, "x"), default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*propio módulo*");
    }
}

/// <summary>RF-07 a RF-09: registro de artículos, campos personalizados y búsqueda.</summary>
public class ArticuloServiceTests
{
    private static ArticuloRequest Peticion(Escenario e, string codigo = "lij-120", IReadOnlyDictionary<int, string?>? valores = null,
        IReadOnlyList<string>? alias = null, decimal? inicial = null) =>
        new(codigo, "Lija grano 120", e.Lijas.Id, e.Bodega.Id, "pieza", 5, EstadoArticulo.Nueva, null, null, null, alias, valores, inicial);

    [Fact(DisplayName = "RF-08: el código no se puede repetir (sin importar mayúsculas)")]
    public async Task Codigo_repetido()
    {
        using var e = new Escenario();
        var s = new ArticuloService(e.Db, e.Como(e.Admin).Usuario);
        var accion = () => s.CrearAsync(Peticion(e, "lij-220"), default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*LIJ-220*ya está registrado*");
    }

    [Fact(DisplayName = "RF-08: la existencia inicial se registra como una entrada")]
    public async Task Existencia_inicial_es_entrada()
    {
        using var e = new Escenario();
        var s = new ArticuloService(e.Db, e.Como(e.Admin).Usuario);
        var d = await s.CrearAsync(Peticion(e, inicial: 40), default);
        d.Articulo.Codigo.Should().Be("LIJ-120");
        d.Articulo.StockActual.Should().Be(40);
        (await e.Db.Movimientos.SingleAsync(m => m.ArticuloId == d.Articulo.Id)).Motivo.Should().Be("Existencia inicial");
    }

    [Fact(DisplayName = "RF-07: un campo personalizado obligatorio debe llenarse")]
    public async Task Campo_obligatorio()
    {
        using var e = new Escenario();
        e.Lijas.Campos.Add(new CampoPersonalizado { Nombre = "Número de grano", TipoDato = TipoDato.Lista, Obligatorio = true, Opciones = "80|120|220" });
        await e.Db.SaveChangesAsync();
        var s = new ArticuloService(e.Db, e.Como(e.Admin).Usuario);

        var sinValor = () => s.CrearAsync(Peticion(e), default);
        await sinValor.Should().ThrowAsync<DomainException>().WithMessage("*Número de grano*obligatorio*");

        var campoId = e.Lijas.Campos[0].Id;
        var fueraDeLista = () => s.CrearAsync(Peticion(e, valores: new Dictionary<int, string?> { [campoId] = "121" }), default);
        await fueraDeLista.Should().ThrowAsync<DomainException>().WithMessage("*no es válido*");

        var ok = await s.CrearAsync(Peticion(e, valores: new Dictionary<int, string?> { [campoId] = "120" }), default);
        ok.Campos.Should().ContainSingle(c => c.Valor == "120");
    }

    [Fact(DisplayName = "Limitación 2: el artículo se encuentra por sus otros nombres")]
    public async Task Busqueda_por_alias()
    {
        using var e = new Escenario();
        var s = new ArticuloService(e.Db, e.Como(e.Admin).Usuario);
        await s.CrearAsync(Peticion(e, alias: ["lija media", "papel de agua"]), default);
        var r = await s.BuscarAsync(new(Texto: "papel de agua"), default);
        r.Items.Should().ContainSingle(a => a.Codigo == "LIJ-120");
    }

    [Fact(DisplayName = "RF-12: un producto de acabado exige fecha de vencimiento")]
    public async Task Acabado_exige_vencimiento()
    {
        using var e = new Escenario();
        var s = new ArticuloService(e.Db, e.Como(e.Admin).Usuario);
        var req = Peticion(e, "ACA-NUEVO") with { CategoriaId = e.Acabados.Id };
        var accion = () => s.CrearAsync(req, default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*vencimiento*");
    }

    [Fact(DisplayName = "RF-11: cambiar el estado deja historial")]
    public async Task Cambio_de_estado_deja_historial()
    {
        using var e = new Escenario();
        var s = new ArticuloService(e.Db, e.Como(e.Encargado).Usuario);
        var req = new ArticuloRequest("LIJ-220", "Lija grano 220", e.Lijas.Id, e.Bodega.Id, "pieza", 10, EstadoArticulo.Desgastada, null, null, null, null, null, null);
        await s.ActualizarAsync(e.Lija.Id, req, default);
        var h = await e.Db.HistorialEstados.SingleAsync();
        (h.EstadoAnterior, h.EstadoNuevo, h.UsuarioId).Should().Be((EstadoArticulo.Buena, EstadoArticulo.Desgastada, e.Encargado.Id));
    }
}

/// <summary>RF-16 a RF-18: herramientas de instalación.</summary>
public class InstalacionServiceTests
{
    private static DateOnly Manana => DateOnly.FromDateTime(DateTime.Today).AddDays(1);

    [Fact(DisplayName = "RF-16: la salida deja la herramienta como no disponible")]
    public async Task Salida_descuenta_unidad()
    {
        using var e = new Escenario();
        var s = new InstalacionService(e.Db, e.Como(e.Encargado).Usuario);
        var r = await s.RegistrarSalidaAsync(new(e.Taladro.Id, e.Trabajador.Id, "Casa Mora", Manana), default);
        r.Responsable.Should().Be(e.Trabajador.Nombre);
        (await e.Db.Articulos.FindAsync(e.Taladro.Id))!.StockActual.Should().Be(0);

        var otra = () => s.RegistrarSalidaAsync(new(e.Taladro.Id, e.Trabajador.Id, "Otra casa", Manana), default);
        await otra.Should().ThrowAsync<DomainException>().WithMessage("*todas están fuera*");
    }

    [Fact(DisplayName = "RF-16: solo salen herramientas de instalación y con fecha de regreso futura")]
    public async Task Validaciones_de_salida()
    {
        using var e = new Escenario();
        var s = new InstalacionService(e.Db, e.Como(e.Encargado).Usuario);
        var noEsInstalacion = () => s.RegistrarSalidaAsync(new(e.Lija.Id, e.Trabajador.Id, "x", Manana), default);
        await noEsInstalacion.Should().ThrowAsync<DomainException>().WithMessage("*no es una herramienta de instalación*");
        var ayer = () => s.RegistrarSalidaAsync(new(e.Taladro.Id, e.Trabajador.Id, "x", Manana.AddDays(-2)), default);
        await ayer.Should().ThrowAsync<DomainException>().WithMessage("*anterior a hoy*");
    }

    [Fact(DisplayName = "RF-17: la devolución repone la unidad y actualiza el estado si regresó dañada")]
    public async Task Devolucion_danada()
    {
        using var e = new Escenario();
        var s = new InstalacionService(e.Db, e.Como(e.Encargado).Usuario);
        var salida = await s.RegistrarSalidaAsync(new(e.Taladro.Id, e.Trabajador.Id, "Casa Mora", Manana), default);
        var r = await s.DevolverAsync(salida.Id, new(EstadoArticulo.Danada), default);

        r.FechaDevolucion.Should().NotBeNull();
        var taladro = (await e.Db.Articulos.FindAsync(e.Taladro.Id))!;
        (taladro.StockActual, taladro.Estado).Should().Be((1m, EstadoArticulo.Danada));
        (await e.Db.HistorialEstados.CountAsync()).Should().Be(1);

        var otraVez = () => s.DevolverAsync(salida.Id, new(EstadoArticulo.Buena), default);
        await otraVez.Should().ThrowAsync<DomainException>().WithMessage("*ya fue devuelta*");
    }

    [Fact(DisplayName = "RF-18: los días de retraso se cuentan desde la fecha prevista")]
    public void Dias_de_retraso()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var s = new SalidaHerramienta { FechaSalida = hoy.AddDays(-6), FechaPrevista = hoy.AddDays(-3), Destino = "x" };
        InstalacionService.ADto(s, "c", "h", "r").DiasRetraso.Should().Be(3);
        s.FechaPrevista = hoy.AddDays(2);
        InstalacionService.ADto(s, "c", "h", "r").DiasRetraso.Should().Be(0);
    }
}
