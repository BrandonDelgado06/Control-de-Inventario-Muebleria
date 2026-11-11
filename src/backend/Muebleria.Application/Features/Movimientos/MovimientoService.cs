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

        return await AplicarAsync(req.ArticuloId, req.Tipo, req.Cantidad, req.Motivo, null, ct);
    }

    /// <summary>
    /// Registra un movimiento con reintento por concurrencia. <paramref name="validar"/> corre antes de
    /// aplicar (para reglas propias de cada módulo) y <paramref name="despues"/> después de aplicar,
    /// dentro del mismo guardado (por ejemplo, para cambiar el estado del artículo).
    /// </summary>
    public async Task<MovimientoDto> AplicarAsync(
        int articuloId, TipoMovimiento tipo, decimal cantidad, string motivo,
        Action<Articulo, Movimiento>? despues, CancellationToken ct, Action<Articulo>? validar = null)
    {
        var usuario = actual.Id ?? throw new DomainException("No hay un usuario en sesión.");

        // Si otra persona movió el mismo artículo al mismo tiempo (rowversion), se vuelve a leer la
        // existencia actualizada y se reintenta. Así dos entradas simultáneas se guardan las dos, y una
        // salida que ya no alcanza se rechaza con el mensaje de existencia insuficiente.
        for (var intento = 1; ; intento++)
        {
            var articulo = await db.Articulos.Include(a => a.Categoria).FirstOrDefaultAsync(a => a.Id == articuloId, ct)
                ?? throw new NoEncontradoException("Artículo", articuloId);
            validar?.Invoke(articulo);
            var movimiento = Crear(articulo, tipo, cantidad, motivo, usuario);
            db.Movimientos.Add(movimiento);
            despues?.Invoke(articulo, movimiento);
            try
            {
                await db.SaveChangesAsync(ct);
                return await Proyectar(db.Movimientos.Where(m => m.Id == movimiento.Id)).FirstAsync(ct);
            }
            catch (DbUpdateConcurrencyException) when (intento < IntentosConcurrencia)
            {
                db.OlvidarCambios();
                // Espera corta y aleatoria para que los que chocaron no vuelvan a chocar al mismo instante.
                await Task.Delay(Random.Shared.Next(5, 25) * intento, ct);
            }
        }
    }

    private const int IntentosConcurrencia = 10;

    /// <summary>
    /// Aplica el cambio de existencia al artículo y arma el movimiento que lo respalda.
    /// Lo usan también las solicitudes (RF-25) y las herramientas de instalación (RF-16/17),
    /// para que todo cambio de stock siga la misma regla (RNF-06).
    /// </summary>
    public static Movimiento Crear(Articulo articulo, TipoMovimiento tipo, decimal cantidad, string motivo, int usuarioId)
    {
        var (anterior, nuevo) = articulo.AplicarMovimiento(tipo, cantidad);
        var movida = tipo == TipoMovimiento.Ajuste ? Math.Abs(nuevo - anterior) : cantidad;
        if (movida == 0)
            throw new DomainException($"La existencia ya es {anterior:0.##}; el ajuste no cambia nada.");

        return new Movimiento
        {
            ArticuloId = articulo.Id,
            UsuarioId = usuarioId,
            Tipo = tipo,
            Cantidad = movida,
            StockAnterior = anterior,
            StockNuevo = nuevo,
            Motivo = motivo.Trim(),
            FechaHora = DateTime.Now,
        };
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
