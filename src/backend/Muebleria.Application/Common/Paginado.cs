using Microsoft.EntityFrameworkCore;

namespace Muebleria.Application.Common;

public record Paginado<T>(IReadOnlyList<T> Items, int Total, int Pagina, int Tamano);

public static class PaginadoExtensions
{
    public static async Task<Paginado<T>> PaginarAsync<T>(this IQueryable<T> query, int pagina, int tamano, CancellationToken ct)
    {
        pagina = Math.Max(1, pagina);
        tamano = Math.Clamp(tamano, 1, 200);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(ct);
        return new Paginado<T>(items, total, pagina, tamano);
    }
}
