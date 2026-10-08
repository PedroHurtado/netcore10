using GestorIncidencias.Application.Comun;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Expedientes;

namespace GestorIncidencias.Application.Expedientes;

/// <summary>
/// Caso práctico SIREI (día 5): casos de uso de los expedientes.
/// Sustituyen al código de Page_Load, btnBuscar_Click, btnGuardar_Click y gvTramites_RowCommand.
/// </summary>
public interface IExpedienteService
{
    Task<Pagina<ExpedienteDto>> ListarAsync(string? texto = null, EstadoExpediente? estado = null, int pagina = 1, int tamanoPagina = Pagina<ExpedienteDto>.TamanoPorDefecto, CancellationToken ct = default);
    Task<ExpedienteDetalleDto?> ObtenerAsync(int id, CancellationToken ct = default);

    Task<Resultado> ActualizarAsync(int id, ActualizarExpedienteComando comando, CancellationToken ct = default);
    Task<Resultado> AgregarTramiteAsync(int id, string descripcion, CancellationToken ct = default);
    Task<Resultado> CompletarTramiteAsync(int id, int tramiteId, CancellationToken ct = default);
}
