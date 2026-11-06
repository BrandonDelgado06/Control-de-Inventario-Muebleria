using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Muebleria.Application.Common;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Application.Features.Auth;

namespace Muebleria.Api.Authorization;

public class UsuarioActual(IHttpContextAccessor accessor) : IUsuarioActual
{
    public int? Id =>
        int.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

public static class Seguridad
{
    public const string ClaimPermiso = "permiso";

    /// <summary>
    /// Cookie HttpOnly con expiración deslizante (RF-01: cierre por inactividad) y una política
    /// de autorización por cada permiso (RNF-02: se valida en el servidor en cada petición).
    /// </summary>
    public static IServiceCollection AddSeguridad(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUsuarioActual, UsuarioActual>();

        var minutos = config.GetValue("Sesion:MinutosInactividad", 30);
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "muebleria.sesion";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // el taller usa http en la red local
                o.ExpireTimeSpan = TimeSpan.FromMinutes(minutos);
                o.SlidingExpiration = true;
                // Es una API: responde 401/403 en lugar de redirigir a una página de login.
                o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
                // Si el usuario fue desactivado, su sesión deja de valer de inmediato.
                o.Events.OnValidatePrincipal = async ctx =>
                {
                    var id = int.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var x) ? x : 0;
                    var auth = ctx.HttpContext.RequestServices.GetRequiredService<AuthService>();
                    if (await auth.ObtenerSesionAsync(id, ctx.HttpContext.RequestAborted) is null)
                    {
                        ctx.RejectPrincipal();
                        await ctx.HttpContext.SignOutAsync();
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.AddAuthorization(o =>
        {
            foreach (var permiso in Permisos.Todos)
                o.AddPolicy(permiso, p => p.RequireAuthenticatedUser().RequireClaim(ClaimPermiso, permiso));
        });
        return services;
    }

    public static ClaimsPrincipal CrearPrincipal(SesionDto s)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, s.Id.ToString()),
            new(ClaimTypes.Name, s.Nombre),
            new(ClaimTypes.Role, s.Rol),
        };
        claims.AddRange(s.Permisos.Select(p => new Claim(ClaimPermiso, p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static bool TienePermiso(this ClaimsPrincipal user, string permiso) => user.HasClaim(ClaimPermiso, permiso);
}
