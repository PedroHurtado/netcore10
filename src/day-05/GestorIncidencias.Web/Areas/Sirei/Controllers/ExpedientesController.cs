using GestorIncidencias.Application.Expedientes;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Expedientes;
using GestorIncidencias.Web.Areas.Sirei.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorIncidencias.Web.Areas.Sirei.Controllers;

/// <summary>
/// Caso práctico SIREI (día 5): las páginas BuscarExpedientes.aspx y ExpedienteDetalle.aspx convertidas en UN controlador.
///
///   BuscarExpedientes.aspx  (Page_Load, btnBuscar_Click, PageIndexChanging) → GET  Index(texto, estado, pagina)
///   ExpedienteDetalle.aspx  (Page_Load !IsPostBack)                         → GET  Detalle(id)
///                           (btnGuardar_Click)                              → POST Detalle(id, formulario)
///                           (gvTramites_RowCommand "Completar")             → POST CompletarTramite(id, tramiteId)
///                           (btnAgregarTramite_Click)                       → POST AgregarTramite(id, descripcion)
///
/// [Area("Sirei")]: el controlador vive en Areas/Sirei y sus URLs empiezan por /Sirei (ruta en Program.cs).
/// Un área agrupa lo migrado de cada aplicación sin mezclarlo con lo demás, como una "subaplicación".
///
/// Sin [Authorize]: la FallbackPolicy exige sesión, que es lo que hacía "if (Session["Usuario"] == null) Response.Redirect(...)".
/// </summary>
[Area("Sirei")]
public class ExpedientesController(IExpedienteService servicio) : Controller
{
    // GET /Sirei/Expedientes?texto=obra&estado=EnTramite&pagina=2
    public async Task<IActionResult> Index(string? texto, EstadoExpediente? estado, int pagina = 1, CancellationToken ct = default)
    {
        var resultado = await servicio.ListarAsync(texto, estado, pagina, ct: ct);
        return View(new ListadoExpedientesViewModel(resultado, texto, estado));
    }

    // GET /Sirei/Expedientes/Detalle/5
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        var expediente = await servicio.ObtenerAsync(id, ct);
        if (expediente is null)
            return NotFound();

        return View(new FichaExpedienteViewModel(expediente, ExpedienteFormulario.Desde(expediente)));
    }

    // POST /Sirei/Expedientes/Detalle/5   ← antes: btnGuardar_Click
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Detalle(
        int id, [Bind(Prefix = nameof(FichaExpedienteViewModel.Formulario))] ExpedienteFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await servicio.ActualizarAsync(id, formulario.AComando(), ct);
            if (resultado.Tipo == TipoError.NoEncontrado)
                return NotFound();

            if (resultado.Exito)
            {
                TempData["Mensaje"] = "Expediente guardado.";                 // antes: lblMensaje.Text = "Guardado"
                return RedirectToAction(nameof(Detalle), new { id });         // Post-Redirect-Get
            }

            ModelState.AddModelError(string.Empty, resultado.Error!);        // antes: lblError.Text = "..."
        }

        // Se vuelve a pintar la ficha con los datos ACTUALES de la base de datos y lo que el usuario había escrito
        // (en Web Forms lo hacía el ViewState; aquí, return View con el mismo formulario).
        var actual = await servicio.ObtenerAsync(id, ct);
        if (actual is null)
            return NotFound();

        // Concurrencia: si otra persona guardó entretanto, se recarga con sus datos (y la versión nueva).
        // Volver a enviar la versión vieja fallaría una y otra vez.
        if (actual.Version != formulario.Version)
        {
            TempData["Error"] = ModelState[string.Empty]?.Errors.FirstOrDefault()?.ErrorMessage;
            return RedirectToAction(nameof(Detalle), new { id });
        }

        return View(new FichaExpedienteViewModel(actual, formulario));
    }

    // POST /Sirei/Expedientes/AgregarTramite/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AgregarTramite(int id, string? descripcion, CancellationToken ct) =>
        TrasCambio(id, await servicio.AgregarTramiteAsync(id, descripcion ?? string.Empty, ct), "Trámite añadido.");

    // POST /Sirei/Expedientes/CompletarTramite/5?tramiteId=12   ← antes: gvTramites_RowCommand (CommandName="Completar")
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompletarTramite(int id, int tramiteId, CancellationToken ct) =>
        TrasCambio(id, await servicio.CompletarTramiteAsync(id, tramiteId, ct), "Trámite completado.");

    // ---------------------------------------------------------------- URLs antiguas
    // Los usuarios tienen marcadores, hay correos con enlaces y otras aplicaciones enlazan a las páginas .aspx.
    // 301 (permanente): el navegador y los buscadores aprenden la URL nueva.
    // [AllowAnonymous]: la redirección no revela nada; si no hay sesión, la página de destino ya pedirá el login.

    // GET /ExpedienteDetalle.aspx?id=5  →  301 /Sirei/Expedientes/Detalle/5
    [HttpGet("/ExpedienteDetalle.aspx")]
    [AllowAnonymous]
    public IActionResult DetalleLegacy(int id) => RedirectToActionPermanent(nameof(Detalle), new { id });

    // GET /BuscarExpedientes.aspx  →  301 /Sirei/Expedientes
    [HttpGet("/BuscarExpedientes.aspx")]
    [AllowAnonymous]
    public IActionResult IndexLegacy() => RedirectToActionPermanent(nameof(Index));

    /// <summary>
    /// Mensaje en TempData y vuelta a la ficha (Post-Redirect-Get). Si el expediente no existe,
    /// es la propia ficha (GET Detalle) la que responde 404.
    /// </summary>
    private IActionResult TrasCambio(int id, Resultado resultado, string mensajeExito)
    {
        if (resultado.Exito)
            TempData["Mensaje"] = mensajeExito;
        else
            TempData["Error"] = resultado.Error;

        return RedirectToAction(nameof(Detalle), new { id });
    }
}
