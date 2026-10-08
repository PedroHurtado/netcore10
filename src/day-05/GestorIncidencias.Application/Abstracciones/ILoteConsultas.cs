using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Lotes;

namespace GestorIncidencias.Application.Abstracciones;

/// <summary>Caso práctico Noticom (día 5): puerto para LEER lotes y remesas (DTOs proyectados, paginados).</summary>
public interface ILoteConsultas
{
    Task<Pagina<LoteDto>> ListarAsync(FiltroLotes filtro, int pagina, int tamanoPagina, CancellationToken ct = default);
    Task<RemesaDto?> ObtenerRemesaAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<RemesaDto>> ListarRemesasAsync(CancellationToken ct = default);
}
