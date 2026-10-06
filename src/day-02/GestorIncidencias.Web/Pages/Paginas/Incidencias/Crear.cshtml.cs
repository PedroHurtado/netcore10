using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GestorIncidencias.Web.Pages.Paginas.Incidencias;

/// <summary>
/// Misma lógica que IncidenciasController.Crear (GET + POST), pero en una sola clase por página.
/// Las Razor Pages validan el token antiforgery automáticamente en los POST.
/// </summary>
public class CrearModel(IIncidenciaService servicio) : PageModel
{
    // [BindProperty] → en el POST, el formulario se enlaza a esta propiedad.
    [BindProperty]
    public IncidenciaFormulario Formulario { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Page();   // vuelve a pintar la página con los errores

        var resultado = await servicio.CrearAsync(Formulario.AComando(), ct);
        if (!resultado.Exito)
        {
            ModelState.AddModelError(string.Empty, resultado.Error!);
            return Page();
        }

        TempData["Mensaje"] = $"Incidencia {resultado.Valor!.Id} creada desde Razor Pages.";
        return RedirectToPage("Index");
    }
}
