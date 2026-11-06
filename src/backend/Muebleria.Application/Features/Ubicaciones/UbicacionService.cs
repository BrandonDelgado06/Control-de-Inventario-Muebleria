using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Articulos;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Application.Features.Ubicaciones;

public record UbicacionDto(int Id, string Nombre, string? Descripcion, bool Activo, int CantidadArticulos);

public record UbicacionRequest(
    [Required(ErrorMessage = "El nombre es obligatorio."), MaxLength(60)] string Nombre,
    [MaxLength(150)] string? Descripcion,
    bool Activo = true);

public class UbicacionService(IAppDbContext db)
{
    public Task<List<UbicacionDto>> ListarAsync(bool incluirInactivas, CancellationToken ct) =>
        db.Ubicaciones
            .Where(u => incluirInactivas || u.Activo)
            .OrderBy(u => u.Nombre)
            .Select(u => new UbicacionDto(u.Id, u.Nombre, u.Descripcion, u.Activo,
                db.Articulos.Count(a => a.UbicacionId == u.Id && a.Activo)))
            .ToListAsync(ct);

    public async Task<UbicacionDto> CrearAsync(UbicacionRequest req, CancellationToken ct)
    {
        await ValidarNombreAsync(req.Nombre, null, ct);
        var ubicacion = new Ubicacion { Nombre = req.Nombre.Trim(), Descripcion = Limpiar(req.Descripcion), Activo = req.Activo };
        db.Ubicaciones.Add(ubicacion);
        await db.SaveChangesAsync(ct);
        return new UbicacionDto(ubicacion.Id, ubicacion.Nombre, ubicacion.Descripcion, ubicacion.Activo, 0);
    }

    public async Task<UbicacionDto> ActualizarAsync(int id, UbicacionRequest req, CancellationToken ct)
    {
        var ubicacion = await db.Ubicaciones.FindAsync([id], ct) ?? throw new NoEncontradoException("Ubicación", id);
        await ValidarNombreAsync(req.Nombre, id, ct);
        var articulos = await db.Articulos.CountAsync(a => a.UbicacionId == id && a.Activo, ct);
        if (!req.Activo && articulos > 0)
            throw new DomainException($"No se puede desactivar: la ubicación tiene {articulos} artículo(s). Muévelos a otra ubicación primero.");

        ubicacion.Nombre = req.Nombre.Trim();
        ubicacion.Descripcion = Limpiar(req.Descripcion);
        ubicacion.Activo = req.Activo;
        await db.SaveChangesAsync(ct);
        return new UbicacionDto(ubicacion.Id, ubicacion.Nombre, ubicacion.Descripcion, ubicacion.Activo, articulos);
    }

    /// <summary>RF-06: artículos almacenados en una ubicación con su existencia.</summary>
    public async Task<List<ArticuloResumenDto>> ArticulosAsync(int id, CancellationToken ct)
    {
        if (!await db.Ubicaciones.AnyAsync(u => u.Id == id, ct)) throw new NoEncontradoException("Ubicación", id);
        return await db.Articulos.Where(a => a.UbicacionId == id && a.Activo)
            .OrderBy(a => a.Nombre)
            .Select(ArticuloService.AResumen)
            .ToListAsync(ct);
    }

    private async Task ValidarNombreAsync(string nombre, int? id, CancellationToken ct)
    {
        var n = nombre.Trim();
        if (await db.Ubicaciones.AnyAsync(u => u.Nombre == n && u.Id != id, ct))
            throw new DomainException($"Ya existe una ubicación llamada \"{n}\".");
    }

    private static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
