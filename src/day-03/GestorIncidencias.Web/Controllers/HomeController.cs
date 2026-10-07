using System.Diagnostics;
using GestorIncidencias.Application.Configuracion;
using GestorIncidencias.Application.Incidencias;
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
        // Día 2: se cargaban TODAS las incidencias y se contaban en memoria (servicio.ListarAsync() + Count(...)).
        // Día 3: las cifras se calculan en la base de datos (COUNT / GROUP BY). Ver capítulo 6.
        var resumen = await servicio.ObtenerResumenAsync(ct);
        return View(new PanelViewModel(opciones.Value.NombreAplicacion, resumen));
    }

    // GET /Home/Error  (la usa UseExceptionHandler fuera de Development)
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel(Activity.Current?.Id ?? HttpContext.TraceIdentifier));
}
