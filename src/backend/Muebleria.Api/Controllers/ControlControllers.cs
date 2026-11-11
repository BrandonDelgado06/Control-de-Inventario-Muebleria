using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Muebleria.Api.Authorization;
using Muebleria.Application.Common;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Alertas;
using Muebleria.Application.Features.Reportes;
using Muebleria.Application.Features.Respaldos;

namespace Muebleria.Api.Controllers;

/// <summary>RF-30 a RF-32.</summary>
[ApiController]
[Route("api/alertas")]
[Authorize(Policy = Permisos.AlertasVer)]
public class AlertasController(AlertaService alertas) : ControllerBase
{
    private bool VeRespaldos => User.TienePermiso(Permisos.RespaldosGestionar);

    [HttpGet]
    public Task<AlertasDto> Listar(CancellationToken ct) => alertas.ListarAsync(VeRespaldos, ct);

    [HttpGet("resumen")]
    public Task<ResumenAlertasDto> Resumen(CancellationToken ct) => alertas.ResumenAsync(VeRespaldos, ct);

    /// <summary>RF-31: anticipación configurable por el administrador.</summary>
    [HttpPut("configuracion")]
    [Authorize(Policy = Permisos.CatalogosGestionar)]
    public Task<int> Configurar(ConfigAlertasRequest req, CancellationToken ct) => alertas.ConfigurarAsync(req, ct);
}

/// <summary>RF-33 a RF-35.</summary>
[ApiController]
[Route("api/reportes")]
[Authorize(Policy = Permisos.ReportesVer)]
public class ReportesController(ReporteService reportes) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<TipoReporteDto> Tipos() => ReporteService.Tipos;

    [HttpGet("responsables")]
    public Task<List<Application.Features.Instalacion.PersonaDto>> Responsables(CancellationToken ct) => reportes.ResponsablesAsync(ct);

    [HttpGet("{clave}")]
    public Task<ReporteDto> Ver(string clave, [FromQuery] ReporteFiltro filtro, CancellationToken ct) => reportes.GenerarAsync(clave, filtro, ct);

    /// <summary>Descarga el reporte en PDF o Excel (formato = pdf | xlsx).</summary>
    [HttpGet("{clave}/exportar")]
    public async Task<IActionResult> Exportar(string clave, [FromQuery] string formato, [FromQuery] ReporteFiltro filtro, CancellationToken ct)
    {
        var (contenido, tipo, archivo) = await reportes.ExportarAsync(clave, formato?.ToLowerInvariant() ?? "", filtro, ct);
        return File(contenido, tipo, archivo);
    }
}

/// <summary>RF-38 y RF-39.</summary>
[ApiController]
[Route("api/respaldos")]
[Authorize(Policy = Permisos.RespaldosGestionar)]
public class RespaldosController(RespaldoService respaldos) : ControllerBase
{
    [HttpGet]
    public Task<RespaldosDto> Listar(CancellationToken ct) => respaldos.ListarAsync(ct);

    [HttpPost]
    public Task<RespaldoInfo> Generar(CancellationToken ct) => respaldos.GenerarAsync(ct);

    [HttpPost("restaurar")]
    public async Task<IActionResult> Restaurar(RestaurarRequest req, CancellationToken ct)
    {
        await respaldos.RestaurarAsync(req, ct);
        return NoContent();
    }
}
