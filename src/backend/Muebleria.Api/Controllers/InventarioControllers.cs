using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Muebleria.Api.Authorization;
using Muebleria.Application.Common;
using Muebleria.Application.Features.Articulos;
using Muebleria.Application.Features.Dashboard;
using Muebleria.Application.Features.Movimientos;
using Muebleria.Domain.Enums;

namespace Muebleria.Api.Controllers;

/// <summary>RF-08 a RF-15: catálogo de artículos.</summary>
[ApiController]
[Route("api/articulos")]
public class ArticulosController(ArticuloService articulos) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.ArticulosVer)]
    public Task<Paginado<ArticuloResumenDto>> Buscar([FromQuery] ArticuloFiltro filtro, CancellationToken ct) => articulos.BuscarAsync(filtro, ct);

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permisos.ArticulosVer)]
    public Task<ArticuloDetalleDto> Obtener(int id, CancellationToken ct) => articulos.ObtenerAsync(id, ct);

    [HttpGet("unidades")]
    [Authorize(Policy = Permisos.ArticulosVer)]
    public string[] Unidades() => ArticuloService.UnidadesMedida;

    [HttpPost]
    [Authorize(Policy = Permisos.ArticulosEditar)]
    public Task<ArticuloDetalleDto> Crear(ArticuloRequest req, CancellationToken ct) => articulos.CrearAsync(req, ct);

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.ArticulosEditar)]
    public Task<ArticuloDetalleDto> Actualizar(int id, ArticuloRequest req, CancellationToken ct) => articulos.ActualizarAsync(id, req, ct);

    [HttpPut("{id:int}/activo")]
    [Authorize(Policy = Permisos.ArticulosEditar)]
    public async Task<IActionResult> CambiarActivo(int id, [FromBody] bool activo, CancellationToken ct)
    {
        await articulos.CambiarActivoAsync(id, activo, ct);
        return NoContent();
    }
}

/// <summary>RF-19 a RF-22: entradas, salidas, ajustes e historial.</summary>
[ApiController]
[Route("api/movimientos")]
public class MovimientosController(MovimientoService movimientos) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.ArticulosVer)]
    public Task<Paginado<MovimientoDto>> Listar([FromQuery] MovimientoFiltro filtro, CancellationToken ct) => movimientos.ListarAsync(filtro, ct);

    [HttpPost]
    [Authorize(Policy = Permisos.MovimientosRegistrar)]
    public async Task<ActionResult<MovimientoDto>> Registrar(RegistrarMovimientoRequest req, CancellationToken ct)
    {
        // RF-21: solo el administrador ajusta existencias.
        if (req.Tipo == TipoMovimiento.Ajuste && !User.TienePermiso(Permisos.MovimientosAjustar))
            return Problem(statusCode: 403, title: "Solo el administrador puede ajustar existencias.");
        return await movimientos.RegistrarAsync(req, ct);
    }
}

[ApiController]
[Route("api/panel")]
[Authorize(Policy = Permisos.ArticulosVer)]
public class PanelController(DashboardService panel) : ControllerBase
{
    [HttpGet]
    public Task<ResumenDto> Resumen(CancellationToken ct) => panel.ResumenAsync(ct);
}
