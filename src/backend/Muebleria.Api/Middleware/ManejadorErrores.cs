using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Muebleria.Domain.Exceptions;

namespace Muebleria.Api.Middleware;

/// <summary>Convierte excepciones en respuestas ProblemDetails con mensajes en español.</summary>
public class ManejadorErrores(IProblemDetailsService problemas, ILogger<ManejadorErrores> log, IHostEnvironment entorno) : IExceptionHandler
{
    /// <summary>Solo en desarrollo y en pruebas se muestra el detalle técnico del error; en el taller, nunca.</summary>
    private bool MostrarDetalle => entorno.IsDevelopment() || entorno.IsEnvironment("Pruebas");

    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (estado, titulo) = ex switch
        {
            NoEncontradoException => (404, ex.Message),
            DomainException => (400, ex.Message),
            DbUpdateConcurrencyException => (409, "Otro usuario modificó este registro al mismo tiempo. Vuelve a intentarlo."),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (409, "Ya existe un registro con esos datos."),
            DbUpdateException { InnerException: SqlException { Number: 547 } } => (400, "La operación no cumple las reglas de la base de datos (por ejemplo, existencia negativa o un dato relacionado inexistente)."),
            _ => (500, "Ocurrió un error inesperado. Si se repite, avisa al administrador."),
        };
        if (estado == 500) log.LogError(ex, "Error no controlado en {Ruta}", ctx.Request.Path);

        ctx.Response.StatusCode = estado;
        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            Exception = ex,
            ProblemDetails = new ProblemDetails { Status = estado, Title = titulo, Detail = estado == 500 && MostrarDetalle ? ex.ToString() : null },
        });
    }
}
