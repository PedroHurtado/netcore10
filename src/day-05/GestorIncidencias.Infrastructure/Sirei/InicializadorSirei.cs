using GestorIncidencias.Application.Configuracion;
using GestorIncidencias.Domain.Expedientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestorIncidencias.Infrastructure.Sirei;

/// <summary>
/// Caso práctico SIREI (día 5): expedientes de demostración. En el proyecto real NO existiría:
/// los datos ya están en la base de datos de SIREI. Aquí simula esa base de datos "que ya tiene años de datos".
///
/// Idempotente, como InicializadorBaseDatos: si ya hay expedientes, no hace nada.
/// </summary>
public class InicializadorSirei(
    IServiceScopeFactory scopeFactory,
    TimeProvider reloj,
    IOptions<IncidenciasOptions> opciones,
    ILogger<InicializadorSirei> logger) : IHostedService
{
    private static readonly string[] Titulares =
    [
        "María López Sánchez", "Construcciones Robledo S.L.", "Juan Martín Gil", "Comunidad de Propietarios Avda. Castilla 12",
        "Lucía Fernández Ortega", "Talleres Hermanos Díaz", "Pedro Navarro Ruiz", "Asociación Vecinal El Prado",
        "Elena Castro Romero", "Hostelería del Centro S.A."
    ];

    private static readonly string[] Asuntos =
    [
        "Licencia de obra menor", "Solicitud de subvención para rehabilitación", "Reclamación de responsabilidad patrimonial",
        "Cambio de titularidad de actividad", "Recurso de reposición", "Licencia de ocupación de vía pública",
        "Solicitud de vado permanente"
    ];

    private static readonly string[] Tramites =
    [
        "Registro de entrada", "Requerimiento de documentación", "Informe técnico", "Informe jurídico",
        "Propuesta de resolución", "Notificación al interesado"
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SireiDbContext>();

        if (!opciones.Value.CargarDatosDemo || await db.Expedientes.AnyAsync(cancellationToken))
            return;

        var ahora = reloj.GetUtcNow();

        // 1) Expedientes con sus trámites (todos pendientes al principio).
        var expedientes = Enumerable.Range(1, 45).Select(n =>
        {
            var alta = ahora.AddDays(-n * 3);
            var expediente = Expediente.Crear($"2026/{n:000000}", Titulares[n % Titulares.Length], Asuntos[n % Asuntos.Length], alta).Valor!;
            var numeroTramites = n == 1 ? 3 : n % 5;   // de 0 a 4 trámites
            for (var t = 0; t < numeroTramites; t++)
                expediente.AgregarTramite(Tramites[t], alta.AddDays(t + 1));
            return expediente;
        }).ToList();

        db.Expedientes.AddRange(expedientes);
        await db.SaveChangesAsync(cancellationToken);   // aquí se generan los Id de los trámites

        // 2) Ahora que los trámites tienen Id, se completan algunos y se cambian estados, como en la vida real.
        foreach (var (expediente, n) in expedientes.Select((e, i) => (e, i + 1)))
        {
            // El expediente 1 se queda con TODOS sus trámites pendientes: sirve para probar la regla de cierre.
            if (n == 1)
                continue;

            var tramites = expediente.Tramites.OrderBy(t => t.Id).ToList();
            var completar = n % 3 == 0 ? tramites.Count : tramites.Count / 2;
            foreach (var tramite in tramites.Take(completar))
                expediente.CompletarTramite(tramite.Id, tramite.FechaAlta.AddDays(1));

            var estado = (n % 6) switch
            {
                0 => EstadoExpediente.Cerrado,
                1 or 2 => EstadoExpediente.Abierto,
                5 => EstadoExpediente.Suspendido,
                _ => EstadoExpediente.EnTramite
            };
            if (estado != EstadoExpediente.Abierto)
                expediente.Actualizar(null, estado, expediente.FechaAlta.AddDays(n));   // si tuviera pendientes y fuera "Cerrado", el dominio lo rechaza
        }

        var filas = await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Datos de demostración de SIREI cargados ({Expedientes} expedientes, {Filas} filas actualizadas)", expedientes.Count, filas);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
