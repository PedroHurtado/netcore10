using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Configuracion;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Incidencias;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestorIncidencias.Application.Incidencias;

/// <summary>
/// Implementación de los casos de uso. Orquesta: carga la entidad, le pide
/// que haga el cambio (las reglas están en la entidad) y guarda.
/// No sabe si hay una base de datos, ni si quien la llama es una página web o una API.
/// </summary>
public class IncidenciaService(
    IIncidenciaRepository repositorio,
    TimeProvider reloj,
    IOptionsSnapshot<IncidenciasOptions> opciones,
    ILogger<IncidenciaService> logger) : IIncidenciaService
{
    private readonly IncidenciasOptions _opciones = opciones.Value;

    public async Task<IReadOnlyList<IncidenciaDto>> ListarAsync(EstadoIncidencia? estado = null, CancellationToken ct = default) =>
        (await repositorio.ObtenerTodasAsync(estado, ct)).Select(IncidenciaDto.Desde).ToList();

    public async Task<IncidenciaDto?> ObtenerAsync(int id, CancellationToken ct = default) =>
        await repositorio.ObtenerPorIdAsync(id, ct) is { } incidencia ? IncidenciaDto.Desde(incidencia) : null;

    public async Task<Resultado<IncidenciaDto>> CrearAsync(CrearIncidenciaComando comando, CancellationToken ct = default)
    {
        // Regla de APLICACIÓN: depende de la configuración y del resto de incidencias, no de una sola entidad.
        if (await repositorio.ContarAbiertasAsync(ct) >= _opciones.MaxIncidenciasAbiertas)
        {
            logger.LogWarning("Límite de incidencias abiertas alcanzado ({Max})", _opciones.MaxIncidenciasAbiertas);
            return Resultado<IncidenciaDto>.Fallo(
                $"No se pueden abrir más de {_opciones.MaxIncidenciasAbiertas} incidencias a la vez.", TipoError.Conflicto);
        }

        // Reglas de DOMINIO (título, descripción...): las comprueba la propia entidad.
        var creacion = Incidencia.Crear(
            comando.Titulo, comando.Descripcion, comando.Prioridad ?? _opciones.PrioridadPorDefecto, reloj.GetUtcNow());

        if (!creacion.Exito)
            return Resultado<IncidenciaDto>.Fallo(creacion.Error!, creacion.Tipo!.Value);

        var incidencia = creacion.Valor!;
        await repositorio.AgregarAsync(incidencia, ct);
        logger.LogInformation("Incidencia {Id} creada con prioridad {Prioridad}", incidencia.Id, incidencia.Prioridad);

        return Resultado<IncidenciaDto>.Ok(IncidenciaDto.Desde(incidencia));
    }

    public Task<Resultado<IncidenciaDto>> IniciarAsync(int id, CancellationToken ct = default) =>
        CambiarEstadoAsync(id, incidencia => incidencia.Iniciar(), ct);

    public Task<Resultado<IncidenciaDto>> ResolverAsync(int id, CancellationToken ct = default) =>
        CambiarEstadoAsync(id, incidencia => incidencia.Resolver(reloj.GetUtcNow()), ct);

    public Task<Resultado<IncidenciaDto>> CerrarAsync(int id, CancellationToken ct = default) =>
        CambiarEstadoAsync(id, incidencia => incidencia.Cerrar(), ct);

    /// <summary>
    /// Patrón común a todos los cambios de estado: cargar → pedir el cambio a la entidad → guardar.
    /// </summary>
    private async Task<Resultado<IncidenciaDto>> CambiarEstadoAsync(
        int id, Func<Incidencia, Resultado> cambio, CancellationToken ct)
    {
        var incidencia = await repositorio.ObtenerPorIdAsync(id, ct);
        if (incidencia is null)
            return Resultado<IncidenciaDto>.Fallo($"No existe la incidencia {id}.", TipoError.NoEncontrado);

        var resultado = cambio(incidencia);
        if (!resultado.Exito)
            return Resultado<IncidenciaDto>.Fallo(resultado.Error!, resultado.Tipo!.Value);

        await repositorio.GuardarCambiosAsync(ct);
        logger.LogInformation("Incidencia {Id} pasa a {Estado}", id, incidencia.Estado);

        return Resultado<IncidenciaDto>.Ok(IncidenciaDto.Desde(incidencia));
    }
}
