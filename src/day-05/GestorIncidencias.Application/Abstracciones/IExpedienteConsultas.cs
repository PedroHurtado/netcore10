using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Expedientes;
using GestorIncidencias.Domain.Expedientes;

namespace GestorIncidencias.Application.Abstracciones;

/// <summary>
/// Caso práctico SIREI (día 5): puerto para LEER expedientes. Devuelve DTOs proyectados y páginas,
/// nunca "SELECT *" ni la tabla entera (el GridView del legacy paginaba en memoria).
/// </summary>
public interface IExpedienteConsultas
{
    Task<Pagina<ExpedienteDto>> ListarAsync(string? texto, EstadoExpediente? estado, int pagina, int tamanoPagina, CancellationToken ct = default);
    Task<ExpedienteDetalleDto?> ObtenerDetalleAsync(int id, CancellationToken ct = default);
}
