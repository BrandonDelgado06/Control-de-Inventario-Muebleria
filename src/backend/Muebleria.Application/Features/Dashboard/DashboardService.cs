using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Articulos;
using Muebleria.Application.Features.Movimientos;
using Muebleria.Domain.Enums;

namespace Muebleria.Application.Features.Dashboard;

public record ConsumoCategoriaDto(string Categoria, int Salidas);

public record ResumenDto(
    int TotalArticulos, int BajoMinimo, int PorVencer, int HerramientasFuera, int HerramientasAtrasadas,
    int SolicitudesPendientes, int MovimientosHoy, int AlertasActivas,
    IReadOnlyList<ArticuloResumenDto> ArticulosBajoMinimo, IReadOnlyList<MovimientoDto> UltimosMovimientos,
    IReadOnlyList<ConsumoCategoriaDto> ConsumoPorCategoria);

/// <summary>RF-36 (indicadores) y RF-37 (últimos movimientos y gráfico de consumo).</summary>
public class DashboardService(IAppDbContext db, MovimientoService movimientos)
{
    public async Task<ResumenDto> ResumenAsync(CancellationToken ct)
    {
        var activos = db.Articulos.Where(a => a.Activo);
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var dias = int.TryParse(await db.Configuraciones.Where(c => c.Clave == "alertas.dias_vencimiento").Select(c => c.Valor).FirstOrDefaultAsync(ct), out var n) ? n : 30;
        var limite = hoy.AddDays(dias);

        var total = await activos.CountAsync(ct);
        var bajo = await activos.CountAsync(a => a.StockActual <= a.StockMinimo, ct);
        var porVencer = await activos.CountAsync(a => a.FechaVencimiento != null && a.FechaVencimiento <= limite && a.StockActual > 0, ct);
        var fuera = await db.SalidasHerramienta.CountAsync(s => s.FechaDevolucion == null, ct);
        var atrasadas = await db.SalidasHerramienta.CountAsync(s => s.FechaDevolucion == null && s.FechaPrevista < hoy, ct);
        var pendientes = await db.Solicitudes.CountAsync(s => s.Estado == EstadoSolicitud.Pendiente, ct);
        var movHoy = await db.Movimientos.CountAsync(m => m.FechaHora >= DateTime.Today, ct);

        var listaBajo = await activos.Where(a => a.StockActual <= a.StockMinimo)
            .OrderBy(a => a.StockActual - a.StockMinimo).Take(6).Select(ArticuloService.AResumen).ToListAsync(ct);
        var ultimos = await movimientos.ListarAsync(new MovimientoFiltro(Tamano: 6), ct);

        // RF-37: salidas de los últimos 30 días por categoría (cantidad de salidas, porque cada artículo usa su propia unidad).
        var desde = DateTime.Today.AddDays(-29);
        var consumo = (await db.Movimientos
                // Las herramientas de instalación salen prestadas y vuelven: no son consumo.
                .Where(m => m.FechaHora >= desde && (m.Tipo == TipoMovimiento.Salida || m.Tipo == TipoMovimiento.Merma)
                    && m.Articulo.Categoria.TipoInventario != TipoInventario.Instalacion)
                .GroupBy(m => m.Articulo.Categoria.Nombre)
                .Select(g => new { Categoria = g.Key, Salidas = g.Count() })
                .OrderByDescending(x => x.Salidas)
                .ToListAsync(ct))
            .Select(x => new ConsumoCategoriaDto(x.Categoria, x.Salidas))
            .ToList();

        return new ResumenDto(total, bajo, porVencer, fuera, atrasadas, pendientes, movHoy, bajo + porVencer + atrasadas,
            listaBajo, ultimos.Items, consumo);
    }
}
