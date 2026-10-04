using GestorIncidencias.Web.Models;
using GestorIncidencias.Web.Options;
using Microsoft.Extensions.Options;

namespace GestorIncidencias.Web.Services;

public interface IIncidenciaService
{
    Task<IReadOnlyList<Incidencia>> ListarAsync(CancellationToken ct = default);
    Task<Incidencia?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<Resultado<Incidencia>> CrearAsync(CrearIncidenciaRequest request, CancellationToken ct = default);
    Task<Resultado<Incidencia>> ResolverAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// Resultado simple de una operación de negocio: éxito con valor o error con mensaje.
/// Evita usar excepciones para el flujo normal.
/// </summary>
public record Resultado<T>(bool Exito, T? Valor, string? Error)
{
    public static Resultado<T> Ok(T valor) => new(true, valor, null);
    public static Resultado<T> Fallo(string error) => new(false, default, error);
}

/// <summary>
/// Lógica de negocio. Recibe TODAS sus dependencias por constructor
/// (inyección de dependencias): repositorio, reloj, opciones y logger.
/// Compárese con Web Forms, donde esta lógica solía vivir en el code-behind (Button_Click).
/// </summary>
public class IncidenciaService(
    IIncidenciaRepository repositorio,
    TimeProvider reloj,
    IOptionsSnapshot<IncidenciasOptions> opciones,
    ILogger<IncidenciaService> logger) : IIncidenciaService
{
    private readonly IncidenciasOptions _opciones = opciones.Value;

    public Task<IReadOnlyList<Incidencia>> ListarAsync(CancellationToken ct = default) =>
        repositorio.ObtenerTodasAsync(ct);

    public Task<Incidencia?> ObtenerAsync(int id, CancellationToken ct = default) =>
        repositorio.ObtenerPorIdAsync(id, ct);

    public async Task<Resultado<Incidencia>> CrearAsync(CrearIncidenciaRequest request, CancellationToken ct = default)
    {
        if (await repositorio.ContarAbiertasAsync(ct) >= _opciones.MaxIncidenciasAbiertas)
        {
            logger.LogWarning("Límite de incidencias abiertas alcanzado ({Max})", _opciones.MaxIncidenciasAbiertas);
            return Resultado<Incidencia>.Fallo(
                $"No se pueden abrir más de {_opciones.MaxIncidenciasAbiertas} incidencias a la vez.");
        }

        var incidencia = new Incidencia
        {
            Titulo = request.Titulo.Trim(),
            Descripcion = request.Descripcion,
            Prioridad = request.Prioridad ?? _opciones.PrioridadPorDefecto,
            FechaAlta = reloj.GetUtcNow()
        };

        await repositorio.AgregarAsync(incidencia, ct);
        logger.LogInformation("Incidencia {Id} creada con prioridad {Prioridad}", incidencia.Id, incidencia.Prioridad);

        return Resultado<Incidencia>.Ok(incidencia);
    }

    public async Task<Resultado<Incidencia>> ResolverAsync(int id, CancellationToken ct = default)
    {
        var incidencia = await repositorio.ObtenerPorIdAsync(id, ct);
        if (incidencia is null)
            return Resultado<Incidencia>.Fallo($"No existe la incidencia {id}.");

        if (incidencia.Estado is EstadoIncidencia.Resuelta or EstadoIncidencia.Cerrada)
            return Resultado<Incidencia>.Fallo($"La incidencia {id} ya está {incidencia.Estado}.");

        incidencia.Estado = EstadoIncidencia.Resuelta;
        incidencia.FechaResolucion = reloj.GetUtcNow();
        await repositorio.ActualizarAsync(incidencia, ct);

        logger.LogInformation("Incidencia {Id} resuelta", id);
        return Resultado<Incidencia>.Ok(incidencia);
    }
}
