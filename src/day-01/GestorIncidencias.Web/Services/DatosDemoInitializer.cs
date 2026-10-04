using GestorIncidencias.Web.Models;
using GestorIncidencias.Web.Options;
using Microsoft.Extensions.Options;

namespace GestorIncidencias.Web.Services;

/// <summary>
/// Servicio en segundo plano (IHostedService) que se ejecuta al arrancar la aplicación.
/// Si la opción CargarDatosDemo está activa, inserta incidencias de ejemplo.
///
/// IMPORTANTE: un IHostedService es Singleton. NO puede recibir por constructor
/// el repositorio (Scoped) porque quedaría "capturado" (captive dependency).
/// Por eso crea su propio ámbito con IServiceScopeFactory.
/// </summary>
public class DatosDemoInitializer(
    IServiceScopeFactory scopeFactory,
    TimeProvider reloj,
    IOptions<IncidenciasOptions> opciones,
    ILogger<DatosDemoInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!opciones.Value.CargarDatosDemo)
            return;

        using var scope = scopeFactory.CreateScope();
        var repositorio = scope.ServiceProvider.GetRequiredService<IIncidenciaRepository>();

        var ahora = reloj.GetUtcNow();
        await repositorio.AgregarAsync(new Incidencia { Titulo = "La impresora de la 2ª planta no imprime", Prioridad = Prioridad.Baja, FechaAlta = ahora.AddDays(-3) }, cancellationToken);
        await repositorio.AgregarAsync(new Incidencia { Titulo = "Error 500 al guardar un expediente", Descripcion = "Ocurre al pulsar Guardar con adjuntos.", Prioridad = Prioridad.Alta, FechaAlta = ahora.AddDays(-1) }, cancellationToken);
        await repositorio.AgregarAsync(new Incidencia { Titulo = "Caída del servicio de correo", Prioridad = Prioridad.Critica, FechaAlta = ahora.AddHours(-2) }, cancellationToken);

        logger.LogInformation("Cargadas {Total} incidencias de demostración",
            (await repositorio.ObtenerTodasAsync(cancellationToken)).Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
