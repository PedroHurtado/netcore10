namespace GestorIncidencias.Domain.Incidencias;

/// <summary>
/// Ciclo de vida de una incidencia:
///
///   Abierta ──Iniciar──▶ EnCurso ──Resolver──▶ Resuelta ──Cerrar──▶ Cerrada
///      └──────────────Resolver──────────────────▲
/// </summary>
public enum EstadoIncidencia
{
    Abierta,
    EnCurso,
    Resuelta,
    Cerrada
}
