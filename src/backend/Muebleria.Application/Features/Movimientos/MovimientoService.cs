using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Application.Features.Movimientos;

public record MovimientoDto(
    int Id, DateTime FechaHora, TipoMovimiento Tipo, int ArticuloId, string Codigo, string Articulo, string UnidadMedida,
    decimal Cantidad, decimal StockAnterior, decimal StockNuevo, string Motivo, string Usuario);

public record RegistrarMovimientoRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Elige un artículo.")] int ArticuloId,
    TipoMovimiento Tipo,
    [Range(0, 99999999, ErrorMessage = "La cantidad no puede ser negativa.")] decimal Cantidad,
    [Required(ErrorMessage = "El motivo es obligatorio."), MaxLength(150)] string Motivo);

public record MovimientoFiltro(
    int? ArticuloId = null, TipoMovimiento? Tipo = null, DateOnly? Desde = null, DateOnly? Hasta = null,
    int Pagina = 1, int Tamano = 25);

/// <summary>
/// RF-19 a RF-22. Todo cambio de existencia pasa por aquí: el movimiento y el nuevo stock
/// se guardan en la misma transacción (RNF-06) y el rowversion del artículo evita que dos
/// salidas simultáneas dejen el stock negativo.
/// </summary>
public class MovimientoService(IAppDbContext db, IUsuarioActual actual)
{
    public static readonly TipoMovimiento[] TiposAvance1 = [TipoMovimiento.Entrada, TipoMovimiento.Salida, TipoMovimiento.Ajuste];

    public async Task<MovimientoDto> RegistrarAsync(RegistrarMovimientoRequest req, CancellationToken ct)
    {
        if (!TiposAvance1.Contains(req.Tipo))
            throw new DomainException($"Los movimientos de tipo {req.Tipo} se registran desde su propio módulo.");

        var articulo = await db.Articulos.FindAsync([req.ArticuloId], ct)
            ?? throw new NoEncontradoException("Artículo", req.ArticuloId);

        var (anterior, nuevo) = articulo.AplicarMovimiento(req.Tipo, req.Cantidad);
        var cantidad = req.Tipo == TipoMovimiento.Ajuste ? Math.Abs(nuevo - anterior) : req.Cantidad;
        if (cantidad == 0)
            throw new DomainException($"La existencia ya es {anterior:0.##}; el ajuste no cambia nada.");

        var movimiento = new Movimiento
        {
            ArticuloId = articulo.Id,
            UsuarioId = actual.Id ?? throw new DomainException("No hay un usuario en sesión."),
            Tipo = req.Tipo,
            Cantidad = cantidad,
            StockAnterior = anterior,
            StockNuevo = nuevo,
            Motivo = req.Motivo.Trim(),
            FechaHora = DateTime.Now,
        };
        db.Movimientos.Add(movimiento);
        await db.SaveChangesAsync(ct);

        return await Proyectar(db.Movimientos.Where(m => m.Id == movimiento.Id)).FirstAsync(ct);
    }

    /// <summary>RF-22: historial cronológico (el más reciente primero).</summary>
    public Task<Paginado<MovimientoDto>> ListarAsync(MovimientoFiltro f, CancellationToken ct)
    {
        var q = db.Movimientos.AsQueryable();
        if (f.ArticuloId is { } a) q = q.Where(m => m.ArticuloId == a);
        if (f.Tipo is { } t) q = q.Where(m => m.Tipo == t);
        if (f.Desde is { } d) q = q.Where(m => m.FechaHora >= d.ToDateTime(TimeOnly.MinValue));
        if (f.Hasta is { } h) q = q.Where(m => m.FechaHora < h.AddDays(1).ToDateTime(TimeOnly.MinValue));
        return Proyectar(q.OrderByDescending(m => m.FechaHora).ThenByDescending(m => m.Id)).PaginarAsync(f.Pagina, f.Tamano, ct);
    }

    private static IQueryable<MovimientoDto> Proyectar(IQueryable<Movimiento> q) =>
        q.Select(m => new MovimientoDto(
            m.Id, m.FechaHora, m.Tipo, m.ArticuloId, m.Articulo.Codigo, m.Articulo.Nombre, m.Articulo.UnidadMedida,
            m.Cantidad, m.StockAnterior, m.StockNuevo, m.Motivo, m.Usuario.Nombre));
}
