using System.ComponentModel.DataAnnotations;
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Expedientes;
using GestorIncidencias.Domain.Expedientes;

namespace GestorIncidencias.Web.Areas.Sirei.Models;

/// <summary>
/// Datos de Views/Expedientes/Index.cshtml. Lo que en BuscarExpedientes.aspx guardaba el ViewState
/// (texto buscado, estado elegido, página del GridView) viaja aquí desde la query string.
/// </summary>
public record ListadoExpedientesViewModel(Pagina<ExpedienteDto> Pagina, string? FiltroTexto, EstadoExpediente? FiltroEstado);

/// <summary>
/// El formulario de ExpedienteDetalle.aspx: txtObservaciones, ddlEstado... y la versión que se está editando.
/// Version va en un campo oculto: es la "foto" que permite detectar si otra persona ha guardado entretanto.
/// (En Web Forms el id iba en ViewState["IdExpediente"]; aquí va en la ruta: /Sirei/Expedientes/Detalle/5.)
/// </summary>
public class ExpedienteFormulario
{
    [Display(Name = "Estado")]
    [Required(ErrorMessage = "El estado es obligatorio.")]
    public EstadoExpediente? Estado { get; set; }

    [Display(Name = "Observaciones")]
    [StringLength(Expediente.ObservacionesLongitudMaxima, ErrorMessage = "Las observaciones no pueden superar {1} caracteres.")]
    [DataType(DataType.MultilineText)]
    public string? Observaciones { get; set; }

    public int Version { get; set; }

    public static ExpedienteFormulario Desde(ExpedienteDetalleDto expediente) => new()
    {
        Estado = expediente.Estado,
        Observaciones = expediente.Observaciones,
        Version = expediente.Version
    };

    public ActualizarExpedienteComando AComando() => new(Observaciones, Estado!.Value, Version);
}

/// <summary>
/// La ficha: los datos actuales del expediente (solo lectura) y el formulario (lo que el usuario está editando).
/// Son dos cosas distintas: si el POST falla, se vuelven a pintar los datos de la base de datos con lo que el usuario escribió.
/// </summary>
public record FichaExpedienteViewModel(ExpedienteDetalleDto Expediente, ExpedienteFormulario Formulario);
