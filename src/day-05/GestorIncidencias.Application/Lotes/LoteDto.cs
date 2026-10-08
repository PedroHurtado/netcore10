using GestorIncidencias.Domain.Lotes;

namespace GestorIncidencias.Application.Lotes;

/// <summary>
/// Una fila de la tabla de lotes. En Noticom la tabla tenía 15 columnas y el JavaScript ocultaba unas u otras
/// "por número" (ocultarColumnaGridview(12, 'gridLotes')) según la pantalla. Aquí el DTO lleva los datos
/// y la vista decide qué columnas pinta: si mañana se añade una columna, nada se descoloca.
/// </summary>
public record LoteDto(
    int Id,
    int Proceso,
    int Ejercicio,
    string Descripcion,
    TipoNotificacion Tipo,
    int NumeroNotificaciones,
    EstadoLote Estado,
    DateTimeOffset FechaAlta,
    DateTimeOffset? FechaValidacion,
    string? Remesa);

public record RemesaDto(int Id, string Nombre, DateTimeOffset FechaCreacion, int NumeroLotes, int NumeroNotificaciones);

/// <summary>
/// Filtros de la búsqueda (normal y avanzada). En el legacy viajaban como seis parámetros sueltos de
/// consultaLotes(_proceso, _ejercicio, _estado, _pago, _tipo, _remesa) y se guardaban en sessionStorage.
/// Aquí son un objeto que el model binding rellena desde la query string: la búsqueda está en la URL.
/// </summary>
public record FiltroLotes(
    int? Proceso = null,
    int? Ejercicio = null,
    EstadoLote? Estado = null,
    TipoNotificacion? Tipo = null,
    string? Remesa = null)
{
    public bool EsAvanzado => Estado is not null || Tipo is not null || !string.IsNullOrWhiteSpace(Remesa);
}

/// <summary>Datos de entrada de "Crear remesa": el nombre y los lotes marcados.</summary>
public record CrearRemesaComando(string Nombre, IReadOnlyList<int> LoteIds);
