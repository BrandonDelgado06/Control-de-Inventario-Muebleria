using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Muebleria.Application.Common;
using Muebleria.Application.Features.Bajas;
using Muebleria.Application.Features.InventarioFisico;
using Muebleria.Application.Features.Movimientos;

namespace Muebleria.Api.Controllers;

/// <summary>RF-26 y RF-27: mermas de materiales y bajas de herramientas.</summary>
[ApiController]
[Route("api/bajas")]
[Authorize(Policy = Permisos.MovimientosRegistrar)]
public class BajasController(BajaService bajas) : ControllerBase
{
    [HttpGet]
    public Task<Paginado<MovimientoDto>> Listar([FromQuery] BajasFiltro filtro, CancellationToken ct) => bajas.ListarAsync(filtro, ct);

    [HttpPost("merma")]
    public Task<MovimientoDto> Merma(RegistrarMermaRequest req, CancellationToken ct) => bajas.RegistrarMermaAsync(req, ct);

    [HttpPost("herramienta")]
    public Task<MovimientoDto> Herramienta(DarDeBajaRequest req, CancellationToken ct) => bajas.DarDeBajaAsync(req, ct);
}

/// <summary>RF-28 y RF-29: conteo físico y ajuste de diferencias.</summary>
[ApiController]
[Route("api/conteos")]
[Authorize(Policy = Permisos.InventarioContar)]
public class ConteosController(ConteoService conteos) : ControllerBase
{
    [HttpGet]
    public Task<List<ConteoResumenDto>> Listar(CancellationToken ct) => conteos.ListarAsync(ct);

    [HttpGet("{id:int}")]
    public Task<ConteoDto> Obtener(int id, CancellationToken ct) => conteos.ObtenerAsync(id, ct);

    [HttpPost]
    public Task<ConteoDto> Crear(CrearConteoRequest req, CancellationToken ct) => conteos.CrearAsync(req, ct);

    [HttpPut("{id:int}")]
    public Task<ConteoDto> Registrar(int id, RegistrarConteoRequest req, CancellationToken ct) => conteos.RegistrarAsync(id, req, ct);

    /// <summary>RF-29: aplicar las diferencias es un ajuste de existencias, así que solo lo hace el administrador.</summary>
    [HttpPost("{id:int}/aplicar")]
    [Authorize(Policy = Permisos.MovimientosAjustar)]
    public Task<ConteoDto> Aplicar(int id, CancellationToken ct) => conteos.AplicarAsync(id, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await conteos.EliminarAsync(id, ct);
        return NoContent();
    }
}
