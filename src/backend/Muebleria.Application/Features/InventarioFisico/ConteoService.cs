using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Movimientos;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Application.Features.InventarioFisico;

public record ConteoResumenDto(
    int Id, DateOnly Fecha, string? Ubicacion, string? Categoria, string Responsable, EstadoConteo Estado,
    int TotalArticulos, int Contados, int ConDiferencia);

public record ConteoDetalleDto(
    int Id, int ArticuloId, string Codigo, string Articulo, string UnidadMedida, string Ubicacion,
    decimal StockSistema, decimal? CantidadContada, decimal? Diferencia);

public record ConteoDto(ConteoResumenDto Conteo, IReadOnlyList<ConteoDetalleDto> Detalles);

public record CrearConteoRequest(int? UbicacionId, int? CategoriaId);

public record CantidadContadaItem(int DetalleId, [Range(0, 99999999, ErrorMessage = "La cantidad contada no puede ser negativa.")] decimal? Cantidad);

public record RegistrarConteoRequest([Required] IReadOnlyList<CantidadContadaItem> Cantidades);

/// <summary>
/// RF-28 y RF-29: inventario físico.
/// 1. Se abre un conteo (por ubicación, por categoría o de todo) y se toma una "foto" de la existencia del sistema.
/// 2. Quien cuenta anota lo que encontró; puede guardar a medias y seguir después.
/// 3. El administrador revisa las diferencias y las aplica: cada diferencia se vuelve un ajuste.
/// </summary>
public class ConteoService(IAppDbContext db, IUsuarioActual actual)
{
    private int UsuarioId => actual.Id ?? throw new DomainException("No hay un usuario en sesión.");

    public async Task<List<ConteoResumenDto>> ListarAsync(CancellationToken ct)
    {
        var conteos = await db.ConteosFisicos.OrderBy(c => c.Estado).ThenByDescending(c => c.Id).Take(100)
            .Select(c => new
            {
                c.Id, c.Fecha, Ubicacion = c.Ubicacion != null ? c.Ubicacion.Nombre : null,
                Categoria = c.Categoria != null ? c.Categoria.Nombre : null, Responsable = c.Responsable.Nombre, c.Estado,
                Total = c.Detalles.Count(),
                Contados = c.Detalles.Count(d => d.CantidadContada != null),
                ConDiferencia = c.Detalles.Count(d => d.CantidadContada != null && d.CantidadContada != d.StockSistema),
            }).ToListAsync(ct);
        return conteos.Select(c => new ConteoResumenDto(c.Id, c.Fecha, c.Ubicacion, c.Categoria, c.Responsable, c.Estado,
            c.Total, c.Contados, c.ConDiferencia)).ToList();
    }

