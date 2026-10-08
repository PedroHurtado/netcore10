using GestorIncidencias.Domain.Expedientes;

namespace GestorIncidencias.Application.Expedientes;

/// <summary>
/// Una fila del listado de expedientes (BuscarExpedientes.aspx). Proyección: solo las columnas que se pintan,
/// y el número de trámites pendientes calculado en la base de datos (COUNT), no en memoria.
/// </summary>
public record ExpedienteDto(
    int Id,
    string Numero,
    string Titular,
    string Asunto,
    EstadoExpediente Estado,
    DateTimeOffset FechaModificacion,
    int TramitesPendientes);

public record TramiteDto(int Id, string Descripcion, DateTimeOffset FechaAlta, bool Pendiente, DateTimeOffset? FechaCompletado);

/// <summary>
/// La ficha de ExpedienteDetalle.aspx: lo que antes era un DataSet con dos DataTable guardado en Session.
/// Version viaja a la vista (campo oculto) y vuelve en el POST: es la "foto" que permite detectar
/// que otra persona ha guardado el expediente mientras tanto (concurrencia optimista).
/// </summary>
public record ExpedienteDetalleDto(
    int Id,
    string Numero,
    string Titular,
    string Asunto,
    string? Observaciones,
    EstadoExpediente Estado,
    DateTimeOffset FechaAlta,
    DateTimeOffset FechaModificacion,
    int Version,
    IReadOnlyList<TramiteDto> Tramites);

/// <summary>Datos de entrada de "Guardar" (btnGuardar_Click): observaciones, estado y la versión que se editó.</summary>
public record ActualizarExpedienteComando(string? Observaciones, EstadoExpediente Estado, int Version);
