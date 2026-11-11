using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Movimientos;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Application.Features.Bajas;

/// <summary>Causas de merma que pide el RF-26.</summary>
public enum CausaMerma { Desperdicio, Danio, Vencimiento }

public record RegistrarMermaRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Elige un artículo.")] int ArticuloId,
    [Range(0.01, 99999999, ErrorMessage = "La cantidad debe ser mayor que cero.")] decimal Cantidad,
    CausaMerma Causa,
    [MaxLength(110, ErrorMessage = "El detalle admite hasta 110 caracteres.")] string? Detalle);

public record DarDeBajaRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Elige la herramienta.")] int ArticuloId,
    [Range(1, 99999999, ErrorMessage = "Indica cuántas unidades se dan de baja.")] decimal Cantidad,
    [Required(ErrorMessage = "El motivo de la baja es obligatorio."), MaxLength(130)] string Motivo);

public record BajasFiltro(TipoMovimiento? Tipo = null, DateOnly? Desde = null, DateOnly? Hasta = null, int Pagina = 1, int Tamano = 25);

/// <summary>
/// RF-26 (mermas de materiales) y RF-27 (baja de herramientas). Las dos restan existencia con un
/// movimiento propio (Merma o Baja), así quedan en el historial y en los reportes de consumo aparte
/// de las salidas normales.
/// </summary>
public class BajaService(IAppDbContext db, MovimientoService movimientos, IUsuarioActual actual)
{
    public static string TextoCausa(CausaMerma c) => c switch
    {
        CausaMerma.Desperdicio => "Desperdicio",
        CausaMerma.Danio => "Daño",
        _ => "Vencimiento",
    };

    /// <summary>RF-26. Vale para cualquier material, también los consumibles como lijas o brocas.</summary>
    public Task<MovimientoDto> RegistrarMermaAsync(RegistrarMermaRequest req, CancellationToken ct)
    {
        var motivo = $"Merma por {TextoCausa(req.Causa).ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(req.Detalle)) motivo += $": {req.Detalle.Trim()}";

        return movimientos.AplicarAsync(req.ArticuloId, TipoMovimiento.Merma, req.Cantidad, motivo, null, ct, validar: a =>
        {
            if (req.Causa == CausaMerma.Vencimiento && a.FechaVencimiento is { } v && v > DateOnly.FromDateTime(DateTime.Today))
                throw new DomainException($"\"{a.Nombre}\" vence el {v:dd/MM/yyyy}; todavía no está vencido.");
        });
    }

    /// <summary>
    /// RF-27. Resta las unidades dadas de baja; si ya no queda ninguna, la herramienta pasa a estado
    /// "De baja" (queda en el historial de estados, RF-11) y se desactiva para que no aparezca en préstamos.
    /// </summary>
    public Task<MovimientoDto> DarDeBajaAsync(DarDeBajaRequest req, CancellationToken ct)
    {
        var usuario = actual.Id ?? throw new DomainException("No hay un usuario en sesión.");
        return movimientos.AplicarAsync(req.ArticuloId, TipoMovimiento.Baja, req.Cantidad, $"Baja: {req.Motivo.Trim()}", (a, m) =>
        {
            if (m.StockNuevo > 0 || a.Estado == EstadoArticulo.DeBaja) return;
            db.HistorialEstados.Add(new HistorialEstado
            {
                ArticuloId = a.Id, EstadoAnterior = a.Estado, EstadoNuevo = EstadoArticulo.DeBaja, UsuarioId = usuario, FechaHora = DateTime.Now,
            });
            a.Estado = EstadoArticulo.DeBaja;
            a.Activo = false;
        }, ct, validar: a =>
        {
            if (!EsHerramienta(a))
                throw new DomainException($"\"{a.Nombre}\" no es una herramienta: para materiales registre una merma.");
            if (req.Cantidad != Math.Floor(req.Cantidad))
                throw new DomainException("Las herramientas se dan de baja por unidades completas.");
        });
    }

    /// <summary>Historial de bajas y mermas (el más reciente primero).</summary>
    public Task<Paginado<MovimientoDto>> ListarAsync(BajasFiltro f, CancellationToken ct)
    {
        if (f.Tipo is { } t && t is not (TipoMovimiento.Baja or TipoMovimiento.Merma))
            throw new DomainException("Solo se pueden consultar bajas o mermas.");
        var q = db.Movimientos.Where(m => m.Tipo == TipoMovimiento.Baja || m.Tipo == TipoMovimiento.Merma);
        if (f.Tipo is { } tipo) q = q.Where(m => m.Tipo == tipo);
        if (f.Desde is { } d) q = q.Where(m => m.FechaHora >= d.ToDateTime(TimeOnly.MinValue));
        if (f.Hasta is { } h) q = q.Where(m => m.FechaHora < h.AddDays(1).ToDateTime(TimeOnly.MinValue));
        return q.OrderByDescending(m => m.FechaHora).ThenByDescending(m => m.Id)
            .Select(m => new MovimientoDto(
                m.Id, m.FechaHora, m.Tipo, m.ArticuloId, m.Articulo.Codigo, m.Articulo.Nombre, m.Articulo.UnidadMedida,
                m.Cantidad, m.StockAnterior, m.StockNuevo, m.Motivo, m.Usuario.Nombre))
            .PaginarAsync(f.Pagina, f.Tamano, ct);
    }

    private static bool EsHerramienta(Articulo a) =>
        a.Categoria.TipoInventario is TipoInventario.Herramienta or TipoInventario.Instalacion;
}
