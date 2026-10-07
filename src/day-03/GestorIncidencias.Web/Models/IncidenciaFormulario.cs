using System.ComponentModel.DataAnnotations;
using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Domain.Incidencias;

namespace GestorIncidencias.Web.Models;

/// <summary>
/// ViewModel del formulario "Nueva incidencia". Lo usan la vista MVC y la Razor Page.
///
/// Los atributos sirven para la INTERFAZ: etiquetas ([Display]) y mensajes de error
/// inmediatos (ModelState). La regla de verdad sigue estando en Incidencia.Crear():
/// si alguien llama al servicio sin pasar por aquí, el dominio también la comprueba.
/// </summary>
public class IncidenciaFormulario
{
    [Display(Name = "Título")]
    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(Incidencia.TituloLongitudMaxima, MinimumLength = Incidencia.TituloLongitudMinima,
        ErrorMessage = "El título debe tener entre {2} y {1} caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [Display(Name = "Descripción")]
    [StringLength(Incidencia.DescripcionLongitudMaxima, ErrorMessage = "La descripción no puede superar {1} caracteres.")]
    [DataType(DataType.MultilineText)]
    public string? Descripcion { get; set; }

    [Display(Name = "Prioridad")]
    public Prioridad? Prioridad { get; set; }

    [Display(Name = "Categoría")]
    public int? CategoriaId { get; set; }

    /// <summary>Traduce el ViewModel (Web) al comando del caso de uso (Application).</summary>
    public CrearIncidenciaComando AComando() => new(Titulo, Descripcion, Prioridad, CategoriaId);
}

/// <summary>Formulario "Añadir comentario" de la ficha de detalle.</summary>
public class ComentarioFormulario
{
    [Required(ErrorMessage = "El comentario es obligatorio.")]
    [StringLength(Comentario.TextoLongitudMaxima, ErrorMessage = "El comentario no puede superar {1} caracteres.")]
    public string Texto { get; set; } = string.Empty;

    // Hasta el día 4 (Identity) no sabemos quién es el usuario: de momento se escribe a mano.
    [Required(ErrorMessage = "El autor es obligatorio.")]
    [StringLength(Comentario.AutorLongitudMaxima, ErrorMessage = "El autor no puede superar {1} caracteres.")]
    public string Autor { get; set; } = string.Empty;

    public ComentarIncidenciaComando AComando() => new(Texto, Autor);
}
