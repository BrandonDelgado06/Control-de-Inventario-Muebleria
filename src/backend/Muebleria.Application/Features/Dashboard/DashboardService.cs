using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Articulos;
using Muebleria.Application.Features.Movimientos;

namespace Muebleria.Application.Features.Dashboard;

public record ResumenDto(
    int TotalArticulos, int BajoMinimo, int PorVencer, int MovimientosHoy,
    IReadOnlyList<ArticuloResumenDto> ArticulosBajoMinimo, IReadOnlyList<MovimientoDto> UltimosMovimientos);

/// <summary>Versión inicial del panel principal (RF-36/37 se completan en el sprint 4).</summary>
public class DashboardService(IAppDbContext db, MovimientoService movimientos)
{
    public async Task<ResumenDto> ResumenAsync(CancellationToken ct)
    {
        var activos = db.Articulos.Where(a => a.Activo);
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var dias = int.TryParse(await db.Configuraciones.Where(c => c.Clave == "alertas.dias_vencimiento").Select(c => c.Valor).FirstOrDefaultAsync(ct), out var n) ? n : 30;

        var total = await activos.CountAsync(ct);
        var bajo = await activos.CountAsync(a => a.StockActual <= a.StockMinimo, ct);
        var porVencer = await activos.CountAsync(a => a.FechaVencimiento != null && a.FechaVencimiento <= hoy.AddDays(dias), ct);
        var movHoy = await db.Movimientos.CountAsync(m => m.FechaHora >= DateTime.Today, ct);
        var listaBajo = await activos.Where(a => a.StockActual <= a.StockMinimo)
            .OrderBy(a => a.StockActual - a.StockMinimo).Take(8).Select(ArticuloService.AResumen).ToListAsync(ct);
        var ultimos = await movimientos.ListarAsync(new MovimientoFiltro(Tamano: 8), ct);

        return new ResumenDto(total, bajo, porVencer, movHoy, listaBajo, ultimos.Items);
    }
}