    public async Task<ConteoDto> ObtenerAsync(int id, CancellationToken ct)
    {
        var c = await db.ConteosFisicos.Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.Fecha, Ubicacion = x.Ubicacion != null ? x.Ubicacion.Nombre : null,
                Categoria = x.Categoria != null ? x.Categoria.Nombre : null, Responsable = x.Responsable.Nombre, x.Estado,
            }).FirstOrDefaultAsync(ct) ?? throw new NoEncontradoException("Conteo", id);

        var detalles = (await db.ConteoDetalles.Where(d => d.ConteoFisicoId == id)
            .OrderBy(d => d.Articulo.Ubicacion.Nombre).ThenBy(d => d.Articulo.Nombre)
            .Select(d => new
            {
                d.Id, d.ArticuloId, d.Articulo.Codigo, d.Articulo.Nombre, d.Articulo.UnidadMedida, Ubicacion = d.Articulo.Ubicacion.Nombre,
                d.StockSistema, d.CantidadContada,
            }).ToListAsync(ct))
            // La diferencia se calcula aquí (también existe como columna calculada en SQL Server).
            .Select(d => new ConteoDetalleDto(d.Id, d.ArticuloId, d.Codigo, d.Nombre, d.UnidadMedida, d.Ubicacion,
                d.StockSistema, d.CantidadContada, d.CantidadContada - d.StockSistema))
            .ToList();

        var resumen = new ConteoResumenDto(c.Id, c.Fecha, c.Ubicacion, c.Categoria, c.Responsable, c.Estado, detalles.Count,
            detalles.Count(d => d.CantidadContada is not null), detalles.Count(d => d.Diferencia is { } x && x != 0));
        return new ConteoDto(resumen, detalles);
    }

    /// <summary>RF-28: abre el conteo con la existencia del sistema de cada artículo activo.</summary>
    public async Task<ConteoDto> CrearAsync(CrearConteoRequest req, CancellationToken ct)
    {
        if (req.UbicacionId is { } u && !await db.Ubicaciones.AnyAsync(x => x.Id == u, ct))
            throw new DomainException("La ubicación elegida no existe.");
        if (req.CategoriaId is { } c && !await db.Categorias.AnyAsync(x => x.Id == c, ct))
            throw new DomainException("La categoría elegida no existe.");

        // Dos conteos abiertos sobre los mismos artículos se pisarían al aplicar.
        var abiertos = await db.ConteosFisicos.Where(x => x.Estado == EstadoConteo.Abierto).ToListAsync(ct);
        if (abiertos.Any(x => SeCruzan(x.UbicacionId, x.CategoriaId, req.UbicacionId, req.CategoriaId)))
            throw new DomainException("Ya hay un conteo abierto que incluye esos artículos. Aplíquelo o elimínelo antes de abrir otro.");

        var q = db.Articulos.Where(a => a.Activo);
        if (req.UbicacionId is { } ubic) q = q.Where(a => a.UbicacionId == ubic);
        if (req.CategoriaId is { } cat) q = q.Where(a => a.CategoriaId == cat);
        var articulos = await q.Select(a => new { a.Id, a.StockActual }).ToListAsync(ct);
        if (articulos.Count == 0)
            throw new DomainException("No hay artículos activos con esos filtros.");

        var conteo = new ConteoFisico
        {
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            UbicacionId = req.UbicacionId,
            CategoriaId = req.CategoriaId,
            ResponsableId = UsuarioId,
            Detalles = articulos.Select(a => new ConteoDetalle { ArticuloId = a.Id, StockSistema = a.StockActual }).ToList(),
        };
        db.ConteosFisicos.Add(conteo);
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(conteo.Id, ct);
    }

    /// <summary>RF-28: guarda lo contado. Una cantidad vacía deja el artículo como "sin contar".</summary>
    public async Task<ConteoDto> RegistrarAsync(int id, RegistrarConteoRequest req, CancellationToken ct)
    {
        var conteo = await AbiertoAsync(id, ct);
        foreach (var item in req.Cantidades)
        {
            var detalle = conteo.Detalles.FirstOrDefault(d => d.Id == item.DetalleId)
                ?? throw new DomainException($"El renglón #{item.DetalleId} no pertenece a este conteo.");
            if (item.Cantidad < 0) throw new DomainException("La cantidad contada no puede ser negativa.");
            detalle.CantidadContada = item.Cantidad;
        }
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    /// <summary>
    /// RF-29: cada diferencia se registra como un ajuste. Se suma la diferencia a la existencia actual
    /// (no se copia lo contado), para no borrar las entradas o salidas hechas mientras se contaba.
    /// </summary>
    public async Task<ConteoDto> AplicarAsync(int id, CancellationToken ct)
    {
        for (var intento = 1; ; intento++)
        {
            var conteo = await AbiertoAsync(id, ct);
            var faltan = conteo.Detalles.Count(d => d.CantidadContada is null);
            if (faltan > 0)
                throw new DomainException(faltan == 1 ? "Falta contar 1 artículo." : $"Faltan contar {faltan} artículos.");

            var conDiferencia = conteo.Detalles.Where(d => d.CantidadContada != d.StockSistema).ToList();
            var ids = conDiferencia.Select(d => d.ArticuloId).ToList();
            var articulos = await db.Articulos.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
            foreach (var d in conDiferencia)
            {
                var a = articulos[d.ArticuloId];
                var diferencia = d.CantidadContada!.Value - d.StockSistema;
                var nuevo = a.StockActual + diferencia;
                if (nuevo < 0)
                    throw new DomainException($"\"{a.Nombre}\" quedaría en negativo: revise el conteo de ese artículo.");
                var signo = diferencia > 0 ? "+" : "";
                db.Movimientos.Add(MovimientoService.Crear(a, TipoMovimiento.Ajuste, nuevo,
                    $"Conteo físico #{id}: diferencia {signo}{diferencia.ToString("0.##", CultureInfo.InvariantCulture)}", UsuarioId));
            }
            conteo.Estado = EstadoConteo.Aplicado;
            try
            {
                await db.SaveChangesAsync(ct);
                return await ObtenerAsync(id, ct);
            }
            catch (DbUpdateConcurrencyException) when (intento < 5)
            {
                // Alguien movió uno de los artículos al mismo tiempo: se vuelve a leer y se reintenta.
                db.OlvidarCambios();
            }
        }
    }

    /// <summary>Elimina un conteo que todavía no se aplicó (por ejemplo, si se abrió por error).</summary>
    public async Task EliminarAsync(int id, CancellationToken ct)
    {
        var conteo = await AbiertoAsync(id, ct);
        db.ConteosFisicos.Remove(conteo);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ConteoFisico> AbiertoAsync(int id, CancellationToken ct)
    {
        var conteo = await db.ConteosFisicos.Include(c => c.Detalles).FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NoEncontradoException("Conteo", id);
        if (conteo.Estado != EstadoConteo.Abierto) throw new DomainException("Este conteo ya fue aplicado.");
        return conteo;
    }

    /// <summary>Dos filtros se cruzan si algún artículo podría quedar en los dos (nulo = todos).</summary>
    internal static bool SeCruzan(int? u1, int? c1, int? u2, int? c2) =>
        (u1 is null || u2 is null || u1 == u2) && (c1 is null || c2 is null || c1 == c2);
}
