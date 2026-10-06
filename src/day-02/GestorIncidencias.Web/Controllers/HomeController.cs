using System.Diagnostics;
using GestorIncidencias.Application.Configuracion;
using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Domain.Incidencias;
using GestorIncidencias.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GestorIncidencias.Web.Controllers;

/// <summary>
/// Controlador de la portada. Por convención:
///   clase HomeController  → controlador "Home"
///   método Index()        → acción "Index"
///   return View(modelo)   → busca Views/Home/Index.cshtml
/// </summary>
public class HomeController(IIncidenciaService servicio, IOptions<IncidenciasOptions> opciones) : Controller
{
    // GET /   (o /Home, o /Home/Index)
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var incidencias = await servicio.ListarAsync(ct: ct);

        var modelo = new PanelViewModel(
            NombreAplicacion: opciones.Value.NombreAplicacion,
            Total: incidencias.Count,
            Abiertas: incidencias.Count(i => i.Estado == EstadoIncidencia.Abierta),
            EnCurso: incidencias.Count(i => i.Estado == EstadoIncidencia.EnCurso),
            CriticasPendientes: incidencias.Count(i =>
                i.Prioridad == Prioridad.Critica && i.Estado is EstadoIncidencia.Abierta or EstadoIncidencia.EnCurso));

        return View(modelo);
    }

    // GET /Home/Error  (la usa UseExceptionHandler fuera de Development)
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel(Activity.Current?.Id ?? HttpContext.TraceIdentifier));
}
