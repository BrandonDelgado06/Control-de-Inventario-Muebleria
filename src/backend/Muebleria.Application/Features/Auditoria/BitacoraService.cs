using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common;
using Muebleria.Application.Common.Interfaces;

namespace Muebleria.Application.Features.Auditoria;

public record BitacoraDto(int Id, DateTime FechaHora, string Usuario, string Modulo, string Accion);

public record BitacoraFiltro(int? UsuarioId = null, string? Modulo = null, string? Accion = null, DateOnly? Desde = null, DateOnly? Hasta = null, int Pagina = 1, int Tamano = 50);

/// <summary>RF-04: consulta de la bitácora con filtros. Solo lectura.</summary>
public class BitacoraService(IAppDbContext db)
{
    public Task<Paginado<BitacoraDto>> ListarAsync(BitacoraFiltro f, CancellationToken ct)
    {
        var q = db.Bitacoras.AsNoTracking();
        if (f.UsuarioId is { } u) q = q.Where(b => b.UsuarioId == u);
        if (!string.IsNullOrWhiteSpace(f.Modulo)) q = q.Where(b => b.Modulo == f.Modulo);
        if (!string.IsNullOrWhiteSpace(f.Accion)) q = q.Where(b => b.Accion.Contains(f.Accion));
        if (f.Desde is { } d) q = q.Where(b => b.FechaHora >= d.ToDateTime(TimeOnly.MinValue));
        if (f.Hasta is { } h) q = q.Where(b => b.FechaHora < h.AddDays(1).ToDateTime(TimeOnly.MinValue));

        return q.OrderByDescending(b => b.Id)
            .Select(b => new BitacoraDto(b.Id, b.FechaHora, b.Usuario.Nombre, b.Modulo, b.Accion))
            .PaginarAsync(f.Pagina, f.Tamano, ct);
    }

    public Task<List<string>> ModulosAsync(CancellationToken ct) =>
        db.Bitacoras.Select(b => b.Modulo).Distinct().OrderBy(m => m).ToListAsync(ct);
}
