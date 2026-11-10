using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Muebleria.Api.Authorization;
using Muebleria.Application.Common;
using Muebleria.Application.Features.Instalacion;
using Muebleria.Application.Features.Solicitudes;

namespace Muebleria.Api.Controllers;

/// <summary>RF-23 a RF-25: solicitudes de materiales.</summary>
[ApiController]
[Route("api/solicitudes")]
public class SolicitudesController(SolicitudService solicitudes) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.SolicitudesCrear)]
    public Task<Paginado<SolicitudDto>> Listar([FromQuery] SolicitudFiltro filtro, CancellationToken ct) =>
        solicitudes.ListarAsync(filtro, User.TienePermiso(Permisos.SolicitudesAprobar), ct);

    /// <summary>Pendientes de aprobar o de entregar (para el contador del menú).</summary>
    [HttpGet("pendientes")]
    [Authorize(Policy = Permisos.SolicitudesAprobar)]
    public Task<int> Pendientes(CancellationToken ct) => solicitudes.PendientesAsync(ct);

    [HttpPost]
    [Authorize(Policy = Permisos.SolicitudesCrear)]
    public Task<SolicitudDto> Crear(CrearSolicitudRequest req, CancellationToken ct) => solicitudes.CrearAsync(req, ct);

    [HttpPost("{id:int}/aprobar")]
    [Authorize(Policy = Permisos.SolicitudesAprobar)]
    public Task<SolicitudDto> Aprobar(int id, CancellationToken ct) => solicitudes.AprobarAsync(id, ct);

    [HttpPost("{id:int}/rechazar")]
    [Authorize(Policy = Permisos.SolicitudesAprobar)]
    public Task<SolicitudDto> Rechazar(int id, RechazarSolicitudRequest req, CancellationToken ct) => solicitudes.RechazarAsync(id, req, ct);

    [HttpPost("{id:int}/entregar")]
    [Authorize(Policy = Permisos.SolicitudesAprobar)]
    public Task<SolicitudDto> Entregar(int id, CancellationToken ct) => solicitudes.EntregarAsync(id, ct);
}

/// <summary>RF-16 a RF-18: herramientas de instalación fuera del taller.</summary>
[ApiController]
[Route("api/instalacion")]
[Authorize(Policy = Permisos.HerramientasPrestar)]
public class InstalacionController(InstalacionService instalacion) : ControllerBase
{
    [HttpGet]
    public Task<List<SalidaHerramientaDto>> Listar([FromQuery] bool soloFuera, CancellationToken ct) => instalacion.ListarAsync(soloFuera, ct);

    [HttpGet("responsables")]
    public Task<List<PersonaDto>> Responsables(CancellationToken ct) => instalacion.ResponsablesAsync(ct);

    [HttpPost]
    public Task<SalidaHerramientaDto> RegistrarSalida(RegistrarSalidaRequest req, CancellationToken ct) => instalacion.RegistrarSalidaAsync(req, ct);

    [HttpPost("{id:int}/devolucion")]
    public Task<SalidaHerramientaDto> Devolver(int id, DevolucionRequest req, CancellationToken ct) => instalacion.DevolverAsync(id, req, ct);
}
