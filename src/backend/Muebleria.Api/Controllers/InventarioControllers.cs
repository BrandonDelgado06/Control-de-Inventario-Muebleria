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
public class ArticulosController(ArticuloService articulos, FotoService fotos) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.ArticulosVer)]
    public Task<Paginado<ArticuloResumenDto>> Buscar([FromQuery] ArticuloFiltro filtro, CancellationToken ct) => articulos.BuscarAsync(filtro, ct);

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permisos.ArticulosVer)]
    public Task<ArticuloDetalleDto> Obtener(int id, CancellationToken ct) => articulos.ObtenerAsync(id, ct);

    /// <summary>RF-11: historial de cambios de estado.</summary>
    [HttpGet("{id:int}/estados")]
    [Authorize(Policy = Permisos.ArticulosVer)]
    public Task<List<HistorialEstadoDto>> Estados(int id, CancellationToken ct) => articulos.HistorialEstadosAsync(id, ct);

    /// <summary>RF-13: acabados agrupados por producto, con sus lotes y vencimientos.</summary>
    [HttpGet("acabados")]
    [Authorize(Policy = Permisos.ArticulosVer)]
    public Task<List<AcabadoAgrupadoDto>> Acabados([FromQuery] string? texto, CancellationToken ct) => articulos.AcabadosPorLoteAsync(texto, ct);

    /// <summary>RF-08: fotografía del artículo.</summary>
    [HttpGet("{id:int}/foto")]
    [Authorize(Policy = Permisos.ArticulosVer)]
    public async Task<IActionResult> Foto(int id, CancellationToken ct)
    {
        var foto = await fotos.ObtenerAsync(id, ct);
        Response.Headers.CacheControl = "private, no-cache";
        return File(foto.Contenido, foto.TipoContenido);
    }

    [HttpPost("{id:int}/foto")]
    [Authorize(Policy = Permisos.ArticulosEditar)]
    [RequestSizeLimit(FotoService.TamanoMaximo + 64 * 1024)]
    public async Task<IActionResult> SubirFoto(int id, IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null) return Problem(statusCode: 400, title: "Elige una foto.");
        await using var contenido = archivo.OpenReadStream();
        await fotos.SubirAsync(id, contenido, archivo.Length, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}/foto")]
    [Authorize(Policy = Permisos.ArticulosEditar)]
    public async Task<IActionResult> EliminarFoto(int id, CancellationToken ct)
    {
        await fotos.EliminarAsync(id, ct);
        return NoContent();
    }

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
