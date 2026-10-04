using GestorIncidencias.Web.Lifetimes;
using GestorIncidencias.Web.Options;
using Microsoft.Extensions.Options;

namespace GestorIncidencias.Web.Endpoints;

/// <summary>
/// Endpoints didácticos para observar DI, configuración y entorno.
/// Solo se mapean en el entorno Development.
/// </summary>
public static class DemoEndpoints
{
    public static RouteGroupBuilder MapDemos(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/demo").WithTags("Demos");

        // Comparar lifetimes: refresca varias veces y observa qué GUID cambia.
        grupo.MapGet("/lifetimes", (
            IOperacionTransient transient,
            IOperacionScoped scoped,
            IOperacionSingleton singleton,
            ConsumidorOperaciones consumidor) => new
            {
                Endpoint = new { Transient = transient.Id, Scoped = scoped.Id, Singleton = singleton.Id },
                Consumidor = consumidor.Ids
            });

        // IOptions vs IOptionsSnapshot vs IOptionsMonitor.
        // Cambia appsettings.json con la app arrancada y vuelve a llamar.
        grupo.MapGet("/opciones", (
            IOptions<IncidenciasOptions> options,
            IOptionsSnapshot<IncidenciasOptions> snapshot,
            IOptionsMonitor<IncidenciasOptions> monitor) => new
            {
                IOptions = options.Value.MaxIncidenciasAbiertas,
                IOptionsSnapshot = snapshot.Value.MaxIncidenciasAbiertas,
                IOptionsMonitor = monitor.CurrentValue.MaxIncidenciasAbiertas
            });

        // Lectura directa de IConfiguration y del entorno.
        grupo.MapGet("/configuracion", (IConfiguration config, IWebHostEnvironment env) => new
        {
            Entorno = env.EnvironmentName,
            env.ApplicationName,
            env.ContentRootPath,
            NombreAplicacion = config["Incidencias:NombreAplicacion"],
            MaxAbiertas = config.GetValue<int>("Incidencias:MaxIncidenciasAbiertas"),
            Mensaje = config["MensajeBienvenida"],
            // Las fuentes de configuración, en orden de prioridad creciente:
            Fuentes = ((IConfigurationRoot)config).Providers.Select(p => p.ToString())
        });

        // Provoca una excepción para ver el manejo de errores según el entorno.
        grupo.MapGet("/error", () =>
        {
            throw new InvalidOperationException("Error de prueba lanzado desde /demo/error");
        });

        return grupo;
    }
}
