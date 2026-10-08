namespace GestorIncidencias.Domain.Lotes;

/// <summary>
/// Caso práctico Noticom (día 5). Ciclo de vida de un lote de notificaciones:
///   PendienteValidacion → Validado → Remesado
/// En el legacy era un IdEstado numérico que la vista comparaba con literales; aquí es un enum que se guarda como texto.
/// </summary>
public enum EstadoLote
{
    PendienteValidacion,
    Validado,
    Remesado
}

/// <summary>Canal por el que se envían las notificaciones del lote (filtro "Tipo" de la búsqueda avanzada).</summary>
public enum TipoNotificacion
{
    Postal,
    Electronica
}
