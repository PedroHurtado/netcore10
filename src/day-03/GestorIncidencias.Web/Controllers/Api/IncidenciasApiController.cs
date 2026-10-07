using System.ComponentModel.DataAnnotations;
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Incidencias;
using Microsoft.AspNetCore.Mvc;

namespace GestorIncidencias.Web.Controllers.Api;

/// <summary>
/// Cuerpo JSON de POST /api/incidencias.
/// OJO: en MVC los atributos de validación de un record van en el PARÁMETRO del constructor
/// (sin "property:"). Con "property:" MVC lanza InvalidOperationException al validar.
/// </summary>
public record CrearIncidenciaRequest(
    [Required, StringLength(Incidencia.TituloLongitudMaxima, MinimumLength = Incidencia.TituloLongitudMinima)] string Titulo,
    [StringLength(Incidencia.DescripcionLongitudMaxima)] string? Descripcion,
    Prioridad? Prioridad,
    int? CategoriaId);

/// <summary>Cuerpo JSON de POST /api/incidencias/5/comentarios.</summary>
public record ComentarioRequest(
    [Required, StringLength(Comentario.TextoLongitudMaxima)] string Texto,
    [Required, StringLength(Comentario.AutorLongitudMaxima)] string Autor);

/// <summary>
/// La misma API REST del día 1, ahora con un CONTROLADOR API en lugar de Minimal APIs.
///
/// [ApiController] activa:
///  - Validación automática: si el modelo no es válido devuelve 400 ValidationProblem
///    sin que escribamos "if (!ModelState.IsValid)".
///  - Inferencia de origen: los tipos complejos vienen del cuerpo ([FromBody]).
///  - Errores en formato ProblemDetails.
///
/// Hereda de ControllerBase (no de Controller) porque no necesita vistas.
/// Usa los MISMOS casos de uso que el controlador MVC y las Razor Pages.
/// </summary>
[ApiController]
[Route("api/incidencias")]
[Produces("application/json")]
public class IncidenciasApiController(IIncidenciaService servicio) : ControllerBase
{
    // GET /api/incidencias?estado=Abierta&pagina=2&tamano=50
    // Día 3: devuelve UNA PÁGINA (elementos + totales), nunca la tabla entera.
    [HttpGet]
    public Task<Pagina<IncidenciaDto>> Listar(
        [FromQuery] EstadoIncidencia? estado, [FromQuery] int pagina = 1, [FromQuery] int tamano = Pagina<IncidenciaDto>.TamanoPorDefecto,
        CancellationToken ct = default) =>
        servicio.ListarAsync(estado, pagina, tamano, ct);

    // GET /api/incidencias/5   → la incidencia y sus comentarios
    [HttpGet("{id:int}", Name = "ObtenerIncidencia")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IncidenciaDetalleDto>> Obtener(int id, CancellationToken ct)
    {
        var detalle = await servicio.ObtenerAsync(id, ct);
        return detalle is null ? NotFound() : detalle;
    }

    // GET /api/incidencias/categorias
    [HttpGet("categorias")]
    public Task<IReadOnlyList<CategoriaDto>> Categorias(CancellationToken ct) =>
        servicio.ListarCategoriasAsync(ct);

    // POST /api/incidencias
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IncidenciaDto>> Crear(CrearIncidenciaRequest request, CancellationToken ct)
    {
        // Si llegamos aquí, [ApiController] ya ha comprobado los atributos de CrearIncidenciaRequest.
        var resultado = await servicio.CrearAsync(new CrearIncidenciaComando(request.Titulo, request.Descripcion, request.Prioridad, request.CategoriaId), ct);

        return resultado.Exito
            ? CreatedAtRoute("ObtenerIncidencia", new { id = resultado.Valor!.Id }, resultado.Valor)
            : Problema(resultado);
    }

    // POST /api/incidencias/5/iniciar
    [HttpPost("{id:int}/iniciar")]
    public async Task<ActionResult<IncidenciaDto>> Iniciar(int id, CancellationToken ct) =>
        Responder(await servicio.IniciarAsync(id, ct));

    // POST /api/incidencias/5/resolver
    [HttpPost("{id:int}/resolver")]
    public async Task<ActionResult<IncidenciaDto>> Resolver(int id, CancellationToken ct) =>
        Responder(await servicio.ResolverAsync(id, ct));

    // POST /api/incidencias/5/cerrar
    [HttpPost("{id:int}/cerrar")]
    public async Task<ActionResult<IncidenciaDto>> Cerrar(int id, CancellationToken ct) =>
        Responder(await servicio.CerrarAsync(id, ct));

    // POST /api/incidencias/5/reabrir
    [HttpPost("{id:int}/reabrir")]
    public async Task<ActionResult<IncidenciaDto>> Reabrir(int id, CancellationToken ct) =>
        Responder(await servicio.ReabrirAsync(id, ct));

    // POST /api/incidencias/5/comentarios
    [HttpPost("{id:int}/comentarios")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IncidenciaDto>> Comentar(int id, ComentarioRequest request, CancellationToken ct) =>
        Responder(await servicio.ComentarAsync(id, new ComentarIncidenciaComando(request.Texto, request.Autor), ct));

    private ActionResult<IncidenciaDto> Responder(Resultado<IncidenciaDto> resultado) =>
        resultado.Exito ? Ok(resultado.Valor) : Problema(resultado);

    /// <summary>Traduce el tipo de error de negocio al código HTTP correspondiente.</summary>
    private ObjectResult Problema<T>(Resultado<T> resultado) =>
        Problem(
            detail: resultado.Error,
            statusCode: resultado.Tipo switch
            {
                TipoError.NoEncontrado => StatusCodes.Status404NotFound,
                TipoError.Conflicto => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            });
}
