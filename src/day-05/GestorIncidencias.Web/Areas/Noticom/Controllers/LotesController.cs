using GestorIncidencias.Application.Lotes;
using GestorIncidencias.Application.Seguridad;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Lotes;
using GestorIncidencias.Web.Areas.Noticom.Models;
using GestorIncidencias.Web.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorIncidencias.Web.Areas.Noticom.Controllers;

/// <summary>
/// Caso práctico Noticom (día 5): el LoteController de MVC 5 migrado. Compárese con docs/day-05/legacy/noticom.
///
///   MVC 5                                              ASP.NET Core
///   ─────────────────────────────────────────────      ─────────────────────────────────────────────────────
///   Index(string t)  con t = "c" | "v" | "cr" | "b"    Index(ModoLotes modo, FiltroLotes filtro, int pagina)
///   [HttpPost] ConsultaLotes(...) → PartialView         [HttpGet] Tabla(...) → PartialView (leer = GET)
///   CrearRemesa(string lista JSON, string nombre)      CrearRemesa(CrearRemesaFormulario) con model binding
///   new NoticomEntities() en el controlador            ILoteService inyectado
///   Session["Usuario"] comprobado en cada acción       FallbackPolicy + [Authorize(Policy/Roles)]
///   return Json(..., JsonRequestBehavior.AllowGet)     (no hace falta: la tabla se devuelve como HTML)
/// </summary>
[Area("Noticom")]
public class LotesController(ILoteService servicio, IAuthorizationService autorizacion) : Controller
{
    // GET /Noticom/Lotes?modo=Validacion&proceso=120&ejercicio=2026&pagina=2
    public async Task<IActionResult> Index(ModoLotes modo, FiltroLotes filtro, int pagina = 1, CancellationToken ct = default) =>
        View(await CrearModeloAsync(modo, filtro, pagina, ct));

    // GET /Noticom/Lotes/Tabla?...   ← la pide wwwroot/js/noticom/lotes.js con fetch: SOLO la tabla (vista parcial, sin layout).
    // Es la misma búsqueda que Index: si JavaScript no está disponible, el formulario funciona igual (GET a Index).
    public async Task<IActionResult> Tabla(ModoLotes modo, FiltroLotes filtro, int pagina = 1, CancellationToken ct = default) =>
        PartialView("_TablaLotes", await CrearModeloAsync(modo, filtro, pagina, ct));

    // POST /Noticom/Lotes/Validar/12
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.GestionarLotes)]
    public async Task<IActionResult> Validar(int id, string? volver, CancellationToken ct) =>
        Volver(await servicio.ValidarAsync(id, ct), $"Lote {id} validado.", volver);

    // POST /Noticom/Lotes/Borrar/12   (solo administradores; en el legacy bastaba con ver la pantalla "b")
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<IActionResult> Borrar(int id, string? volver, CancellationToken ct) =>
        Volver(await servicio.BorrarAsync(id, ct), $"Lote {id} borrado.", volver);

    // POST /Noticom/Lotes/CrearRemesa   (Nombre + LoteIds=3&LoteIds=7&...)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.GestionarLotes)]
    public async Task<IActionResult> CrearRemesa(CrearRemesaFormulario formulario, string? volver, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Volver(Resultado.Fallo(PrimerError()), string.Empty, volver);

        var resultado = await servicio.CrearRemesaAsync(formulario.AComando(), ct);
        if (!resultado.Exito)
            return Volver(Resultado.Fallo(resultado.Error!), string.Empty, volver);

        var remesa = resultado.Valor!;
        TempData["Mensaje"] = $"Remesa \"{remesa.Nombre}\" creada con {remesa.NumeroLotes} lote(s) y {remesa.NumeroNotificaciones} notificaciones.";
        return RedirectToAction(nameof(Remesas));
    }

    // GET /Noticom/Lotes/Remesas
    public async Task<IActionResult> Remesas(CancellationToken ct) =>
        View(await servicio.ListarRemesasAsync(ct));

    // ---------------------------------------------------------------- privados

    private async Task<LotesViewModel> CrearModeloAsync(ModoLotes modo, FiltroLotes filtro, int pagina, CancellationToken ct)
    {
        // Cada pantalla trabaja sobre los lotes que le corresponden: el modo FIJA el estado
        // (antes lo decidía el procedimiento de búsqueda según el TipoEjecucion que le pasaba el JavaScript).
        var filtroEfectivo = modo switch
        {
            ModoLotes.Validacion or ModoLotes.Borrado => filtro with { Estado = EstadoLote.PendienteValidacion },
            ModoLotes.CrearRemesa => filtro with { Estado = EstadoLote.Validado },
            _ => filtro
        };

        var resultado = await servicio.ListarAsync(filtroEfectivo, pagina, ct: ct);
        var puedeGestionar = (await autorizacion.AuthorizeAsync(User, Politicas.GestionarLotes)).Succeeded;
        var urlVolver = Url.Action(nameof(Index), Rutas.De(modo, filtro, resultado.NumeroPagina))!;

        return new LotesViewModel(modo, filtroEfectivo, resultado, puedeGestionar, User.IsInRole(Roles.Administrador), urlVolver);
    }

    /// <summary>
    /// Post-Redirect-Get volviendo a la MISMA búsqueda (modo, filtros y página), que llega en el campo oculto "volver".
    /// Solo URLs locales: un "volver" manipulado no puede sacar al usuario de la aplicación (open redirect, día 4).
    /// </summary>
    private IActionResult Volver(Resultado resultado, string mensajeExito, string? volver)
    {
        if (resultado.Exito)
            TempData["Mensaje"] = mensajeExito;
        else
            TempData["Error"] = resultado.Error;

        return Url.IsLocalUrl(volver) ? LocalRedirect(volver) : RedirectToAction(nameof(Index));
    }

    private string PrimerError() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault() ?? "Datos no válidos.";
}
