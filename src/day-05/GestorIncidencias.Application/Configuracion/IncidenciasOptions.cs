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

    /// <summary>
    /// Incidencias cerradas "de relleno" que se añaden a los datos de demostración,
    /// para que el listado tenga varias páginas y el panel, cifras.
    /// </summary>
    [Range(0, 10_000)]
    public int IncidenciasHistoricasDemo { get; set; }

    /// <summary>
    /// Contraseña de los usuarios de demostración (ana@, luis@ y admin@demo.local).
    /// Solo se rellena en appsettings.Development.json: en producción no hay usuarios de demostración
    /// y una contraseña nunca va en appsettings.json ni en el código.
    /// </summary>
    public string? ClaveUsuariosDemo { get; set; }
}
