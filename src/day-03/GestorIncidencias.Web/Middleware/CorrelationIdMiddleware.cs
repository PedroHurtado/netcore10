namespace GestorIncidencias.Web.Middleware;

/// <summary>
/// Middleware "por convención": clase con constructor que recibe RequestDelegate
/// y un método InvokeAsync(HttpContext). Se crea UNA sola vez (singleton),
/// por lo que los servicios scoped se piden como parámetros de InvokeAsync, no en el constructor.
///
/// Función: asegura que cada petición tiene un identificador de correlación,
/// lo devuelve en la respuesta y lo añade al ámbito de logging.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string Cabecera = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[Cabecera].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;

        // OnStarting: se ejecuta justo antes de enviar las cabeceras de respuesta.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Cabecera] = correlationId;
            return Task.CompletedTask;
        });

        // Todo lo que se loguee "dentro" llevará el CorrelationId.
        using (logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
        {
            await next(context); // ← pasa el control al siguiente middleware
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>Método de extensión para usarlo como app.UseCorrelationId().</summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();
}
