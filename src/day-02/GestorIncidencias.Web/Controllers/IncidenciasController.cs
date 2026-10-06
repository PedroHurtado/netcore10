using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Incidencias;
using GestorIncidencias.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GestorIncidencias.Web.Controllers;

/// <summary>
/// Controlador MVC de incidencias: recibe la petición, llama al caso de uso
/// y elige qué vista mostrar o a dónde redirigir. NO contiene reglas de negocio.
///
/// Compárese con Web Forms: lo que antes era Page_Load y Button_Click
/// ahora son acciones GET y POST de un controlador.
/// </summary>
public class IncidenciasController(IIncidenciaService servicio) : Controller
{
    // GET /Incidencias
    // GET /Incidencias?estado=Abierta   ← model binding desde la query string
    public async Task<IActionResult> Index(EstadoIncidencia? estado, CancellationToken ct)
    {
        var incidencias = await servicio.ListarAsync(estado, ct);
        return View(new ListadoIncidenciasViewModel(incidencias, estado));   // Views/Incidencias/Index.cshtml
    }

    // GET /Incidencias/Detalle/5   ← "id" sale de la ruta {id?}
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        var incidencia = await servicio.ObtenerAsync(id, ct);
        if (incidencia is null)
            return NotFound();                                              // 404

        return View(incidencia);                                            // Views/Incidencias/Detalle.cshtml
    }

    // GET /Incidencias/Crear   → muestra el formulario vacío
    [HttpGet]
    public IActionResult Crear() => View(new IncidenciaFormulario());

    // POST /Incidencias/Crear  → recibe el formulario
    [HttpPost]
    [ValidateAntiForgeryToken]   // protege contra CSRF (el <form> genera el token automáticamente)
    public async Task<IActionResult> Crear(IncidenciaFormulario formulario, CancellationToken ct)
    {
        // 1) Validación de la interfaz (atributos del ViewModel)
        if (!ModelState.IsValid)
            return View(formulario);   // se vuelve a pintar el formulario con los errores

        // 2) Caso de uso (reglas de negocio)
        var resultado = await servicio.CrearAsync(formulario.AComando(), ct);
        if (!resultado.Exito)
        {
            ModelState.AddModelError(string.Empty, resultado.Error!);   // error general del formulario
            return View(formulario);
        }

        // 3) Patrón Post-Redirect-Get: tras un POST correcto, SIEMPRE redirigir.
        //    Así, si el usuario pulsa F5, no se vuelve a enviar el formulario.
        TempData["Mensaje"] = $"Incidencia {resultado.Valor!.Id} creada correctamente.";
        return RedirectToAction(nameof(Detalle), new { id = resultado.Valor.Id });
    }

    // POST /Incidencias/Iniciar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Iniciar(int id, CancellationToken ct) =>
        TrasCambioDeEstado(id, await servicio.IniciarAsync(id, ct), "Incidencia iniciada.");

    // POST /Incidencias/Resolver/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolver(int id, CancellationToken ct) =>
        TrasCambioDeEstado(id, await servicio.ResolverAsync(id, ct), "Incidencia resuelta.");

    // POST /Incidencias/Cerrar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(int id, CancellationToken ct) =>
        TrasCambioDeEstado(id, await servicio.CerrarAsync(id, ct), "Incidencia cerrada.");

    /// <summary>
    /// Común a Iniciar / Resolver / Cerrar: deja un mensaje en TempData y vuelve al detalle.
    /// TempData sobrevive a UNA redirección (se guarda en una cookie): ideal para Post-Redirect-Get.
    /// </summary>
    private IActionResult TrasCambioDeEstado(int id, Resultado<IncidenciaDto> resultado, string mensajeExito)
    {
        if (resultado.Tipo == TipoError.NoEncontrado)
            return NotFound();

        if (resultado.Exito)
            TempData["Mensaje"] = mensajeExito;
        else
            TempData["Error"] = resultado.Error;

        return RedirectToAction(nameof(Detalle), new { id });
    }
}
