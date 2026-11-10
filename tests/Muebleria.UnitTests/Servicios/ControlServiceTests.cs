using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Alertas;
using Muebleria.Application.Features.Reportes;
using Muebleria.Application.Features.Respaldos;
using Muebleria.Application.Features.Usuarios;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;
using Muebleria.Infrastructure.Security;
using Muebleria.UnitTests.Apoyo;

namespace Muebleria.UnitTests.Servicios;

/// <summary>RF-30 a RF-32 y aviso de respaldo (RNF-07).</summary>
public class AlertaServiceTests
{
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    [Fact(DisplayName = "RF-30: avisa los artículos en o bajo su mínimo")]
    public async Task Stock_minimo()
    {
        using var e = new Escenario();
        e.NuevoArticulo("LIJ-080", "Lija 80", e.Lijas, existencia: 4, minimo: 10);   // bajo mínimo
        e.NuevoArticulo("LIJ-100", "Lija 100", e.Lijas, existencia: 10, minimo: 10); // igual al mínimo
        await e.Db.SaveChangesAsync();
        var a = await new AlertaService(e.Db, new RespaldosFalsos(), e.Usuario).ListarAsync(false, default);
        a.StockMinimo.Select(x => x.Codigo).Should().BeEquivalentTo(["LIJ-080", "LIJ-100"]);
    }

    [Fact(DisplayName = "RF-31: avisa lo que vence dentro de la anticipación configurada")]
    public async Task Vencimientos_segun_anticipacion()
    {
        using var e = new Escenario();   // el tinte vence en 10 días
        e.NuevoArticulo("ACA-VEN", "Fondo vencido", e.Acabados, existencia: 1, minimo: 0, vence: Hoy.AddDays(-2));
        e.NuevoArticulo("ACA-LEJ", "Catalizador", e.Acabados, existencia: 1, minimo: 0, vence: Hoy.AddDays(60));
        await e.Db.SaveChangesAsync();
        var s = new AlertaService(e.Db, new RespaldosFalsos(), e.Como(e.Admin).Usuario);

        var a = await s.ListarAsync(false, default);   // 30 días por defecto
        a.Vencimientos.Select(v => (v.Codigo, v.DiasRestantes)).Should().BeEquivalentTo([("ACA-VEN", -2), ("ACA-TIN", 10)]);

        await s.ConfigurarAsync(new(5), default);
        (await s.ListarAsync(false, default)).Vencimientos.Should().ContainSingle(v => v.Codigo == "ACA-VEN");
        (await e.Db.Bitacoras.SingleAsync()).Accion.Should().Contain("a 5 días");
    }

    [Fact(DisplayName = "RF-32: avisa las herramientas que no volvieron a tiempo")]
    public async Task Herramientas_sin_devolver()
    {
        using var e = new Escenario();
        e.Db.SalidasHerramienta.AddRange(
            new SalidaHerramienta { Articulo = e.Taladro, Responsable = e.Trabajador, Destino = "Atrasada", FechaSalida = Hoy.AddDays(-5), FechaPrevista = Hoy.AddDays(-1) },
            new SalidaHerramienta { Articulo = e.Taladro, Responsable = e.Trabajador, Destino = "A tiempo", FechaSalida = Hoy, FechaPrevista = Hoy.AddDays(3) },
            new SalidaHerramienta { Articulo = e.Taladro, Responsable = e.Trabajador, Destino = "Devuelta", FechaSalida = Hoy.AddDays(-9), FechaPrevista = Hoy.AddDays(-7), FechaDevolucion = Hoy.AddDays(-6) });
        await e.Db.SaveChangesAsync();
        var a = await new AlertaService(e.Db, new RespaldosFalsos(), e.Usuario).ListarAsync(false, default);
        a.HerramientasSinDevolver.Should().ContainSingle(h => h.Destino == "Atrasada" && h.DiasRetraso == 1);
    }

    [Fact(DisplayName = "RNF-07: avisa si no hay respaldo o si tiene una semana o más")]
    public async Task Aviso_de_respaldo()
    {
        using var e = new Escenario();
        var respaldos = new RespaldosFalsos();
        var s = new AlertaService(e.Db, respaldos, e.Usuario);
        (await s.ListarAsync(true, default)).Respaldo!.Mensaje.Should().Contain("ninguna copia");

        respaldos.Lista.Add(new RespaldoInfo(1, "a.bak", DateTime.Today.AddDays(-8), 1, null));
        (await s.ListarAsync(true, default)).Respaldo!.DiasDesdeUltimo.Should().Be(8);

        respaldos.Lista.Insert(0, new RespaldoInfo(2, "b.bak", DateTime.Now, 1, null));
        (await s.ListarAsync(true, default)).Respaldo.Should().BeNull();
        (await s.ListarAsync(false, default)).Respaldo.Should().BeNull("el encargado no ve el aviso de respaldos");
    }
}

/// <summary>RF-33 y RF-34.</summary>
public class ReporteServiceTests
{
    private sealed class SinExportar : IExportadorReportes
    {
        public byte[] Pdf(ReporteDto r) => [];
        public byte[] Excel(ReporteDto r) => [];
    }

