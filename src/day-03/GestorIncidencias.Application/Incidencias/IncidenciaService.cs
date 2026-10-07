using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Configuracion;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Incidencias;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestorIncidencias.Application.Incidencias;

/// <summary>
/// Implementación de los casos de uso.
///   - ESCRIBIR: carga la entidad con el repositorio, le pide que haga el cambio (las reglas están
///     en la entidad), guarda y devuelve el DTO leído con las consultas.
///   - LEER: delega en IIncidenciaConsultas, que devuelve DTOs ya proyectados.
/// No sabe si hay una base de datos, ni si quien la llama es una página web o una API.
/// </summary>
public class IncidenciaService(
    IIncidenciaRepository repositorio,
    IIncidenciaConsultas consultas,
    TimeProvider reloj,
    IOptionsSnapshot<IncidenciasOptions> opciones,
    ILogger<IncidenciaService> logger) : IIncidenciaService
{
    private readonly IncidenciasOptions _opciones = opciones.Value;

    // ------------------------------------------------------------------ Lecturas

    public Task<Pagina<IncidenciaDto>> ListarAsync(
        EstadoIncidencia? estado = null, int pagina = 1, int tamanoPagina = Pagina<IncidenciaDto>.TamanoPorDefecto, CancellationToken ct = default)
    {
        (pagina, tamanoPagina) = Pagina<IncidenciaDto>.Normalizar(pagina, tamanoPagina);
        return consultas.ListarAsync(estado, pagina, tamanoPagina, ct);
    }

    public Task<IncidenciaDetalleDto?> ObtenerAsync(int id, CancellationToken ct = default) =>
        consultas.ObtenerDetalleAsync(id, ct);

    public Task<ResumenIncidenciasDto> ObtenerResumenAsync(CancellationToken ct = default) =>
        consultas.ObtenerResumenAsync(ct);

    public Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct = default) =>
        consultas.ListarCategoriasAsync(ct);

    // ------------------------------------------------------------------ Escrituras

    public async Task<Resultado<IncidenciaDto>> CrearAsync(CrearIncidenciaComando comando, CancellationToken ct = default)
    {
        // Regla de APLICACIÓN: depende de la configuración y del resto de incidencias, no de una sola entidad.
        if (await repositorio.ContarAbiertasAsync(ct) >= _opciones.MaxIncidenciasAbiertas)
        {
            logger.LogWarning("Límite de incidencias abiertas alcanzado ({Max})", _opciones.MaxIncidenciasAbiertas);
            return Resultado<IncidenciaDto>.Fallo(
                $"No se pueden abrir más de {_opciones.MaxIncidenciasAbiertas} incidencias a la vez.", TipoError.Conflicto);
        }

        // Regla de APLICACIÓN: la categoría tiene que existir (el dominio no puede consultar la base de datos).
        if (comando.CategoriaId is { } categoriaId && !await repositorio.ExisteCategoriaAsync(categoriaId, ct))
            return Resultado<IncidenciaDto>.Fallo($"No existe la categoría {categoriaId}.");

        // Reglas de DOMINIO (título, descripción...): las comprueba la propia entidad.
        var creacion = Incidencia.Crear(
            comando.Titulo, comando.Descripcion, comando.Prioridad ?? _opciones.PrioridadPorDefecto, reloj.GetUtcNow(), comando.CategoriaId);

        if (!creacion.Exito)
            return Resultado<IncidenciaDto>.Fallo(creacion.Error!, creacion.Tipo!.Value);

        var incidencia = creacion.Valor!;
        await repositorio.AgregarAsync(incidencia, ct);
        logger.LogInformation("Incidencia {Id} creada con prioridad {Prioridad}", incidencia.Id, incidencia.Prioridad);

        return await LeerTrasGuardarAsync(incidencia.Id, ct);
    }

    public Task<Resultado<IncidenciaDto>> IniciarAsync(int id, CancellationToken ct = default) =>
        ModificarAsync(id, incidencia => incidencia.Iniciar(), ct);

    public Task<Resultado<IncidenciaDto>> ResolverAsync(int id, CancellationToken ct = default) =>
        ModificarAsync(id, incidencia => incidencia.Resolver(reloj.GetUtcNow()), ct);

    public Task<Resultado<IncidenciaDto>> CerrarAsync(int id, CancellationToken ct = default) =>
        ModificarAsync(id, incidencia => incidencia.Cerrar(), ct);

    public Task<Resultado<IncidenciaDto>> ReabrirAsync(int id, CancellationToken ct = default) =>
        ModificarAsync(id, incidencia => incidencia.Reabrir(), ct);

    public Task<Resultado<IncidenciaDto>> ComentarAsync(int id, ComentarIncidenciaComando comando, CancellationToken ct = default) =>
        ModificarAsync(id, incidencia =>
        {
            var comentario = incidencia.AgregarComentario(comando.Texto, comando.Autor, reloj.GetUtcNow());
            return comentario.Exito ? Resultado.Ok() : Resultado.Fallo(comentario.Error!, comentario.Tipo!.Value);
        }, ct);

    /// <summary>
    /// Patrón común a todos los cambios: cargar → pedir el cambio a la entidad → guardar → leer el DTO.
    /// </summary>
    private async Task<Resultado<IncidenciaDto>> ModificarAsync(
        int id, Func<Incidencia, Resultado> cambio, CancellationToken ct)
    {
        var incidencia = await repositorio.ObtenerPorIdAsync(id, ct);
        if (incidencia is null)
            return Resultado<IncidenciaDto>.Fallo($"No existe la incidencia {id}.", TipoError.NoEncontrado);

        var resultado = cambio(incidencia);
        if (!resultado.Exito)
            return Resultado<IncidenciaDto>.Fallo(resultado.Error!, resultado.Tipo!.Value);

        await repositorio.GuardarCambiosAsync(ct);
        logger.LogInformation("Incidencia {Id} modificada (estado {Estado})", id, incidencia.Estado);

        return await LeerTrasGuardarAsync(id, ct);
    }

    /// <summary>
    /// El DTO lleva datos que la entidad no tiene cargados (nombre de la categoría, número de comentarios):
    /// se lee con la misma consulta que usan las pantallas. Una consulta más, pero un único sitio que construye el DTO.
    /// </summary>
    private async Task<Resultado<IncidenciaDto>> LeerTrasGuardarAsync(int id, CancellationToken ct) =>
        await consultas.ObtenerAsync(id, ct) is { } dto
            ? Resultado<IncidenciaDto>.Ok(dto)
            : Resultado<IncidenciaDto>.Fallo($"No existe la incidencia {id}.", TipoError.NoEncontrado);
}
