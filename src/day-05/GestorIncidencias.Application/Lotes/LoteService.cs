using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Lotes;
using Microsoft.Extensions.Logging;

namespace GestorIncidencias.Application.Lotes;

/// <summary>
/// Caso práctico Noticom (día 5). Compárese con LoteController + LoteService de MVC 5 (docs/day-05/legacy/noticom):
///   new NoticomEntities() en cada método            ⇒ repositorio y consultas inyectados (DbContext por petición)
///   UPDATE sin comprobar el estado del lote          ⇒ lote.Validar() / Remesa.Crear(...) aplican las reglas
///   la vista decide qué lotes se pueden borrar       ⇒ lote.ComprobarBorrado() en el servidor
///   ToList() y después filtrar en memoria            ⇒ filtros en el IQueryable + paginación en SQL
/// </summary>
public class LoteService(
    ILoteRepository repositorio,
    ILoteConsultas consultas,
    IUsuarioActual usuario,
    TimeProvider reloj,
    ILogger<LoteService> logger) : ILoteService
{
    // ------------------------------------------------------------------ Lecturas

    public Task<Pagina<LoteDto>> ListarAsync(
        FiltroLotes filtro, int pagina = 1, int tamanoPagina = Pagina<LoteDto>.TamanoPorDefecto, CancellationToken ct = default)
    {
        (pagina, tamanoPagina) = Pagina<LoteDto>.Normalizar(pagina, tamanoPagina);
        var remesa = string.IsNullOrWhiteSpace(filtro.Remesa) ? null : filtro.Remesa.Trim();
        return consultas.ListarAsync(filtro with { Remesa = remesa }, pagina, tamanoPagina, ct);
    }

    public Task<IReadOnlyList<RemesaDto>> ListarRemesasAsync(CancellationToken ct = default) =>
        consultas.ListarRemesasAsync(ct);

    // ------------------------------------------------------------------ Escrituras

    public async Task<Resultado> ValidarAsync(int id, CancellationToken ct = default)
    {
        var lote = await repositorio.ObtenerPorIdAsync(id, ct);
        if (lote is null)
            return Resultado.Fallo($"No existe el lote {id}.", TipoError.NoEncontrado);

        var resultado = lote.Validar(reloj.GetUtcNow());
        if (!resultado.Exito)
            return Rechazar(resultado);

        await repositorio.GuardarCambiosAsync(ct);
        logger.LoteValidado(id, usuario.Nombre);
        return resultado;
    }

    public async Task<Resultado> BorrarAsync(int id, CancellationToken ct = default)
    {
        var lote = await repositorio.ObtenerPorIdAsync(id, ct);
        if (lote is null)
            return Resultado.Fallo($"No existe el lote {id}.", TipoError.NoEncontrado);

        var resultado = lote.ComprobarBorrado();
        if (!resultado.Exito)
            return Rechazar(resultado);

        repositorio.Borrar(lote);
        await repositorio.GuardarCambiosAsync(ct);
        logger.LoteBorrado(id, usuario.Nombre);
        return resultado;
    }

    public async Task<Resultado<RemesaDto>> CrearRemesaAsync(CrearRemesaComando comando, CancellationToken ct = default)
    {
        var ids = comando.LoteIds.Distinct().ToList();
        var lotes = await repositorio.ObtenerVariosAsync(ids, ct);

        // Regla de APLICACIÓN: todos los lotes marcados tienen que existir (el dominio no puede consultar la base de datos).
        var inexistentes = ids.Except(lotes.Select(l => l.Id)).ToList();
        if (inexistentes.Count > 0)
            return RechazarRemesa(Resultado<Remesa>.Fallo($"No existen los lotes: {string.Join(", ", inexistentes)}.", TipoError.NoEncontrado));

        // Reglas de DOMINIO (nombre, al menos un lote, solo lotes validados y sin remesa): las aplica Remesa.Crear.
        var creacion = Remesa.Crear(comando.Nombre, lotes, reloj.GetUtcNow());
        if (!creacion.Exito)
            return RechazarRemesa(creacion);

        var remesa = creacion.Valor!;
        repositorio.AgregarRemesa(remesa);
        await repositorio.GuardarCambiosAsync(ct);   // INSERT de la remesa + UPDATE de sus lotes, en UNA transacción
        logger.RemesaCreada(remesa.Id, remesa.Nombre, lotes.Count, usuario.Nombre);

        return await consultas.ObtenerRemesaAsync(remesa.Id, ct) is { } dto
            ? Resultado<RemesaDto>.Ok(dto)
            : Resultado<RemesaDto>.Fallo($"No existe la remesa {remesa.Id}.", TipoError.NoEncontrado);
    }

    private Resultado Rechazar(Resultado resultado)
    {
        logger.OperacionRechazada(resultado.Error!);
        return resultado;
    }

    private Resultado<RemesaDto> RechazarRemesa(Resultado<Remesa> resultado)
    {
        logger.OperacionRechazada(resultado.Error!);
        return Resultado<RemesaDto>.Fallo(resultado.Error!, resultado.Tipo!.Value);
    }
}
