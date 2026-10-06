using GestorIncidencias.Domain.Incidencias;

namespace GestorIncidencias.Application.Incidencias;

/// <summary>
/// Datos que la capa de Aplicación devuelve hacia fuera.
/// La capa Web nunca recibe la entidad: así no puede saltarse las reglas del dominio.
/// </summary>
public record IncidenciaDto(
    int Id,
    string Titulo,
    string? Descripcion,
    Prioridad Prioridad,
    EstadoIncidencia Estado,
    DateTimeOffset FechaAlta,
    DateTimeOffset? FechaResolucion)
{
    public static IncidenciaDto Desde(Incidencia i) =>
        new(i.Id, i.Titulo, i.Descripcion, i.Prioridad, i.Estado, i.FechaAlta, i.FechaResolucion);
}

/// <summary>
/// Datos de entrada del caso de uso "crear incidencia".
/// No lleva atributos de validación de interfaz: eso es cosa de la capa Web.
/// Las reglas de verdad (longitud del título...) están en la entidad.
/// </summary>
public record CrearIncidenciaComando(string Titulo, string? Descripcion, Prioridad? Prioridad);
