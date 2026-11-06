using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Application.Features.Categorias;

public record CampoDto(int Id, string Nombre, TipoDato TipoDato, bool Obligatorio, IReadOnlyList<string> Opciones, int Orden);

public record CategoriaDto(int Id, string Nombre, TipoInventario TipoInventario, bool Activo, int CantidadArticulos, IReadOnlyList<CampoDto> Campos);

public record CampoRequest(
    int? Id,
    [Required(ErrorMessage = "Cada campo necesita un nombre."), MaxLength(60)] string Nombre,
    TipoDato TipoDato,
    bool Obligatorio,
    IReadOnlyList<string>? Opciones);

public record CategoriaRequest(
    [Required(ErrorMessage = "El nombre es obligatorio."), MaxLength(60)] string Nombre,
    TipoInventario TipoInventario,
    bool Activo,
    IReadOnlyList<CampoRequest> Campos);

/// <summary>RF-07: categorías con campos personalizados (medidas, color, grosor, número de grano…).</summary>
public class CategoriaService(IAppDbContext db)
{
    public async Task<List<CategoriaDto>> ListarAsync(bool incluirInactivas, CancellationToken ct)
    {
        var categorias = await db.Categorias.Include(c => c.Campos)
            .Where(c => incluirInactivas || c.Activo)
            .OrderBy(c => c.Nombre)
            .ToListAsync(ct);
        var conteos = await db.Articulos.Where(a => a.Activo)
            .GroupBy(a => a.CategoriaId)
            .Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        return categorias.Select(c => ADto(c, conteos.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<CategoriaDto> ObtenerAsync(int id, CancellationToken ct)
    {
        var c = await db.Categorias.Include(x => x.Campos).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NoEncontradoException("Categoría", id);
        return ADto(c, await db.Articulos.CountAsync(a => a.CategoriaId == id && a.Activo, ct));
    }

    public async Task<CategoriaDto> CrearAsync(CategoriaRequest req, CancellationToken ct)
    {
        await ValidarAsync(req, null, ct);
        var categoria = new Categoria { Nombre = req.Nombre.Trim(), TipoInventario = req.TipoInventario, Activo = req.Activo };
        var orden = 0;
        foreach (var campo in req.Campos)
            categoria.Campos.Add(NuevoCampo(campo, ++orden));
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(categoria.Id, ct);
    }

    public async Task<CategoriaDto> ActualizarAsync(int id, CategoriaRequest req, CancellationToken ct)
    {
        var categoria = await db.Categorias.Include(c => c.Campos).FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NoEncontradoException("Categoría", id);
        await ValidarAsync(req, id, ct);
        if (!req.Activo && await db.Articulos.AnyAsync(a => a.CategoriaId == id && a.Activo, ct))
            throw new DomainException("No se puede desactivar: la categoría tiene artículos activos.");

        categoria.Nombre = req.Nombre.Trim();
        categoria.TipoInventario = req.TipoInventario;
        categoria.Activo = req.Activo;

        // Campos que ya no vienen en la petición: se eliminan solo si ningún artículo los usa.
        var idsEnviados = req.Campos.Where(c => c.Id.HasValue).Select(c => c.Id!.Value).ToHashSet();
        foreach (var campo in categoria.Campos.Where(c => !idsEnviados.Contains(c.Id)).ToList())
        {
            if (await db.ArticuloValores.AnyAsync(v => v.CampoId == campo.Id, ct))
                throw new DomainException($"No se puede quitar el campo \"{campo.Nombre}\" porque hay artículos que lo usan.");
            db.CamposPersonalizados.Remove(campo);
        }

        var orden = 0;
        foreach (var campoReq in req.Campos)
        {
            orden++;
            var existente = campoReq.Id is { } campoId ? categoria.Campos.FirstOrDefault(c => c.Id == campoId) : null;
            if (existente is null)
            {
                categoria.Campos.Add(NuevoCampo(campoReq, orden));
                continue;
            }
            existente.Nombre = campoReq.Nombre.Trim();
            existente.TipoDato = campoReq.TipoDato;
            existente.Obligatorio = campoReq.Obligatorio;
            existente.Opciones = UnirOpciones(campoReq);
            existente.Orden = orden;
        }

        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    private async Task ValidarAsync(CategoriaRequest req, int? id, CancellationToken ct)
    {
        var nombre = req.Nombre.Trim();
        if (await db.Categorias.AnyAsync(c => c.Nombre == nombre && c.Id != id, ct))
            throw new DomainException($"Ya existe una categoría llamada \"{nombre}\".");

        var repetido = req.Campos.GroupBy(c => c.Nombre.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            throw new DomainException($"El campo \"{repetido.Key}\" está repetido.");

        var listaVacia = req.Campos.FirstOrDefault(c => c.TipoDato == TipoDato.Lista && (c.Opciones is null || !c.Opciones.Any(o => !string.IsNullOrWhiteSpace(o))));
        if (listaVacia is not null)
            throw new DomainException($"El campo \"{listaVacia.Nombre}\" es una lista y necesita al menos una opción.");
    }

    private static CampoPersonalizado NuevoCampo(CampoRequest c, int orden) => new()
    {
        Nombre = c.Nombre.Trim(),
        TipoDato = c.TipoDato,
        Obligatorio = c.Obligatorio,
        Opciones = UnirOpciones(c),
        Orden = orden,
    };

    private static string? UnirOpciones(CampoRequest c) =>
        c.TipoDato == TipoDato.Lista && c.Opciones is not null
            ? string.Join('|', c.Opciones.Select(o => o.Trim()).Where(o => o.Length > 0).Distinct())
            : null;

    internal static CampoDto ACampoDto(CampoPersonalizado c) =>
        new(c.Id, c.Nombre, c.TipoDato, c.Obligatorio,
            c.Opciones?.Split('|', StringSplitOptions.RemoveEmptyEntries) ?? [], c.Orden);

    private static CategoriaDto ADto(Categoria c, int cantidad) =>
        new(c.Id, c.Nombre, c.TipoInventario, c.Activo, cantidad, c.Campos.OrderBy(x => x.Orden).Select(ACampoDto).ToList());
}
