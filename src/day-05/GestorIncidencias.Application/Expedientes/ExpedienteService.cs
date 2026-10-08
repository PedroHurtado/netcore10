using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Expedientes;
using Microsoft.Extensions.Logging;

namespace GestorIncidencias.Application.Expedientes;

/// <summary>
/// Caso práctico SIREI (día 5). Mismo patrón que IncidenciaService:
///   - LEER: consultas que devuelven DTOs proyectados.
///   - ESCRIBIR: cargar el agregado → pedirle el cambio → guardar.
///
/// Compárese con ExpedienteDetalle.aspx.cs (docs/day-05/legacy/sirei):
///   Session["Usuario"] == null → Response.Redirect   ⇒ FallbackPolicy (nadie llega aquí sin sesión)
///   "SELECT * ... WHERE Id = " + id                  ⇒ repositorio con EF Core (consulta parametrizada)
///   Session["ExpedienteActual"] = ds                 ⇒ se vuelve a cargar en cada petición
///   if (ddlEstado.SelectedValue == "4" && ...)       ⇒ expediente.Actualizar(...) (dominio)
///   DateTime.Now                                     ⇒ reloj.GetUtcNow() (TimeProvider)
///   "último que guarda gana"                         ⇒ comprobación de Version (concurrencia optimista)
/// </summary>
public class ExpedienteService(
    IExpedienteRepository repositorio,
    IExpedienteConsultas consultas,
    IUsuarioActual usuario,
    TimeProvider reloj,
    ILogger<ExpedienteService> logger) : IExpedienteService
{
    // ------------------------------------------------------------------ Lecturas

    public Task<Pagina<ExpedienteDto>> ListarAsync(
        string? texto = null, EstadoExpediente? estado = null, int pagina = 1, int tamanoPagina = Pagina<ExpedienteDto>.TamanoPorDefecto,
        CancellationToken ct = default)
    {
        (pagina, tamanoPagina) = Pagina<ExpedienteDto>.Normalizar(pagina, tamanoPagina);
        texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        return consultas.ListarAsync(texto, estado, pagina, tamanoPagina, ct);
    }

    public Task<ExpedienteDetalleDto?> ObtenerAsync(int id, CancellationToken ct = default) =>
        consultas.ObtenerDetalleAsync(id, ct);

    // ------------------------------------------------------------------ Escrituras

    public async Task<Resultado> ActualizarAsync(int id, ActualizarExpedienteComando comando, CancellationToken ct = default)
    {
        var expediente = await repositorio.ObtenerPorIdAsync(id, ct);
        if (expediente is null)
            return Resultado.Fallo($"No existe el expediente {id}.", TipoError.NoEncontrado);

        // CONCURRENCIA OPTIMISTA: el formulario trae la versión que el usuario tenía delante.
        // Si alguien ha guardado entretanto, no se pisa su trabajo: se avisa y se recarga.
        if (expediente.Version != comando.Version)
        {
            logger.ConflictoConcurrencia(expediente.Numero, comando.Version, expediente.Version);
            return Resultado.Fallo(
                "Otra persona ha modificado el expediente mientras lo editabas. Se muestran los datos actuales: revisa y vuelve a guardar.",
                TipoError.Conflicto);
        }

        var resultado = expediente.Actualizar(comando.Observaciones, comando.Estado, reloj.GetUtcNow());
        if (!resultado.Exito)
        {
            logger.OperacionRechazada(id, resultado.Error!);
            return resultado;
        }

        await repositorio.GuardarCambiosAsync(ct);
        logger.ExpedienteActualizado(expediente.Numero, expediente.Estado, expediente.Version, usuario.Nombre);
        return resultado;
    }

    public Task<Resultado> AgregarTramiteAsync(int id, string descripcion, CancellationToken ct = default) =>
        ModificarAsync(id, expediente =>
        {
            var tramite = expediente.AgregarTramite(descripcion, reloj.GetUtcNow());
            return tramite.Exito ? Resultado.Ok() : Resultado.Fallo(tramite.Error!, tramite.Tipo!.Value);
        }, "trámite añadido", ct);

    public Task<Resultado> CompletarTramiteAsync(int id, int tramiteId, CancellationToken ct = default) =>
        ModificarAsync(id, expediente => expediente.CompletarTramite(tramiteId, reloj.GetUtcNow()), $"trámite {tramiteId} completado", ct);

    private async Task<Resultado> ModificarAsync(int id, Func<Expediente, Resultado> cambio, string operacion, CancellationToken ct)
    {
        var expediente = await repositorio.ObtenerPorIdAsync(id, ct);
        if (expediente is null)
            return Resultado.Fallo($"No existe el expediente {id}.", TipoError.NoEncontrado);

        var resultado = cambio(expediente);
        if (!resultado.Exito)
        {
            logger.OperacionRechazada(id, resultado.Error!);
            return resultado;
        }

        await repositorio.GuardarCambiosAsync(ct);
        logger.TramitesModificados(expediente.Numero, operacion, usuario.Nombre);
        return resultado;
    }
}
