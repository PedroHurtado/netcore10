using GestorIncidencias.Application.Comun;
using GestorIncidencias.Domain.Comun;

namespace GestorIncidencias.Application.Lotes;

/// <summary>
/// Caso práctico Noticom (día 5): casos de uso de lotes y remesas.
/// Sustituyen a las acciones de LoteController (MVC 5) que llamaban directamente a LoteService con EF6.
/// </summary>
public interface ILoteService
{
    Task<Pagina<LoteDto>> ListarAsync(FiltroLotes filtro, int pagina = 1, int tamanoPagina = Pagina<LoteDto>.TamanoPorDefecto, CancellationToken ct = default);
    Task<IReadOnlyList<RemesaDto>> ListarRemesasAsync(CancellationToken ct = default);

    Task<Resultado> ValidarAsync(int id, CancellationToken ct = default);
    Task<Resultado> BorrarAsync(int id, CancellationToken ct = default);
    Task<Resultado<RemesaDto>> CrearRemesaAsync(CrearRemesaComando comando, CancellationToken ct = default);
}
