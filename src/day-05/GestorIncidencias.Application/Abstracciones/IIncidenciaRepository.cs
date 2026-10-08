using GestorIncidencias.Domain.Incidencias;

namespace GestorIncidencias.Application.Abstracciones;

/// <summary>
/// "Puerto" de salida para ESCRIBIR: carga agregados completos (con seguimiento de cambios),
/// los modifica la entidad y se guardan. Solo lo usan los casos de uso que cambian datos.
///
/// Las lecturas para pantallas e informes NO pasan por aquí: van por IIncidenciaConsultas,
/// que devuelve DTOs directamente (proyecciones). Ver docs/day-03/04-patrones-acceso-datos.md.
/// </summary>
public interface IIncidenciaRepository
{
    /// <summary>Carga la incidencia CON sus comentarios (el agregado completo), con seguimiento.</summary>
    Task<Incidencia?> ObtenerPorIdAsync(int id, CancellationToken ct = default);

    Task<int> ContarAbiertasAsync(CancellationToken ct = default);
    Task<bool> ExisteCategoriaAsync(int categoriaId, CancellationToken ct = default);
    Task AgregarAsync(Incidencia incidencia, CancellationToken ct = default);

    /// <summary>Persiste los cambios hechos en las entidades obtenidas con este repositorio.</summary>
    Task GuardarCambiosAsync(CancellationToken ct = default);
}
