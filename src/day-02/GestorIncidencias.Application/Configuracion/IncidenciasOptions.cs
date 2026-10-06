using System.ComponentModel.DataAnnotations;
using GestorIncidencias.Domain.Incidencias;

namespace GestorIncidencias.Application.Configuracion;

/// <summary>
/// Opciones de negocio enlazadas con la sección "Incidencias" de appsettings.json.
/// Viven en Aplicación porque las usan los casos de uso; el enlace con la
/// configuración se hace en el proyecto Web (Program.cs).
/// </summary>
public class IncidenciasOptions
{
    public const string Seccion = "Incidencias";

    [Required]
    public string NombreAplicacion { get; set; } = "Gestor de Incidencias";

    [Range(1, 10_000)]
    public int MaxIncidenciasAbiertas { get; set; } = 100;

    public Prioridad PrioridadPorDefecto { get; set; } = Prioridad.Media;

    public bool CargarDatosDemo { get; set; }
}
