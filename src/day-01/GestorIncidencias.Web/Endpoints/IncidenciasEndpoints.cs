using System.ComponentModel.DataAnnotations;
using GestorIncidencias.Web.Models;
using GestorIncidencias.Web.Services;

namespace GestorIncidencias.Web.Endpoints;

/// <summary>
/// API REST de incidencias con Minimal APIs.
/// Agrupamos los endpoints en un método de extensión para no llenar Program.cs.
/// El día 2 veremos la misma API implementada con controladores MVC.
/// </summary>
public static class IncidenciasEndpoints
{
    public static RouteGroupBuilder MapIncidencias(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/incidencias")
                       .WithTags("Incidencias");

        // GET /api/incidencias
        grupo.MapGet("/", async (IIncidenciaService servicio, CancellationToken ct) =>
            (await servicio.ListarAsync(ct)).Select(IncidenciaResponse.Desde));

        // GET /api/incidencias/5
        grupo.MapGet("/{id:int}", async (int id, IIncidenciaService servicio, CancellationToken ct) =>
            await servicio.ObtenerAsync(id, ct) is { } incidencia
                ? Results.Ok(IncidenciaResponse.Desde(incidencia))
                : Results.NotFound())
            .WithName("ObtenerIncidencia");

        // POST /api/incidencias
        grupo.MapPost("/", async (CrearIncidenciaRequest request, IIncidenciaService servicio, CancellationToken ct) =>
        {
            // Validación manual con DataAnnotations (el día 2 la automatizaremos).
            var errores = Validar(request);
            if (errores.Count > 0)
                return Results.ValidationProblem(errores);

            var resultado = await servicio.CrearAsync(request, ct);
            return resultado.Exito
                ? Results.CreatedAtRoute("ObtenerIncidencia",
                    new { id = resultado.Valor!.Id },
                    IncidenciaResponse.Desde(resultado.Valor))
                : Results.Problem(resultado.Error, statusCode: StatusCodes.Status409Conflict);
        });

        // POST /api/incidencias/5/resolver
        grupo.MapPost("/{id:int}/resolver", async (int id, IIncidenciaService servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.ResolverAsync(id, ct);
            return resultado.Exito
                ? Results.Ok(IncidenciaResponse.Desde(resultado.Valor!))
                : Results.Problem(resultado.Error, statusCode: StatusCodes.Status400BadRequest);
        });

        return grupo;
    }

    private static Dictionary<string, string[]> Validar(object modelo)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), resultados, validateAllProperties: true);

        return resultados
            .SelectMany(r => r.MemberNames.DefaultIfEmpty(string.Empty), (r, campo) => (campo, r.ErrorMessage ?? "Valor no válido"))
            .GroupBy(x => x.campo)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Item2).ToArray());
    }
}
