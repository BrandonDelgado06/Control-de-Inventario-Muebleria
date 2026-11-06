using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Muebleria.Api.Authorization;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Auth;

namespace Muebleria.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth, IUsuarioActual actual) : ControllerBase
{
    /// <summary>RF-01: inicio de sesión.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<SesionDto> Login(LoginRequest req, CancellationToken ct)
    {
        var sesion = await auth.IniciarSesionAsync(req, ct);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Seguridad.CrearPrincipal(sesion));
        return sesion;
    }

    /// <summary>RF-01: cierre de sesión manual.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>Sesión actual; 401 si expiró por inactividad.</summary>
    [HttpGet("sesion")]
    public async Task<ActionResult<SesionDto>> Sesion(CancellationToken ct) =>
        await auth.ObtenerSesionAsync(actual.Id ?? 0, ct) is { } s ? s : Unauthorized();
}
