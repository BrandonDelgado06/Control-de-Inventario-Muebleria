using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Domain.Entities;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Application.Features.Usuarios;

public record UsuarioDto(int Id, string Nombre, string Usuario, int RolId, string Rol, bool Activo, DateTime FechaCreacion);

public record RolDto(int Id, string Nombre, IReadOnlyList<string> Permisos);

public record CrearUsuarioRequest(
    [Required(ErrorMessage = "El nombre es obligatorio."), MaxLength(100)] string Nombre,
    [Required(ErrorMessage = "El usuario es obligatorio."), MaxLength(30),
     RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "El usuario solo puede tener letras, números, punto, guion y guion bajo.")]
    string Usuario,
    [Required(ErrorMessage = "La contraseña es obligatoria."), MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    string Contrasena,
    [Range(1, int.MaxValue, ErrorMessage = "Elige un rol.")] int RolId);

public record ActualizarUsuarioRequest(
    [Required(ErrorMessage = "El nombre es obligatorio."), MaxLength(100)] string Nombre,
    [Range(1, int.MaxValue, ErrorMessage = "Elige un rol.")] int RolId,
    bool Activo,
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")] string? NuevaContrasena);

public class UsuarioService(IAppDbContext db, IHasherContrasenas hasher, IUsuarioActual actual)
{
    public Task<List<UsuarioDto>> ListarAsync(CancellationToken ct) =>
        db.Usuarios
            .OrderByDescending(u => u.Activo).ThenBy(u => u.Nombre)
            .Select(u => new UsuarioDto(u.Id, u.Nombre, u.NombreUsuario, u.RolId, u.Rol.Nombre, u.Activo, u.FechaCreacion))
            .ToListAsync(ct);

    public async Task<List<RolDto>> RolesAsync(CancellationToken ct)
    {
        var roles = await db.Roles.Include(r => r.Permisos).ThenInclude(p => p.Permiso).OrderBy(r => r.Id).ToListAsync(ct);
        return roles.Select(r => new RolDto(r.Id, r.Nombre, r.Permisos.Select(p => p.Permiso.Codigo).OrderBy(c => c).ToList())).ToList();
    }

    public async Task<UsuarioDto> CrearAsync(CrearUsuarioRequest req, CancellationToken ct)
    {
        var nombreUsuario = req.Usuario.Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUsuario, ct))
            throw new DomainException($"El usuario \"{nombreUsuario}\" ya existe.");
        await ValidarRolAsync(req.RolId, ct);

        var usuario = new Usuario
        {
            Nombre = req.Nombre.Trim(),
            NombreUsuario = nombreUsuario,
            ContrasenaHash = hasher.Hash(req.Contrasena),
            RolId = req.RolId,
            FechaCreacion = DateTime.Now,
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(usuario.Id, ct);
    }

    public async Task<UsuarioDto> ActualizarAsync(int id, ActualizarUsuarioRequest req, CancellationToken ct)
    {
        var usuario = await db.Usuarios.FindAsync([id], ct) ?? throw new NoEncontradoException("Usuario", id);
        await ValidarRolAsync(req.RolId, ct);
        if (id == actual.Id && !req.Activo)
            throw new DomainException("No puedes desactivar tu propia cuenta.");

        usuario.Nombre = req.Nombre.Trim();
        usuario.RolId = req.RolId;
        usuario.Activo = req.Activo;
        if (!string.IsNullOrWhiteSpace(req.NuevaContrasena))
            usuario.ContrasenaHash = hasher.Hash(req.NuevaContrasena);

        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    private async Task<UsuarioDto> ObtenerAsync(int id, CancellationToken ct) =>
        await db.Usuarios.Where(u => u.Id == id)
            .Select(u => new UsuarioDto(u.Id, u.Nombre, u.NombreUsuario, u.RolId, u.Rol.Nombre, u.Activo, u.FechaCreacion))
            .FirstOrDefaultAsync(ct) ?? throw new NoEncontradoException("Usuario", id);

    private async Task ValidarRolAsync(int rolId, CancellationToken ct)
    {
        if (!await db.Roles.AnyAsync(r => r.Id == rolId, ct))
            throw new DomainException("El rol elegido no existe.");
    }
}
