using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Movimientos;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Application.Features.Solicitudes;

public record SolicitudDto(
    int Id, int ArticuloId, string Codigo, string Articulo, string UnidadMedida, decimal StockActual,
    decimal Cantidad, string Motivo, EstadoSolicitud Estado,
    int SolicitanteId, string Solicitante, string? Aprobador, string? MotivoRechazo,
    DateTime FechaSolicitud, DateTime? FechaResolucion, DateTime? FechaEntrega);

public record CrearSolicitudRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Elige un artículo.")] int ArticuloId,
    [Range(0.01, 99999999, ErrorMessage = "La cantidad debe ser mayor que cero.")] decimal Cantidad,
    [Required(ErrorMessage = "Explica para qué necesitas el material."), MaxLength(150)] string Motivo);

public record RechazarSolicitudRequest(
    [Required(ErrorMessage = "Indica el motivo del rechazo."), MaxLength(150)] string Motivo);

public record SolicitudFiltro(EstadoSolicitud? Estado = null, int Pagina = 1, int Tamano = 25);

/// <summary>
/// RF-23 a RF-25. El trabajador pide material; el encargado de bodega aprueba o rechaza;
/// al entregar lo aprobado se genera la salida y se descuenta la existencia.
/// </summary>
public class SolicitudService(IAppDbContext db, IUsuarioActual actual)
{
    private int UsuarioId => actual.Id ?? throw new DomainException("No hay un usuario en sesión.");

    /// <summary>Quien puede aprobar ve todas; el trabajador ve solo las suyas (RF-24).</summary>
    public Task<Paginado<SolicitudDto>> ListarAsync(SolicitudFiltro f, bool verTodas, CancellationToken ct)
    {
        var q = db.Solicitudes.AsQueryable();
        if (!verTodas) q = q.Where(s => s.SolicitanteId == UsuarioId);
        if (f.Estado is { } e) q = q.Where(s => s.Estado == e);
        // Primero lo pendiente, luego lo más reciente.
        return Proyectar(q.OrderBy(s => s.Estado == EstadoSolicitud.Pendiente ? 0 : s.Estado == EstadoSolicitud.Aprobada ? 1 : 2)
                .ThenByDescending(s => s.FechaSolicitud))
            .PaginarAsync(f.Pagina, f.Tamano, ct);
    }

    public Task<int> PendientesAsync(CancellationToken ct) =>
        db.Solicitudes.CountAsync(s => s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.Aprobada, ct);

    /// <summary>RF-23: la solicitud queda pendiente hasta que el encargado la revise.</summary>
    public async Task<SolicitudDto> CrearAsync(CrearSolicitudRequest req, CancellationToken ct)
    {
        var articulo = await db.Articulos.FindAsync([req.ArticuloId], ct) ?? throw new NoEncontradoException("Artículo", req.ArticuloId);
        if (!articulo.Activo) throw new DomainException($"El artículo \"{articulo.Nombre}\" está desactivado.");

        var solicitud = new Solicitud
        {
            ArticuloId = articulo.Id,
            SolicitanteId = UsuarioId,
            Cantidad = req.Cantidad,
            Motivo = req.Motivo.Trim(),
            Estado = EstadoSolicitud.Pendiente,
            FechaSolicitud = DateTime.Now,
        };
        db.Solicitudes.Add(solicitud);
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(solicitud.Id, ct);
    }

    /// <summary>RF-24: aprobación.</summary>
    public async Task<SolicitudDto> AprobarAsync(int id, CancellationToken ct)
    {
        var s = await PendienteAsync(id, ct);
        s.Estado = EstadoSolicitud.Aprobada;
        s.AprobadorId = UsuarioId;
        s.FechaResolucion = DateTime.Now;
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    /// <summary>RF-24: rechazo con motivo obligatorio.</summary>
    public async Task<SolicitudDto> RechazarAsync(int id, RechazarSolicitudRequest req, CancellationToken ct)
    {
        var s = await PendienteAsync(id, ct);
        s.Estado = EstadoSolicitud.Rechazada;
        s.AprobadorId = UsuarioId;
        s.MotivoRechazo = req.Motivo.Trim();
        s.FechaResolucion = DateTime.Now;
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    /// <summary>RF-25: al entregar se registra la salida y se descuenta la existencia en la misma transacción.</summary>
    public async Task<SolicitudDto> EntregarAsync(int id, CancellationToken ct)
    {
        var s = await db.Solicitudes.Include(x => x.Articulo).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NoEncontradoException("Solicitud", id);
        if (s.Estado != EstadoSolicitud.Aprobada)
            throw new DomainException(s.Estado == EstadoSolicitud.Pendiente
                ? "La solicitud todavía no está aprobada."
                : $"La solicitud ya está {s.Estado.ToString().ToLowerInvariant()}.");

        db.Movimientos.Add(MovimientoService.Crear(s.Articulo, TipoMovimiento.Salida, s.Cantidad, $"Entrega de la solicitud #{s.Id}", UsuarioId));
        s.Estado = EstadoSolicitud.Entregada;
        s.FechaEntrega = DateTime.Now;
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    private async Task<Solicitud> PendienteAsync(int id, CancellationToken ct)
    {
        var s = await db.Solicitudes.FindAsync([id], ct) ?? throw new NoEncontradoException("Solicitud", id);
        if (s.Estado != EstadoSolicitud.Pendiente)
            throw new DomainException($"La solicitud ya fue {s.Estado.ToString().ToLowerInvariant()}.");
        return s;
    }

    private Task<SolicitudDto> ObtenerAsync(int id, CancellationToken ct) =>
        Proyectar(db.Solicitudes.Where(s => s.Id == id)).FirstAsync(ct);

    private static IQueryable<SolicitudDto> Proyectar(IQueryable<Solicitud> q) =>
        q.Select(s => new SolicitudDto(
            s.Id, s.ArticuloId, s.Articulo.Codigo, s.Articulo.Nombre, s.Articulo.UnidadMedida, s.Articulo.StockActual,
            s.Cantidad, s.Motivo, s.Estado, s.SolicitanteId, s.Solicitante.Nombre,
            s.Aprobador != null ? s.Aprobador.Nombre : null, s.MotivoRechazo,
            s.FechaSolicitud, s.FechaResolucion, s.FechaEntrega));
}
