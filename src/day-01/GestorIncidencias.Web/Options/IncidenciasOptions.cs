using System.ComponentModel.DataAnnotations;
using GestorIncidencias.Web.Models;

namespace GestorIncidencias.Web.Options;

/// <summary>
/// Patrón Options: clase fuertemente tipada que se enlaza con la sección
/// "Incidencias" de appsettings.json (y de cualquier otra fuente de configuración).
/// </summary>
public class IncidenciasOptions
{
    public const string Seccion = "Incidencias";

    [Required]
    public string NombreAplicacion { get; set; } = "Gestor de Incidencias";

    /// <summary>Máximo de incidencias abiertas permitidas a la vez.</summary>
    [Range(1, 10_000)]
    public int MaxIncidenciasAbiertas { get; set; } = 100;

    public Prioridad PrioridadPorDefecto { get; set; } = Prioridad.Media;

    /// <summary>Si es true, se cargan incidencias de ejemplo al arrancar.</summary>
    public bool CargarDatosDemo { get; set; }
}
