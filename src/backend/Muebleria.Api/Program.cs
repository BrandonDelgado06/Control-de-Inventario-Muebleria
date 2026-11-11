using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Muebleria.Api.Authorization;
using Muebleria.Api.Middleware;
using Muebleria.Application;
using Muebleria.Infrastructure;
using Muebleria.Infrastructure.Persistence;
using Muebleria.Infrastructure.Persistence.Seed;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(); // en el taller corre como servicio de Windows

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ctx =>
        new BadRequestObjectResult(new ValidationProblemDetails(ctx.ModelState)
        {
            Title = "Revisa los datos del formulario.",
            Status = StatusCodes.Status400BadRequest,
        }));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorErrores>();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSeguridad(builder.Configuration, builder.Environment.ContentRootPath);

var app = builder.Build();

// Aplica migraciones pendientes y datos iniciales al arrancar.
if (app.Configuration.GetValue("Database:MigrarAlIniciar", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().EjecutarAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous(); // documentación en /scalar
}

// En producción, el build de React vive en wwwroot y se sirve desde el mismo origen.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

public partial class Program; // para las pruebas de integración