    [Fact(DisplayName = "RF-34: el consumo suma salidas y mermas y no cuenta herramientas de instalación")]
    public async Task Consumo_excluye_instalacion()
    {
        using var e = new Escenario();
        e.Como(e.Encargado);
        foreach (var (a, tipo, n) in new[] { (e.Lija, TipoMovimiento.Salida, 3m), (e.Lija, TipoMovimiento.Merma, 1m), (e.Lija, TipoMovimiento.Entrada, 5m), (e.Taladro, TipoMovimiento.Salida, 1m) })
            e.Db.Movimientos.Add(Application.Features.Movimientos.MovimientoService.Crear(a, tipo, n, "x", e.Encargado.Id));
        await e.Db.SaveChangesAsync();

        var r = await new ReporteService(e.Db, e.Usuario, new SinExportar()).GenerarAsync("consumo", new(), default);
        r.Filas.Should().ContainSingle();
        r.Filas[0][1].Should().Be("LIJ-220");
        r.Filas[0][4].Should().Be(2);      // dos salidas (salida + merma)
        r.Filas[0][5].Should().Be(4m);     // 3 + 1
    }

    [Fact(DisplayName = "RF-33: existencias filtra por categoría y marca lo que está bajo mínimo")]
    public async Task Existencias_por_categoria()
    {
        using var e = new Escenario();
        var r = await new ReporteService(e.Db, e.Como(e.Admin).Usuario, new SinExportar()).GenerarAsync("existencias", new(CategoriaId: e.Acabados.Id), default);
        r.Filas.Should().ContainSingle();
        r.Filtros.Should().Contain("Categoría: Productos de acabado");
        r.Filas[0].Last().Should().Be("No");
    }

    [Fact(DisplayName = "RF-33: un período con la fecha inicial después de la final se rechaza")]
    public async Task Periodo_invalido()
    {
        using var e = new Escenario();
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var accion = () => new ReporteService(e.Db, e.Como(e.Admin).Usuario, new SinExportar()).GenerarAsync("movimientos", new(Desde: hoy, Hasta: hoy.AddDays(-1)), default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*fecha inicial*");
    }
}

/// <summary>RF-38 y RF-39.</summary>
public class RespaldoServiceTests
{
    [Fact(DisplayName = "RF-39: restaurar exige escribir RESTAURAR y antes guarda una copia del estado actual")]
    public async Task Restaurar_con_confirmacion()
    {
        using var e = new Escenario();
        var respaldos = new RespaldosFalsos();
        var s = new RespaldoService(e.Db, respaldos, e.Como(e.Admin).Usuario);
        var r = await s.GenerarAsync(default);

        var mal = () => s.RestaurarAsync(new(r.Id, "restaurar"), default);
        await mal.Should().ThrowAsync<DomainException>().WithMessage("*RESTAURAR*");
        respaldos.Lista.Should().HaveCount(1);

        await s.RestaurarAsync(new(r.Id, "RESTAURAR"), default);
        respaldos.Lista.Should().HaveCount(2);
        respaldos.Lista[0].Descripcion.Should().Be("Automático antes de restaurar");
        (await e.Db.Bitacoras.Select(b => b.Accion).ToListAsync()).Should().Contain(x => x.StartsWith("Restaurar"));
    }
}

/// <summary>RF-01, RF-02, RF-03 y RNF-01.</summary>
public class SeguridadTests
{
    [Fact(DisplayName = "RNF-01: la contraseña se guarda como hash con sal y se puede verificar")]
    public void Hash_de_contrasena()
    {
        var h = new HasherContrasenas();
        var a = h.Hash("Bodega123!");
        var b = h.Hash("Bodega123!");
        a.Should().NotContain("Bodega123!").And.NotBe(b, "cada hash lleva su propia sal");
        h.Verificar(a, "Bodega123!").Should().BeTrue();
        h.Verificar(a, "bodega123!").Should().BeFalse();
    }

    [Fact(DisplayName = "RF-02/RF-03: crear un usuario lo registra en la bitácora y no guarda la contraseña")]
    public async Task Crear_usuario_queda_en_bitacora()
    {
        using var e = new Escenario();
        var s = new UsuarioService(e.Db, new HasherContrasenas(), e.Como(e.Admin).Usuario);
        var u = await s.CrearAsync(new("Pedro Lijador", "PLijador", "Segura123!", e.Admin.RolId), default);
        u.Usuario.Should().Be("plijador");
        var b = await e.Db.Bitacoras.SingleAsync();
        (b.Modulo, b.UsuarioId).Should().Be(("Usuarios", e.Admin.Id));
        b.Accion.Should().Contain("Crear usuario plijador");
        (await e.Db.Usuarios.SingleAsync(x => x.NombreUsuario == "plijador")).ContrasenaHash.Should().NotBe("Segura123!");
    }

    [Fact(DisplayName = "RF-02: el administrador no puede desactivar su propia cuenta")]
    public async Task No_desactivarse_a_si_mismo()
    {
        using var e = new Escenario();
        var s = new UsuarioService(e.Db, new HasherContrasenas(), e.Como(e.Admin).Usuario);
        var accion = () => s.ActualizarAsync(e.Admin.Id, new("Ana Admin", e.Admin.RolId, false, null), default);
        await accion.Should().ThrowAsync<DomainException>().WithMessage("*propia cuenta*");
    }
}
