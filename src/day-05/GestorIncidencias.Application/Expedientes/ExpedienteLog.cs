using GestorIncidencias.Domain.Expedientes;
using Microsoft.Extensions.Logging;

namespace GestorIncidencias.Application.Expedientes;

/// <summary>
/// Mensajes de log del módulo de expedientes (serie 2000; las incidencias usan la 1000).
/// En SIREI no había log: cuando algo fallaba, el usuario veía la página amarilla de ASP.NET.
/// </summary>
internal static partial class ExpedienteLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
        Message = "Expediente {Numero} actualizado (estado {Estado}, versión {Version}) por {Usuario}")]
    public static partial void ExpedienteActualizado(this ILogger logger, string numero, EstadoExpediente estado, int version, string usuario);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information,
        Message = "Expediente {Numero}: {Operacion} por {Usuario}")]
    public static partial void TramitesModificados(this ILogger logger, string numero, string operacion, string usuario);

    // Warning: no es un error de la aplicación, pero si se repite mucho indica que dos personas trabajan sobre lo mismo.
    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning,
        Message = "Conflicto de concurrencia en el expediente {Numero}: versión editada {VersionEditada}, versión actual {VersionActual}")]
    public static partial void ConflictoConcurrencia(this ILogger logger, string numero, int versionEditada, int versionActual);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Debug,
        Message = "Operación rechazada sobre el expediente {Id}: {Motivo}")]
    public static partial void OperacionRechazada(this ILogger logger, int id, string motivo);
}
