using GestorIncidencias.Application.Comun;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Incidencias;

namespace GestorIncidencias.Application.Incidencias;

/// <summary>
/// Casos de uso de las incidencias. Es lo que consumen los controladores MVC,
/// las Razor Pages y la API: los tres comparten exactamente la misma lógica.
/// </summary>
public interface IIncidenciaService
{
    // Lecturas
    Task<Pagina<IncidenciaDto>> ListarAsync(EstadoIncidencia? estado = null, int pagina = 1, int tamanoPagina = Pagina<IncidenciaDto>.TamanoPorDefecto, CancellationToken ct = default);
    Task<IncidenciaDetalleDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<ResumenIncidenciasDto> ObtenerResumenAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ResumenPrioridadDto>> ResumenPorPrioridadAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct = default);

    // Escrituras
    Task<Resultado<IncidenciaDto>> CrearAsync(CrearIncidenciaComando comando, CancellationToken ct = default);
    Task<Resultado<IncidenciaDto>> IniciarAsync(int id, CancellationToken ct = default);
    Task<Resultado<IncidenciaDto>> ResolverAsync(int id, CancellationToken ct = default);
    Task<Resultado<IncidenciaDto>> CerrarAsync(int id, CancellationToken ct = default);
    Task<Resultado<IncidenciaDto>> ReabrirAsync(int id, CancellationToken ct = default);
    Task<Resultado<IncidenciaDto>> CambiarCategoriaAsync(int id, int? categoriaId, CancellationToken ct = default);
    Task<Resultado<IncidenciaDto>> ComentarAsync(int id, ComentarIncidenciaComando comando, CancellationToken ct = default);
}
