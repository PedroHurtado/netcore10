using GestorIncidencias.Domain.Incidencias;

namespace GestorIncidencias.Application.Abstracciones;

/// <summary>
/// "Puerto" de salida: lo que la aplicación NECESITA del almacenamiento.
/// La interfaz se define aquí (Aplicación) y se implementa en Infraestructura.
/// Así la dependencia apunta hacia dentro: Infrastructure → Application, nunca al revés.
/// </summary>
public interface IIncidenciaRepository
{
    Task<IReadOnlyList<Incidencia>> ObtenerTodasAsync(EstadoIncidencia? estado = null, CancellationToken ct = default);
    Task<Incidencia?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<int> ContarAbiertasAsync(CancellationToken ct = default);
    Task AgregarAsync(Incidencia incidencia, CancellationToken ct = default);

    /// <summary>Persiste los cambios hechos en las entidades obtenidas con este repositorio.</summary>
    Task GuardarCambiosAsync(CancellationToken ct = default);
}
