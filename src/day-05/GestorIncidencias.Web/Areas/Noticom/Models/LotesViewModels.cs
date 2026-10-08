using System.ComponentModel.DataAnnotations;
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Lotes;
using GestorIncidencias.Domain.Lotes;

namespace GestorIncidencias.Web.Areas.Noticom.Models;

/// <summary>
/// Caso práctico Noticom (día 5). Sustituye al parámetro TipoEjecucion de LoteModel, que era un string
/// con valores mágicos ("c", "v", "cr", "b") comparado en la vista, en el JavaScript y en el controlador.
/// Un enum: si se escribe mal, no compila.
/// </summary>
public enum ModoLotes
{
    Consulta,       // "c"
    Validacion,     // "v"
    CrearRemesa,    // "cr"
    Borrado         // "b"
}

/// <summary>
/// Todo lo que la vista de lotes necesita saber, ya decidido en el servidor:
///   - qué título poner (antes: cuatro if en la vista),
///   - qué columnas pintar (antes: ocultarColumnaGridview(12, 'gridLotes') en JavaScript, por posición),
///   - qué acción ofrecer en cada fila y si el usuario puede usarla.
/// </summary>
public record LotesViewModel(
    ModoLotes Modo,
    FiltroLotes Filtro,
    Pagina<LoteDto> Pagina,
    bool PuedeGestionar,
    bool PuedeBorrar,
    string UrlVolver)
{
    public string Titulo => Modo switch
    {
        ModoLotes.Validacion => "Lotes pendientes de validación",
        ModoLotes.CrearRemesa => "Crear remesas",
        ModoLotes.Borrado => "Borrar lotes",
        _ => "Consulta de lotes"
    };

    // Columnas: cada pantalla enseña las suyas (antes, la tabla tenía todas y JavaScript escondía unas cuantas).
    public bool ColumnaSeleccion => Modo == ModoLotes.CrearRemesa && PuedeGestionar;
    public bool ColumnaEstado => Modo == ModoLotes.Consulta;
    public bool ColumnaRemesa => Modo == ModoLotes.Consulta;
    public bool ColumnaValidacion => Modo is ModoLotes.Consulta or ModoLotes.CrearRemesa;
    public bool ColumnaValidar => Modo == ModoLotes.Validacion && PuedeGestionar;
    public bool ColumnaBorrar => Modo == ModoLotes.Borrado && PuedeBorrar;

    /// <summary>Valores de ruta de esta misma búsqueda en otra página (paginador). Sin Estado si el modo lo fija.</summary>
    public Dictionary<string, string?> RutaPagina(int pagina) => Rutas.De(Modo, Filtro, pagina);
}

/// <summary>Formulario "Crear remesa": el nombre y los Id de los lotes marcados (casillas name="LoteIds").</summary>
public class CrearRemesaFormulario
{
    [Display(Name = "Nombre remesa")]
    [Required(ErrorMessage = "El nombre de la remesa es obligatorio.")]
    [StringLength(Remesa.NombreLongitudMaxima, ErrorMessage = "El nombre no puede superar {1} caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public List<int> LoteIds { get; set; } = [];

    public CrearRemesaComando AComando() => new(Nombre, LoteIds);
}

/// <summary>Valores de ruta de una búsqueda de lotes. Los usa el paginador y la vuelta tras un POST.</summary>
public static class Rutas
{
    public static Dictionary<string, string?> De(ModoLotes modo, FiltroLotes filtro, int pagina)
    {
        var valores = new Dictionary<string, string?> { ["modo"] = modo.ToString() };
        if (filtro.Proceso is { } proceso) valores["proceso"] = proceso.ToString();
        if (filtro.Ejercicio is { } ejercicio) valores["ejercicio"] = ejercicio.ToString();
        if (filtro.Estado is { } estado && modo == ModoLotes.Consulta) valores["estado"] = estado.ToString();
        if (filtro.Tipo is { } tipo) valores["tipo"] = tipo.ToString();
        if (!string.IsNullOrWhiteSpace(filtro.Remesa)) valores["remesa"] = filtro.Remesa;
        if (pagina > 1) valores["pagina"] = pagina.ToString();
        return valores;
    }
}
