using Microsoft.Extensions.DependencyInjection;
using Muebleria.Application.Features.Alertas;
using Muebleria.Application.Features.Articulos;
using Muebleria.Application.Features.Auth;
using Muebleria.Application.Features.Auditoria;
using Muebleria.Application.Features.Categorias;
using Muebleria.Application.Features.Dashboard;
using Muebleria.Application.Features.Instalacion;
using Muebleria.Application.Features.Movimientos;
using Muebleria.Application.Features.Reportes;
using Muebleria.Application.Features.Respaldos;
using Muebleria.Application.Features.Solicitudes;
using Muebleria.Application.Features.Ubicaciones;
using Muebleria.Application.Features.Usuarios;

namespace Muebleria.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UsuarioService>();
        services.AddScoped<UbicacionService>();
        services.AddScoped<CategoriaService>();
        services.AddScoped<ArticuloService>();
        services.AddScoped<MovimientoService>();
        services.AddScoped<BitacoraService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<SolicitudService>();
        services.AddScoped<InstalacionService>();
        services.AddScoped<AlertaService>();
        services.AddScoped<ReporteService>();
        services.AddScoped<RespaldoService>();
        return services;
    }
}
