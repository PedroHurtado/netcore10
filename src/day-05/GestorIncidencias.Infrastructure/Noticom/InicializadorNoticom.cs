using GestorIncidencias.Application.Configuracion;
using GestorIncidencias.Domain.Lotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestorIncidencias.Infrastructure.Noticom;

/// <summary>
/// Caso práctico Noticom (día 5): lotes y remesas de demostración (simula la base de datos existente de Noticom).
/// Idempotente: si ya hay lotes, no hace nada.
/// </summary>
public class InicializadorNoticom(
    IServiceScopeFactory scopeFactory,
    TimeProvider reloj,
    IOptions<IncidenciasOptions> opciones,
    ILogger<InicializadorNoticom> logger) : IHostedService
{
    /// <summary>Procesos que generan notificaciones (código → descripción).</summary>
    private static readonly (int Codigo, string Nombre)[] Procesos =
    [
        (120, "Liquidaciones IBI"),
        (130, "Sanciones de tráfico"),
        (145, "Tasa de residuos"),
        (210, "Requerimientos de pago")
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NoticomDbContext>();

        if (!opciones.Value.CargarDatosDemo || await db.Lotes.AnyAsync(cancellationToken))
            return;

        var ahora = reloj.GetUtcNow();

        // 1) 56 lotes: 4 procesos × 2 ejercicios × 7 lotes. Todos empiezan pendientes de validación.
        var lotes = (
            from ejercicio in new[] { 2025, 2026 }
            from proceso in Procesos
            from n in Enumerable.Range(1, 7)
            select Lote.Crear(
                proceso.Codigo, ejercicio, $"{proceso.Nombre} {ejercicio} - lote {n}",
                n % 3 == 0 ? TipoNotificacion.Electronica : TipoNotificacion.Postal,
                numeroNotificaciones: 50 * n + proceso.Codigo % 37,
                fechaAlta: ahora.AddDays(ejercicio == 2025 ? -300 + n : -40 + n)).Valor!
        ).ToList();

        db.Lotes.AddRange(lotes);
        await db.SaveChangesAsync(cancellationToken);

        // 2) Los de 2025 están validados casi todos; los de 2026, la mitad.
        foreach (var lote in lotes.Where(l => l.Ejercicio == 2025 ? l.Id % 7 != 0 : l.Id % 2 == 0))
            lote.Validar(lote.FechaAlta.AddDays(2));

        // 3) Tres remesas con lotes validados de 2025 (el resto de validados quedan disponibles para remesar).
        var validados2025 = lotes.Where(l => l.Ejercicio == 2025 && l.Estado == EstadoLote.Validado).ToList();
        var remesas = new[]
        {
            Remesa.Crear("IBI 2025 - primer envío", validados2025.Where(l => l.Proceso == 120).Take(4).ToList(), ahora.AddDays(-250)),
            Remesa.Crear("Tráfico 2025 - enero", validados2025.Where(l => l.Proceso == 130).Take(3).ToList(), ahora.AddDays(-240)),
            Remesa.Crear("Residuos 2025", validados2025.Where(l => l.Proceso == 145).Take(5).ToList(), ahora.AddDays(-200))
        };
        db.Remesas.AddRange(remesas.Select(r => r.Valor!));

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Datos de demostración de Noticom cargados ({Lotes} lotes, {Remesas} remesas)", lotes.Count, remesas.Length);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
