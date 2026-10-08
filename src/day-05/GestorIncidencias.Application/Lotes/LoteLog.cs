using Microsoft.Extensions.Logging;

namespace GestorIncidencias.Application.Lotes;

/// <summary>Mensajes de log del módulo de lotes y remesas (serie 3000).</summary>
internal static partial class LoteLog
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Lote {Id} validado por {Usuario}")]
    public static partial void LoteValidado(this ILogger logger, int id, string usuario);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information, Message = "Lote {Id} borrado por {Usuario}")]
    public static partial void LoteBorrado(this ILogger logger, int id, string usuario);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information,
        Message = "Remesa {RemesaId} '{Nombre}' creada con {NumeroLotes} lotes por {Usuario}")]
    public static partial void RemesaCreada(this ILogger logger, int remesaId, string nombre, int numeroLotes, string usuario);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Debug, Message = "Operación rechazada sobre lotes: {Motivo}")]
    public static partial void OperacionRechazada(this ILogger logger, string motivo);
}
