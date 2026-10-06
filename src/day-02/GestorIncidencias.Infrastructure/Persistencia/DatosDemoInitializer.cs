using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Configuracion;
using GestorIncidencias.Domain.Incidencias;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestorIncidencias.Infrastructure.Persistencia;

/// <summary>
/// Carga datos de ejemplo al arrancar si la opción CargarDatosDemo está activa.
/// Igual que el día 1, pero ahora las incidencias se crean con Incidencia.Crear()
/// y sus métodos de negocio (no hay setters públicos).
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

        var impresora = Nueva("La impresora de la 2ª planta no imprime", null, Prioridad.Baja, ahora.AddDays(-3));
        var error500 = Nueva("Error 500 al guardar un expediente", "Ocurre al pulsar Guardar con adjuntos.", Prioridad.Alta, ahora.AddDays(-1));
        var correo = Nueva("Caída del servicio de correo", null, Prioridad.Critica, ahora.AddHours(-2));
        var vpn = Nueva("No funciona la VPN desde casa", "Error de certificado al conectar.", Prioridad.Media, ahora.AddDays(-5));

        error500.Iniciar();
        vpn.Resolver(ahora.AddDays(-4));

        foreach (var incidencia in new[] { impresora, error500, correo, vpn })
            await repositorio.AgregarAsync(incidencia, cancellationToken);

        logger.LogInformation("Cargadas {Total} incidencias de demostración", 4);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static Incidencia Nueva(string titulo, string? descripcion, Prioridad prioridad, DateTimeOffset fecha) =>
        Incidencia.Crear(titulo, descripcion, prioridad, fecha).Valor!;
}
