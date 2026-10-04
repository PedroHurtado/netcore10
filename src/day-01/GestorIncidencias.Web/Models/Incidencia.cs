namespace GestorIncidencias.Web.Models;

public enum Prioridad
{
    Baja,
    Media,
    Alta,
    Critica
}

public enum EstadoIncidencia
{
    Abierta,
    EnCurso,
    Resuelta,
    Cerrada
}

/// <summary>
/// Entidad principal del dominio. Durante el día 1 vive en el proyecto Web;
/// el día 2 la moveremos a la capa de Dominio (Clean Architecture).
/// </summary>
public class Incidencia
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public Prioridad Prioridad { get; set; } = Prioridad.Media;
    public EstadoIncidencia Estado { get; set; } = EstadoIncidencia.Abierta;
    public DateTimeOffset FechaAlta { get; set; }
    public DateTimeOffset? FechaResolucion { get; set; }
}
