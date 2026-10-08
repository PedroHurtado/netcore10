namespace GestorIncidencias.Domain.Expedientes;

/// <summary>
/// Caso práctico SIREI (día 5). Sustituye al "número mágico" IdEstado del legacy (ddlEstado.SelectedValue == "4").
///
/// Los valores numéricos se fijan A MANO y coinciden con los de la columna IdEstado de la base de datos de SIREI:
/// la aplicación nueva lee y escribe la MISMA tabla que la vieja mientras conviven. Nunca se reordenan.
/// </summary>
public enum EstadoExpediente
{
    Abierto = 1,
    EnTramite = 2,
    Suspendido = 3,
    Cerrado = 4
}
