using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Articulos;
using Muebleria.Application.Features.Bajas;
using Muebleria.Application.Features.InventarioFisico;
using Muebleria.Application.Features.Movimientos;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;
using Muebleria.UnitTests.Apoyo;

namespace Muebleria.UnitTests.Servicios;

/// <summary>RF-26 y RF-27: mermas y bajas de herramientas.</summary>
public class BajaServiceTests
{
    private static BajaService Servicio(Escenario e) => new(e.Db, new MovimientoService(e.Db, e.Usuario), e.Usuario);

    [Fact(DisplayName = "RF-26: la merma resta existencia y guarda la causa en el motivo")]
    public async Task Merma_resta_y_guarda_causa()
    {
        using var e = new Escenario();
        var m = await Servicio(e.Como(e.Encargado)).RegistrarMermaAsync(new(e.Lija.Id, 4, CausaMerma.Danio, "se mojaron"), default);
        (m.Tipo, m.StockAnterior, m.StockNuevo).Should().Be((TipoMovimiento.Merma, 20m, 16m));
        m.Motivo.Should().Be("Merma por daño: se mojaron");
    }

    [Fact(DisplayName = "RF-26: no se registra merma por vencimiento de un producto que no ha vencido")]
    public async Task Merma_vencimiento_sin_vencer()
    {
        using var e = new Escenario();
        var accion = () => Servicio(e.Como(e.Encargado)).RegistrarMermaAsync(new(e.Tinte.Id, 1, CausaMerma.Vencimiento, null), default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*todavía no está vencido*");
    }

    [Fact(DisplayName = "RF-26: la merma mayor a la existencia no se guarda")]
    public async Task Merma_mayor_a_existencia()
    {
        using var e = new Escenario();
        var accion = () => Servicio(e.Como(e.Encargado)).RegistrarMermaAsync(new(e.Tinte.Id, 5, CausaMerma.Desperdicio, null), default);
        await accion.Should().ThrowAsync<StockInsuficienteException>();
        (await e.Db.Movimientos.CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "RF-27: al dar de baja la última unidad, la herramienta queda De baja y en el historial de estados")]
    public async Task Baja_ultima_unidad_cambia_estado()
    {
        using var e = new Escenario();
        var m = await Servicio(e.Como(e.Encargado)).DarDeBajaAsync(new(e.Taladro.Id, 1, "Motor quemado"), default);
        m.Tipo.Should().Be(TipoMovimiento.Baja);
        var t = await e.Db.Articulos.AsNoTracking().FirstAsync(a => a.Id == e.Taladro.Id);
        (t.StockActual, t.Estado, t.Activo).Should().Be((0m, EstadoArticulo.DeBaja, false));
        var h = await e.Db.HistorialEstados.SingleAsync();
        (h.EstadoAnterior, h.EstadoNuevo, h.UsuarioId).Should().Be((EstadoArticulo.Buena, EstadoArticulo.DeBaja, e.Encargado.Id));
    }

    [Fact(DisplayName = "RF-27: solo las herramientas se dan de baja")]
    public async Task Baja_solo_herramientas()
    {
        using var e = new Escenario();
        var accion = () => Servicio(e.Como(e.Encargado)).DarDeBajaAsync(new(e.Tinte.Id, 1, "x"), default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*no es una herramienta*");
    }

    [Fact(DisplayName = "El historial de bajas y mermas no muestra entradas ni salidas")]
    public async Task Historial_solo_bajas_y_mermas()
    {
        using var e = new Escenario();
        e.Como(e.Encargado);
        await new MovimientoService(e.Db, e.Usuario).RegistrarAsync(new(e.Lija.Id, TipoMovimiento.Salida, 1, "Uso"), default);
        await Servicio(e).RegistrarMermaAsync(new(e.Lija.Id, 1, CausaMerma.Desperdicio, null), default);
        var lista = await Servicio(e).ListarAsync(new(), default);
        lista.Items.Should().ContainSingle().Which.Tipo.Should().Be(TipoMovimiento.Merma);
    }
}

/// <summary>RF-28 y RF-29: conteo físico y ajuste de diferencias.</summary>
public class ConteoServiceTests
{
    [Fact(DisplayName = "RF-28: el conteo guarda la existencia del sistema de cada artículo activo")]
    public async Task Crear_toma_foto_del_sistema()
    {
        using var e = new Escenario();
        var c = await new ConteoService(e.Db, e.Como(e.Encargado).Usuario).CrearAsync(new(e.Bodega.Id, null), default);
        c.Detalles.Should().HaveCount(3);
        c.Detalles.Single(d => d.ArticuloId == e.Lija.Id).StockSistema.Should().Be(20);
        c.Detalles.Should().OnlyContain(d => d.CantidadContada == null);
        (c.Conteo.Estado, c.Conteo.Contados).Should().Be((EstadoConteo.Abierto, 0));
    }

    [Fact(DisplayName = "RF-28: no se abren dos conteos que cubren los mismos artículos")]
    public async Task No_dos_conteos_cruzados()
    {
        using var e = new Escenario();
        var s = new ConteoService(e.Db, e.Como(e.Encargado).Usuario);
        await s.CrearAsync(new(null, e.Lijas.Id), default);
        var accion = () => s.CrearAsync(new(e.Bodega.Id, null), default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*conteo abierto*");
    }

    [Fact(DisplayName = "RF-29: no se aplica un conteo con artículos sin contar")]
    public async Task No_aplica_incompleto()
    {
        using var e = new Escenario();
        var s = new ConteoService(e.Db, e.Como(e.Admin).Usuario);
        var c = await s.CrearAsync(new(null, null), default);
        await s.RegistrarAsync(c.Conteo.Id, new([new(c.Detalles[0].Id, 5)]), default);
        var accion = () => s.AplicarAsync(c.Conteo.Id, default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("Faltan contar 2 artículos.");
    }

    [Fact(DisplayName = "RF-29: cada diferencia se vuelve un ajuste y respeta lo que se movió mientras se contaba")]
    public async Task Aplicar_genera_ajustes()
    {
        using var e = new Escenario();
        e.Como(e.Admin);
        var s = new ConteoService(e.Db, e.Usuario);
        var c = await s.CrearAsync(new(null, null), default);
        var lija = c.Detalles.Single(d => d.ArticuloId == e.Lija.Id);
        var tinte = c.Detalles.Single(d => d.ArticuloId == e.Tinte.Id);
        var taladro = c.Detalles.Single(d => d.ArticuloId == e.Taladro.Id);

        // En el estante había 18 lijas (2 menos); el tinte y el taladro cuadran.
        await s.RegistrarAsync(c.Conteo.Id, new([new(lija.Id, 18), new(tinte.Id, 3), new(taladro.Id, 1)]), default);
        // Mientras tanto salieron 5 lijas al taller: el sistema tiene 15, y debe quedar en 13 (no en 18).
        await new MovimientoService(e.Db, e.Usuario).RegistrarAsync(new(e.Lija.Id, TipoMovimiento.Salida, 5, "Uso"), default);

        var r = await s.AplicarAsync(c.Conteo.Id, default);
        r.Conteo.Estado.Should().Be(EstadoConteo.Aplicado);
        r.Conteo.ConDiferencia.Should().Be(1);
        (await e.Db.Articulos.AsNoTracking().FirstAsync(a => a.Id == e.Lija.Id)).StockActual.Should().Be(13);
        var ajuste = await e.Db.Movimientos.SingleAsync(m => m.Tipo == TipoMovimiento.Ajuste);
        ajuste.Motivo.Should().Be($"Conteo físico #{c.Conteo.Id}: diferencia -2");
        (await e.Db.Movimientos.CountAsync(m => m.Tipo == TipoMovimiento.Ajuste)).Should().Be(1);
    }

    [Fact(DisplayName = "RF-29: un conteo aplicado ya no se modifica")]
    public async Task Aplicado_no_se_modifica()
    {
        using var e = new Escenario();
        var s = new ConteoService(e.Db, e.Como(e.Admin).Usuario);
        var c = await s.CrearAsync(new(null, e.Instalacion.Id), default);
        await s.RegistrarAsync(c.Conteo.Id, new([new(c.Detalles[0].Id, 1)]), default);
        await s.AplicarAsync(c.Conteo.Id, default);
        var accion = () => s.RegistrarAsync(c.Conteo.Id, new([new(c.Detalles[0].Id, 0)]), default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*ya fue aplicado*");
    }

    [Theory(DisplayName = "Dos filtros de conteo se cruzan si pueden incluir el mismo artículo")]
    [InlineData(1, null, 1, null, true)]
    [InlineData(1, null, 2, null, false)]
    [InlineData(1, null, null, 5, true)]
    [InlineData(1, 5, 1, 6, false)]
    [InlineData(null, null, 2, 3, true)]
    public void Filtros_se_cruzan(int? u1, int? c1, int? u2, int? c2, bool esperado) =>
        ConteoService.SeCruzan(u1, c1, u2, c2).Should().Be(esperado);
}

/// <summary>RF-08, RF-11, RF-13 y RF-15 en el catálogo.</summary>
public class CatalogoPendientesTests
{
    private sealed class AlmacenMemoria : IAlmacenFotos
    {
        public Dictionary<string, byte[]> Archivos { get; } = [];
        public async Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct)
        {
            using var m = new MemoryStream();
            await contenido.CopyToAsync(m, ct);
            var nombre = $"{Archivos.Count + 1}{extension}";
            Archivos[nombre] = m.ToArray();
            return nombre;
        }
        public Stream? Abrir(string nombre) => Archivos.TryGetValue(nombre, out var b) ? new MemoryStream(b) : null;
        public void Eliminar(string nombre) => Archivos.Remove(nombre);
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    [Fact(DisplayName = "RF-08: la foto se guarda, reemplaza a la anterior y se puede leer")]
    public async Task Foto_se_guarda_y_reemplaza()
    {
        using var e = new Escenario();
        var almacen = new AlmacenMemoria();
        var s = new FotoService(e.Db, almacen);
        e.Como(e.Encargado);
        await s.SubirAsync(e.Lija.Id, new MemoryStream(Png), Png.Length, default);
        await s.SubirAsync(e.Lija.Id, new MemoryStream(Png), Png.Length, default);
        almacen.Archivos.Should().ContainSingle();   // la primera se borró
        var foto = await s.ObtenerAsync(e.Lija.Id, default);
        foto.TipoContenido.Should().Be("image/png");
    }

    [Fact(DisplayName = "RF-08: un archivo que no es imagen se rechaza aunque diga .jpg")]
    public async Task Foto_no_imagen()
    {
        using var e = new Escenario();
        var texto = "hola, no soy una foto"u8.ToArray();
        var accion = () => new FotoService(e.Db, new AlmacenMemoria()).SubirAsync(e.Lija.Id, new MemoryStream(texto), texto.Length, default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*JPG, PNG o WEBP*");
    }

    [Theory(DisplayName = "RF-08: el tipo de imagen se reconoce por sus primeros bytes")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, ".jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, ".png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 }, ".webp")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38 }, null)]
    public void Detecta_tipo(byte[] cabecera, string? esperado) => FotoService.Detectar(cabecera).Should().Be(esperado);

    [Fact(DisplayName = "RF-11: el historial de estados muestra el cambio con quién lo hizo")]
    public async Task Historial_estados()
    {
        using var e = new Escenario();
        e.Como(e.Encargado);
        await new BajaService(e.Db, new MovimientoService(e.Db, e.Usuario), e.Usuario).DarDeBajaAsync(new(e.Taladro.Id, 1, "Quebrado"), default);
        var h = await new ArticuloService(e.Db, e.Usuario).HistorialEstadosAsync(e.Taladro.Id, default);
        h.Should().ContainSingle().Which.Should().Match<HistorialEstadoDto>(x => x.EstadoNuevo == EstadoArticulo.DeBaja && x.Usuario == e.Encargado.Nombre);
    }

    [Fact(DisplayName = "RF-13: los acabados se agrupan por producto y el lote que vence primero va arriba")]
    public async Task Acabados_por_lote()
    {
        using var e = new Escenario();
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var viejo = e.NuevoArticulo("ACA-TIN-2", "Tinte nogal", e.Acabados, 2, 1, hoy.AddDays(-3));
        viejo.Lote = "L-0";
        e.NuevoArticulo("ACA-LAC", "Laca transparente", e.Acabados, 1, 1, hoy.AddDays(200));
        await e.Db.SaveChangesAsync();

        var grupos = await new ArticuloService(e.Db, e.Usuario).AcabadosPorLoteAsync(null, default);
        grupos.Select(g => g.Producto).Should().Equal("Laca transparente", "Tinte nogal");
        var tinte = grupos[1];
        tinte.Lotes.Select(l => l.Lote).Should().Equal("L-0", "L-1");
        (tinte.ExistenciaTotal, tinte.LotesVencidos, tinte.LotesPorVencer).Should().Be((5m, 1, 1));
    }

    [Fact(DisplayName = "RF-15: se filtran los artículos por unidad de medida")]
    public async Task Filtro_unidad()
    {
        using var e = new Escenario();
        var tabla = e.NuevoArticulo("MAD-PIN", "Pino 1x4", e.Lijas, 10, 2);
        tabla.UnidadMedida = "metro";
        await e.Db.SaveChangesAsync();
        var r = await new ArticuloService(e.Db, e.Usuario).BuscarAsync(new(UnidadMedida: "metro"), default);
        r.Items.Should().ContainSingle().Which.Codigo.Should().Be("MAD-PIN");
    }
}
