using System.Diagnostics;
using System.Globalization;

namespace GestorIncidencias.Web.Middleware;

/// <summary>
/// Middleware "por factoría": implementa IMiddleware y se registra en el contenedor de DI.
/// Se activa en cada petición según el lifetime con que se registre (aquí Transient),
/// por lo que SÍ puede recibir servicios scoped en el constructor.
///
/// Función: mide cuánto tarda la petición en el resto del pipeline.
/// </summary>
public class TiempoRespuestaMiddleware(ILogger<TiempoRespuestaMiddleware> logger) : IMiddleware
{
    public const string Cabecera = "X-Tiempo-Respuesta-ms";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var inicio = Stopwatch.GetTimestamp();

        context.Response.OnStarting(() =>
        {
            var ms = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
            context.Response.Headers[Cabecera] = ms.ToString("F2", CultureInfo.InvariantCulture);
            return Task.CompletedTask;
        });

        await next(context);

        // Código "de vuelta": se ejecuta cuando el resto del pipeline ya ha respondido.
        var total = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
        logger.LogInformation("{Metodo} {Ruta} → {Estado} en {Ms:F2} ms",
            context.Request.Method, context.Request.Path, context.Response.StatusCode, total);
    }
}
