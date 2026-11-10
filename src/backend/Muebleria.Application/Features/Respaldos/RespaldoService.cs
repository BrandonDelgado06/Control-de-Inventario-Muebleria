using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Application.Features.Respaldos;

public record RespaldosDto(string Carpeta, IReadOnlyList<RespaldoInfo> Respaldos);

public record RestaurarRequest(
    [Range(1, int.MaxValue)] int RespaldoId,
    [Required(ErrorMessage = "Escribe RESTAURAR para confirmar.")] string Confirmacion);

/// <summary>
/// RF-38 y RF-39 (RNF-07). Antes de restaurar se genera automáticamente una copia del estado actual,
/// así un error al elegir el archivo siempre se puede deshacer.
/// </summary>
public class RespaldoService(IAppDbContext db, IRespaldos respaldos, IUsuarioActual actual)
{
    public const string PalabraConfirmacion = "RESTAURAR";

    public async Task<RespaldosDto> ListarAsync(CancellationToken ct) =>
        new(await respaldos.CarpetaAsync(ct), await respaldos.ListarAsync(ct));

    /// <summary>RF-38: copia con fecha y hora en el nombre.</summary>
    public async Task<RespaldoInfo> GenerarAsync(CancellationToken ct)
    {
        var usuario = actual.Id ?? throw new DomainException("No hay un usuario en sesión.");
        var r = await respaldos.GenerarAsync("Respaldo manual", ct);
        db.Respaldos.Add(new Respaldo { Archivo = r.Archivo, FechaHora = r.Fecha, UsuarioId = usuario, TamanoBytes = r.TamanoBytes });
        db.Bitacoras.Add(new Bitacora { UsuarioId = usuario, Modulo = "Respaldos", Accion = $"Generar respaldo {Path.GetFileName(r.Archivo)}", FechaHora = DateTime.Now });
        await db.SaveChangesAsync(ct);
        return r;
    }

    /// <summary>RF-39: reemplaza los datos actuales; exige escribir la palabra de confirmación.</summary>
    public async Task RestaurarAsync(RestaurarRequest req, CancellationToken ct)
    {
        if (!string.Equals(req.Confirmacion?.Trim(), PalabraConfirmacion, StringComparison.Ordinal))
            throw new DomainException($"Para confirmar escribe {PalabraConfirmacion} en mayúsculas.");
        var usuario = actual.Id ?? throw new DomainException("No hay un usuario en sesión.");
        var elegido = (await respaldos.ListarAsync(ct)).FirstOrDefault(r => r.Id == req.RespaldoId)
            ?? throw new NoEncontradoException("Respaldo", req.RespaldoId);

        var previo = await respaldos.GenerarAsync("Automático antes de restaurar", ct);
        await respaldos.RestaurarAsync(elegido.Id, ct);

        // La base ya es la restaurada: la bitácora se escribe ahí, si el usuario existe en esa copia.
        db.OlvidarCambios();
        if (await db.Usuarios.AnyAsync(u => u.Id == usuario, ct))
        {
            db.Bitacoras.Add(new Bitacora
            {
                UsuarioId = usuario, Modulo = "Respaldos", FechaHora = DateTime.Now,
                Accion = $"Restaurar respaldo #{elegido.Id} del {elegido.Fecha:dd/MM/yyyy HH:mm} (copia previa #{previo.Id})",
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
