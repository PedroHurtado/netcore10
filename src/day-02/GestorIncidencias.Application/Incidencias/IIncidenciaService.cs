using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Incidencias;

namespace GestorIncidencias.Application.Incidencias;

/// <summary>
/// Casos de uso de las incidencias. Es lo que consumen los controladores MVC,
/// las Razor Pages y la API: los tres comparten exactamente la misma lógica.
/// </summary>
public interface IIncidenciaService
{
    Task<IReadOnlyList<IncidenciaDto>> ListarAsync(EstadoIncidencia? estado = null, CancellationToken ct = default);
    Task<IncidenciaDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<Resultado<IncidenciaDto>> CrearAsync(CrearIncidenciaComando comando, CancellationToken ct = default);
    Task<Resultado<IncidenciaDto>> IniciarAsync(int id, CancellationToken ct = default);
    Task<Resultado<IncidenciaDto>> ResolverAsync(int id, CancellationToken ct = default);
    Task<Resultado<IncidenciaDto>> CerrarAsync(int id, CancellationToken ct = default);
}
