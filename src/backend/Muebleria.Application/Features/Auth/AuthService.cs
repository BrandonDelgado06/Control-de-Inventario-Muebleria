using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Application.Features.Auth;

public record LoginRequest(
    [Required(ErrorMessage = "Escribe tu usuario.")] string Usuario,
    [Required(ErrorMessage = "Escribe tu contraseña.")] string Contrasena);

public record SesionDto(int Id, string Nombre, string Usuario, string Rol, IReadOnlyList<string> Permisos);

public class AuthService(IAppDbContext db, IHasherContrasenas hasher)
{
    public async Task<SesionDto> IniciarSesionAsync(LoginRequest req, CancellationToken ct)
    {
        var nombreUsuario = req.Usuario.Trim();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario, ct);

        // Mismo mensaje para usuario inexistente o contraseña errónea: no revela qué usuarios existen.
        if (usuario is null || !hasher.Verificar(usuario.ContrasenaHash, req.Contrasena))
            throw new DomainException("Usuario o contraseña incorrectos.");
        if (!usuario.Activo)
            throw new DomainException("Tu cuenta está desactivada. Habla con el administrador.");

        db.Bitacoras.Add(new Bitacora
        {
            UsuarioId = usuario.Id,
            Modulo = "Seguridad",
            Accion = "Iniciar sesión",
            FechaHora = DateTime.Now,
        });
        await db.SaveChangesAsync(ct);

        return (await ObtenerSesionAsync(usuario.Id, ct))!;
    }

    /// <summary>Datos de sesión del usuario activo, o null si no existe o fue desactivado.</summary>
    public async Task<SesionDto?> ObtenerSesionAsync(int usuarioId, CancellationToken ct)
    {
        var u = await db.Usuarios
            .Where(x => x.Id == usuarioId && x.Activo)
            .Select(x => new { x.Id, x.Nombre, x.NombreUsuario, Rol = x.Rol.Nombre, x.RolId })
            .FirstOrDefaultAsync(ct);
        if (u is null) return null;

        var permisos = await db.RolPermisos
            .Where(rp => rp.RolId == u.RolId)
            .Select(rp => rp.Permiso.Codigo)
            .OrderBy(c => c)
            .ToListAsync(ct);

        return new SesionDto(u.Id, u.Nombre, u.NombreUsuario, u.Rol, permisos);
    }
}
