using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Domain.Incidencias;
using GestorIncidencias.Web.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GestorIncidencias.Web.Pages.Paginas.Incidencias;

/// <summary>
/// PageModel = el "code-behind" de la página, pero SIN ViewState ni eventos de controles.
///   OnGet...            → se ejecuta con GET  (equivale a Page_Load sin IsPostBack)
///   OnPost{Handler}...  → se ejecuta con POST (equivale a Button_Click)
/// </summary>
public class IndexModel(IIncidenciaService servicio, IAuthorizationService autorizacion) : PageModel
{
    public Pagina<IncidenciaDto> Resultado { get; private set; } = default!;

    // SupportsGet = true → también se enlaza en peticiones GET (?Estado=Abierta&NumeroPagina=2)
    [BindProperty(SupportsGet = true)]
    public EstadoIncidencia? Estado { get; set; }

    [BindProperty(SupportsGet = true)]
    public int NumeroPagina { get; set; } = 1;

    // GET /Paginas/Incidencias
    public async Task OnGetAsync(CancellationToken ct) =>
        Resultado = await servicio.ListarAsync(Estado, NumeroPagina, ct: ct);

    // POST /Paginas/Incidencias?handler=Resolver&id=3   ← "handler con nombre"
    public async Task<IActionResult> OnPostResolverAsync(int id, CancellationToken ct)
    {
        // En Razor Pages, [Authorize] en un MÉTODO handler no tiene efecto: se comprueba a mano (laboratorio 2 del día 4).
        if (!(await autorizacion.AuthorizeAsync(User, Politicas.GestionarIncidencias)).Succeeded)
            return Forbid();   // 403 → la cookie lo convierte en una redirección a /Cuenta/AccesoDenegado

        var resultado = await servicio.ResolverAsync(id, ct);

        if (resultado.Exito)
            TempData["Mensaje"] = $"Incidencia {id} resuelta.";
        else
            TempData["Error"] = resultado.Error;

        return RedirectToPage(new { Estado, NumeroPagina });   // Post-Redirect-Get, conservando filtro y página
    }
}
