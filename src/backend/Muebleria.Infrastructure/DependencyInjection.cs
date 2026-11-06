using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muebleria.Application.Common.Interfaces;
using Muebleria.Infrastructure.Persistence;
using Muebleria.Infrastructure.Persistence.Seed;
using Muebleria.Infrastructure.Security;

namespace Muebleria.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var conexion = config.GetConnectionString("Inventario");
        if (string.IsNullOrWhiteSpace(conexion))
            throw new InvalidOperationException(
                "Falta la cadena de conexión ConnectionStrings:Inventario (en desarrollo está en appsettings.Development.json; " +
                "revisa que ASPNETCORE_ENVIRONMENT=Development).");

        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(conexion));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IHasherContrasenas, HasherContrasenas>();
        services.AddScoped<DbSeeder>();
        return services;
    }
}
