using GestorIncidencias.Domain.Incidencias;
using Microsoft.Extensions.Logging;

namespace GestorIncidencias.Application.Incidencias;

/// <summary>
/// Mensajes de log de los casos de uso, con [LoggerMessage]: el compilador GENERA el código de cada método.
///
/// Frente a logger.LogInformation("Incidencia {Id} creada...", id):
///   - Más rápido: la plantilla se analiza una vez al compilar, no en cada llamada, y no hay "boxing" de los int.
///   - Si el nivel está desactivado, no se hace ningún trabajo.
///   - Cada mensaje tiene un EventId fijo: se puede buscar y crear alertas por él ("avísame si aparece el 1003").
///   - Todos los mensajes del módulo están juntos y con nombre: se revisan de un vistazo.
///
/// Siguen siendo logs ESTRUCTURADOS: {Id}, {Prioridad} y {Usuario} viajan como propiedades con nombre,
/// no como parte de un texto (ver docs/day-04/07-logging-monitorizacion.md).
/// </summary>
internal static partial class IncidenciaLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Incidencia {Id} creada con prioridad {Prioridad} por {Usuario}")]
    public static partial void IncidenciaCreada(this ILogger logger, int id, Prioridad prioridad, string usuario);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Incidencia {Id} modificada (estado {Estado}) por {Usuario}")]
    public static partial void IncidenciaModificada(this ILogger logger, int id, EstadoIncidencia estado, string usuario);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning,
        Message = "Límite de incidencias abiertas alcanzado ({Max})")]
    public static partial void LimiteAbiertasAlcanzado(this ILogger logger, int max);

    // Debug: las reglas de negocio que rechazan una operación son flujo normal, no errores de la aplicación.
    [LoggerMessage(EventId = 1004, Level = LogLevel.Debug,
        Message = "Operación rechazada sobre la incidencia {Id}: {Motivo}")]
    public static partial void OperacionRechazada(this ILogger logger, int id, string motivo);
}
