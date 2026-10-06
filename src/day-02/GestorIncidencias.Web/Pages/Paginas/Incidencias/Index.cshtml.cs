using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Domain.Incidencias;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GestorIncidencias.Web.Pages.Paginas.Incidencias;

/// <summary>
/// PageModel = el "code-behind" de la página, pero SIN ViewState ni eventos de controles.
///   OnGet...            → se ejecuta con GET  (equivale a Page_Load sin IsPostBack)
///   OnPost{Handler}...  → se ejecuta con POST (equivale a Button_Click)
/// </summary>
public class IndexModel(IIncidenciaService servicio) : PageModel
{
    public IReadOnlyList<IncidenciaDto> Incidencias { get; private set; } = [];

    // SupportsGet = true → también se enlaza en peticiones GET (?Estado=Abierta)
    [BindProperty(SupportsGet = true)]
    public EstadoIncidencia? Estado { get; set; }

    // GET /Paginas/Incidencias
    public async Task OnGetAsync(CancellationToken ct) =>
        Incidencias = await servicio.ListarAsync(Estado, ct);

    // POST /Paginas/Incidencias?handler=Resolver&id=3   ← "handler con nombre"
    public async Task<IActionResult> OnPostResolverAsync(int id, CancellationToken ct)
    {
        var resultado = await servicio.ResolverAsync(id, ct);

        if (resultado.Exito)
            TempData["Mensaje"] = $"Incidencia {id} resuelta.";
        else
            TempData["Error"] = resultado.Error;

        return RedirectToPage(new { Estado });   // Post-Redirect-Get, conservando el filtro
    }
}
