using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Articulos;
using Muebleria.Application.Features.Instalacion;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;

namespace Muebleria.Application.Features.Alertas;

public record AlertaVencimientoDto(
    int ArticuloId, string Codigo, string Producto, string? Lote, DateOnly FechaVencimiento, int DiasRestantes,
    decimal StockActual, string UnidadMedida);

public record AlertaRespaldoDto(DateTime? Ultimo, int? DiasDesdeUltimo, string Mensaje);

public record AlertasDto(
    IReadOnlyList<ArticuloResumenDto> StockMinimo,
    IReadOnlyList<AlertaVencimientoDto> Vencimientos,
    IReadOnlyList<SalidaHerramientaDto> HerramientasSinDevolver,
    AlertaRespaldoDto? Respaldo,
    int DiasAnticipacion,
    int Total);

public record ResumenAlertasDto(int StockMinimo, int Vencimientos, int HerramientasSinDevolver, int Respaldo, int Total);

public record ConfigAlertasRequest([Range(1, 365, ErrorMessage = "Los días de anticipación deben estar entre 1 y 365.")] int DiasAnticipacion);

/// <summary>
/// RF-30 a RF-32. Las alertas se calculan en el momento con los datos actuales; no se guardan,
/// así nunca quedan desactualizadas. También avisa si no hay un respaldo de la última semana (RNF-07).
/// </summary>
public class AlertaService(IAppDbContext db, IRespaldos respaldos, IUsuarioActual actual)
{
    public const string ClaveDias = "alertas.dias_vencimiento";
    public const int DiasRespaldo = 7;
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    public async Task<int> DiasAnticipacionAsync(CancellationToken ct) =>
        int.TryParse(await db.Configuraciones.Where(c => c.Clave == ClaveDias).Select(c => c.Valor).FirstOrDefaultAsync(ct), out var d) ? d : 30;

    public async Task<AlertasDto> ListarAsync(bool incluirRespaldo, CancellationToken ct)
    {
        var dias = await DiasAnticipacionAsync(ct);
        var limite = Hoy.AddDays(dias);

        // RF-30: existencia igual o menor al mínimo.
        var stock = await db.Articulos.Where(a => a.Activo && a.StockActual <= a.StockMinimo)
            .OrderBy(a => a.StockActual - a.StockMinimo).Select(ArticuloService.AResumen).ToListAsync(ct);

        // RF-31: productos de acabado próximos a vencer o vencidos (con existencia).
        var venc = (await db.Articulos
                .Where(a => a.Activo && a.FechaVencimiento != null && a.FechaVencimiento <= limite && a.StockActual > 0)
                .OrderBy(a => a.FechaVencimiento)
                .Select(a => new { a.Id, a.Codigo, a.Nombre, a.Lote, Vence = a.FechaVencimiento!.Value, a.StockActual, a.UnidadMedida })
                .ToListAsync(ct))
            .Select(a => new AlertaVencimientoDto(a.Id, a.Codigo, a.Nombre, a.Lote, a.Vence, a.Vence.DayNumber - Hoy.DayNumber, a.StockActual, a.UnidadMedida))
            .ToList();

        // RF-32: herramientas que siguen fuera después de la fecha prevista.
        var hoy = Hoy;
        var fuera = (await db.SalidasHerramienta.Where(s => s.FechaDevolucion == null && s.FechaPrevista < hoy)
                .OrderBy(s => s.FechaPrevista)
                .Select(s => new { s, Codigo = s.Articulo.Codigo, Nombre = s.Articulo.Nombre, Responsable = s.Responsable.Nombre })
                .ToListAsync(ct))
            .Select(x => InstalacionService.ADto(x.s, x.Codigo, x.Nombre, x.Responsable)).ToList();

        var resp = incluirRespaldo ? await AlertaRespaldoAsync(ct) : null;
        return new AlertasDto(stock, venc, fuera, resp, dias, stock.Count + venc.Count + fuera.Count + (resp is null ? 0 : 1));
    }

    public async Task<ResumenAlertasDto> ResumenAsync(bool incluirRespaldo, CancellationToken ct)
    {
        var a = await ListarAsync(incluirRespaldo, ct);
        return new ResumenAlertasDto(a.StockMinimo.Count, a.Vencimientos.Count, a.HerramientasSinDevolver.Count, a.Respaldo is null ? 0 : 1, a.Total);
    }

    /// <summary>RF-31: el administrador define con cuántos días de anticipación se avisa.</summary>
    public async Task<int> ConfigurarAsync(ConfigAlertasRequest req, CancellationToken ct)
    {
        var c = await db.Configuraciones.FindAsync([ClaveDias], ct);
        if (c is null) db.Configuraciones.Add(c = new Configuracion { Clave = ClaveDias, Descripcion = "Días de anticipación para avisar que un producto de acabado está por vencer (RF-31)." });
        var anterior = c.Valor;
        c.Valor = req.DiasAnticipacion.ToString();
        db.Bitacoras.Add(new Bitacora
        {
            UsuarioId = actual.Id!.Value, Modulo = "Alertas", FechaHora = DateTime.Now,
            Accion = $"Cambiar anticipación de vencimiento de {anterior} a {req.DiasAnticipacion} días",
        });
        await db.SaveChangesAsync(ct);
        return req.DiasAnticipacion;
    }

    private async Task<AlertaRespaldoDto?> AlertaRespaldoAsync(CancellationToken ct)
    {
        var ultimo = (await respaldos.ListarAsync(ct)).Select(r => (DateTime?)r.Fecha).FirstOrDefault();
        if (ultimo is null) return new AlertaRespaldoDto(null, null, "Todavía no se ha generado ninguna copia de seguridad.");
        var diasDesde = (int)(DateTime.Today - ultimo.Value.Date).TotalDays;
        return diasDesde >= DiasRespaldo
            ? new AlertaRespaldoDto(ultimo, diasDesde, $"El último respaldo tiene {diasDesde} días. Se recomienda al menos uno por semana (RNF-07).")
            : null;
    }
}
