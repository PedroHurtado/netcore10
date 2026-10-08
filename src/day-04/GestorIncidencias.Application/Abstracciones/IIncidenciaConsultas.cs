using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Domain.Incidencias;

namespace GestorIncidencias.Application.Abstracciones;

/// <summary>
/// "Puerto" de salida para LEER. Devuelve DTOs, nunca entidades:
/// la implementación (Infrastructure) puede pedir a la base de datos SOLO las columnas necesarias
/// (proyección con Select), sin seguimiento de cambios y con los recuentos hechos en SQL.
///
/// Es la versión "ligera" de CQRS: un camino para escribir (repositorio + entidad)
/// y otro para leer (consultas + DTO), sobre la misma base de datos.
/// </summary>
public interface IIncidenciaConsultas
{
    Task<Pagina<IncidenciaDto>> ListarAsync(EstadoIncidencia? estado, int pagina, int tamanoPagina, CancellationToken ct = default);
    Task<IncidenciaDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<IncidenciaDetalleDto?> ObtenerDetalleAsync(int id, CancellationToken ct = default);
    Task<ResumenIncidenciasDto> ObtenerResumenAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ResumenPrioridadDto>> ResumenPorPrioridadAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct = default);
}
