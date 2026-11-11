using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Movimientos;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Enums;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Application.Features.Instalacion;

public record SalidaHerramientaDto(
    int Id, int ArticuloId, string Codigo, string Herramienta, int ResponsableId, string Responsable, string Destino,
    DateOnly FechaSalida, DateOnly FechaPrevista, DateOnly? FechaDevolucion, EstadoArticulo? EstadoDevolucion, int DiasRetraso);

public record PersonaDto(int Id, string Nombre);

public record RegistrarSalidaRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Elige la herramienta.")] int ArticuloId,
    [Range(1, int.MaxValue, ErrorMessage = "Elige quién se la lleva.")] int ResponsableId,
    [Required(ErrorMessage = "Indica el destino."), MaxLength(150)] string Destino,
    DateOnly FechaPrevista);

public record DevolucionRequest(EstadoArticulo Estado);

/// <summary>
/// RF-16 a RF-18: herramientas que salen del taller para instalar muebles.
/// La salida descuenta una unidad (queda "no disponible") y la devolución la repone.
/// </summary>
public class InstalacionService(IAppDbContext db, IUsuarioActual actual)
{
    private int UsuarioId => actual.Id ?? throw new DomainException("No hay un usuario en sesión.");
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    public async Task<List<SalidaHerramientaDto>> ListarAsync(bool soloFuera, CancellationToken ct)
    {
        var q = db.SalidasHerramienta.AsQueryable();
        if (soloFuera) q = q.Where(s => s.FechaDevolucion == null);
        var lista = await q.OrderBy(s => s.FechaDevolucion != null).ThenBy(s => s.FechaPrevista).ThenByDescending(s => s.Id)
            .Select(s => new { s, Codigo = s.Articulo.Codigo, Herramienta = s.Articulo.Nombre, Responsable = s.Responsable.Nombre })
            .Take(200).ToListAsync(ct);
        return lista.Select(x => ADto(x.s, x.Codigo, x.Herramienta, x.Responsable)).ToList();
    }

    /// <summary>Usuarios activos a los que se les puede prestar una herramienta.</summary>
    public Task<List<PersonaDto>> ResponsablesAsync(CancellationToken ct) =>
        db.Usuarios.Where(u => u.Activo).OrderBy(u => u.Nombre).Select(u => new PersonaDto(u.Id, u.Nombre)).ToListAsync(ct);

    /// <summary>RF-16.</summary>
    public async Task<SalidaHerramientaDto> RegistrarSalidaAsync(RegistrarSalidaRequest req, CancellationToken ct)
    {
        var herramienta = await db.Articulos.Include(a => a.Categoria).FirstOrDefaultAsync(a => a.Id == req.ArticuloId, ct)
            ?? throw new NoEncontradoException("Herramienta", req.ArticuloId);
        if (herramienta.Categoria.TipoInventario != TipoInventario.Instalacion)
            throw new DomainException($"\"{herramienta.Nombre}\" no es una herramienta de instalación.");
        if (herramienta.StockActual < 1)
            throw new DomainException($"No hay unidades disponibles de \"{herramienta.Nombre}\": todas están fuera del taller.");
        var responsable = await db.Usuarios.FindAsync([req.ResponsableId], ct) ?? throw new NoEncontradoException("Usuario", req.ResponsableId);
        if (!responsable.Activo) throw new DomainException($"{responsable.Nombre} tiene la cuenta desactivada.");
        if (req.FechaPrevista < Hoy) throw new DomainException("La fecha prevista de devolución no puede ser anterior a hoy.");

        var salida = new SalidaHerramienta
        {
            ArticuloId = herramienta.Id,
            ResponsableId = responsable.Id,
            Destino = req.Destino.Trim(),
            FechaSalida = Hoy,
            FechaPrevista = req.FechaPrevista,
        };
        db.SalidasHerramienta.Add(salida);
        db.Movimientos.Add(MovimientoService.Crear(herramienta, TipoMovimiento.Salida, 1, $"Salida a instalación: {salida.Destino}", UsuarioId));
        await db.SaveChangesAsync(ct);
        return ADto(salida, herramienta.Codigo, herramienta.Nombre, responsable.Nombre);
    }

    /// <summary>RF-17: la herramienta vuelve a estar disponible y, si regresó dañada o desgastada, cambia su estado (RF-11).</summary>
    public async Task<SalidaHerramientaDto> DevolverAsync(int id, DevolucionRequest req, CancellationToken ct)
    {
        var salida = await db.SalidasHerramienta.Include(s => s.Articulo).Include(s => s.Responsable).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NoEncontradoException("Salida", id);
        if (salida.FechaDevolucion is not null) throw new DomainException("Esta herramienta ya fue devuelta.");
        if (req.Estado is EstadoArticulo.DeBaja) throw new DomainException("Para dar de baja una herramienta use el módulo de bajas.");

        var h = salida.Articulo;
        salida.FechaDevolucion = Hoy;
        salida.EstadoDevolucion = req.Estado;
        db.Movimientos.Add(MovimientoService.Crear(h, TipoMovimiento.Entrada, 1, $"Devolución de instalación: {salida.Destino}", UsuarioId));

        if (req.Estado is EstadoArticulo.Desgastada or EstadoArticulo.Danada && h.Estado != req.Estado)
        {
            db.HistorialEstados.Add(new HistorialEstado
            {
                ArticuloId = h.Id, EstadoAnterior = h.Estado, EstadoNuevo = req.Estado, UsuarioId = UsuarioId, FechaHora = DateTime.Now,
            });
            h.Estado = req.Estado;
        }
        await db.SaveChangesAsync(ct);
        return ADto(salida, h.Codigo, h.Nombre, salida.Responsable.Nombre);
    }

    internal static SalidaHerramientaDto ADto(SalidaHerramienta s, string codigo, string herramienta, string responsable)
    {
        var retraso = s.FechaDevolucion is null ? Math.Max(0, Hoy.DayNumber - s.FechaPrevista.DayNumber) : 0;
        return new SalidaHerramientaDto(s.Id, s.ArticuloId, codigo, herramienta, s.ResponsableId, responsable, s.Destino,
            s.FechaSalida, s.FechaPrevista, s.FechaDevolucion, s.EstadoDevolucion, retraso);
    }
}
