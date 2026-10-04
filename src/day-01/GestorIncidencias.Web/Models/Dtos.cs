using System.ComponentModel.DataAnnotations;

namespace GestorIncidencias.Web.Models;

/// <summary>
/// Datos de entrada para crear una incidencia (lo que envía el cliente).
/// Separamos el DTO de la entidad para no exponer el modelo interno.
/// </summary>
public record CrearIncidenciaRequest(
    [property: Required, StringLength(120, MinimumLength = 5)] string Titulo,
    [property: StringLength(2000)] string? Descripcion,
    Prioridad? Prioridad);

/// <summary>
/// Datos de salida (lo que devuelve la API).
/// </summary>
public record IncidenciaResponse(
    int Id,
    string Titulo,
    string? Descripcion,
    string Prioridad,
    string Estado,
    DateTimeOffset FechaAlta,
    DateTimeOffset? FechaResolucion)
{
    public static IncidenciaResponse Desde(Incidencia i) => new(
        i.Id, i.Titulo, i.Descripcion, i.Prioridad.ToString(), i.Estado.ToString(),
        i.FechaAlta, i.FechaResolucion);
}
