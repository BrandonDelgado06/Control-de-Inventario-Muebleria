using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Muebleria.Application.Common;
using Muebleria.Application.Features.Articulos;
using Muebleria.Application.Features.Auditoria;
using Muebleria.Application.Features.Categorias;
using Muebleria.Application.Features.Ubicaciones;
using Muebleria.Application.Features.Usuarios;

namespace Muebleria.Api.Controllers;

/// <summary>RF-02: gestión de usuarios y roles.</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Policy = Permisos.UsuariosGestionar)]
public class UsuariosController(UsuarioService usuarios) : ControllerBase
{
    [HttpGet]
    public Task<List<UsuarioDto>> Listar(CancellationToken ct) => usuarios.ListarAsync(ct);

    [HttpGet("roles")]
    public Task<List<RolDto>> Roles(CancellationToken ct) => usuarios.RolesAsync(ct);

    [HttpPost]
    public Task<UsuarioDto> Crear(CrearUsuarioRequest req, CancellationToken ct) => usuarios.CrearAsync(req, ct);

    [HttpPut("{id:int}")]
    public Task<UsuarioDto> Actualizar(int id, ActualizarUsuarioRequest req, CancellationToken ct) => usuarios.ActualizarAsync(id, req, ct);
}

/// <summary>RF-05 y RF-06: ubicaciones del taller.</summary>
[ApiController]
[Route("api/ubicaciones")]
public class UbicacionesController(UbicacionService ubicaciones) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.ArticulosVer)]
    public Task<List<UbicacionDto>> Listar([FromQuery] bool incluirInactivas, CancellationToken ct) => ubicaciones.ListarAsync(incluirInactivas, ct);

    [HttpGet("{id:int}/articulos")]
    [Authorize(Policy = Permisos.ArticulosVer)]
    public Task<List<ArticuloResumenDto>> Articulos(int id, CancellationToken ct) => ubicaciones.ArticulosAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Permisos.CatalogosGestionar)]
    public Task<UbicacionDto> Crear(UbicacionRequest req, CancellationToken ct) => ubicaciones.CrearAsync(req, ct);

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.CatalogosGestionar)]
    public Task<UbicacionDto> Actualizar(int id, UbicacionRequest req, CancellationToken ct) => ubicaciones.ActualizarAsync(id, req, ct);
}

/// <summary>RF-07: categorías y campos personalizados.</summary>
[ApiController]
[Route("api/categorias")]
public class CategoriasController(CategoriaService categorias) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.ArticulosVer)]
    public Task<List<CategoriaDto>> Listar([FromQuery] bool incluirInactivas, CancellationToken ct) => categorias.ListarAsync(incluirInactivas, ct);

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permisos.ArticulosVer)]
    public Task<CategoriaDto> Obtener(int id, CancellationToken ct) => categorias.ObtenerAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Permisos.CatalogosGestionar)]
    public Task<CategoriaDto> Crear(CategoriaRequest req, CancellationToken ct) => categorias.CrearAsync(req, ct);

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.CatalogosGestionar)]
    public Task<CategoriaDto> Actualizar(int id, CategoriaRequest req, CancellationToken ct) => categorias.ActualizarAsync(id, req, ct);
}

/// <summary>RF-04: consulta de la bitácora.</summary>
[ApiController]
[Route("api/bitacora")]
[Authorize(Policy = Permisos.BitacoraVer)]
public class BitacoraController(BitacoraService bitacora) : ControllerBase
{
    [HttpGet]
    public Task<Paginado<BitacoraDto>> Listar([FromQuery] BitacoraFiltro filtro, CancellationToken ct) => bitacora.ListarAsync(filtro, ct);

    [HttpGet("modulos")]
    public Task<List<string>> Modulos(CancellationToken ct) => bitacora.ModulosAsync(ct);
}
