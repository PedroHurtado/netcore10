using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Incidencias;
using GestorIncidencias.Web.Estado;
using GestorIncidencias.Web.Models;
using GestorIncidencias.Web.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GestorIncidencias.Web.Controllers;

/// <summary>
/// Controlador MVC de incidencias: recibe la petición, llama al caso de uso
/// y elige qué vista mostrar o a dónde redirigir. NO contiene reglas de negocio.
///
/// Compárese con Web Forms: lo que antes era Page_Load y Button_Click
/// ahora son acciones GET y POST de un controlador.
///
/// Día 4: no lleva [Authorize] y aun así exige haber iniciado sesión: lo hace la política POR DEFECTO
/// (FallbackPolicy en Program.cs). Seguro por defecto: una acción nueva nace protegida.
/// Las acciones de gestión exigen además la política GestionarIncidencias (laboratorio 2 del día 4).
/// </summary>
public class IncidenciasController(IIncidenciaService servicio) : Controller
{
    // GET /Incidencias
    // GET /Incidencias?estado=Abierta&texto=correo&pagina=2   ← model binding desde la query string
    // (el filtro "texto" es el laboratorio 1 del día 4: la búsqueda de BuscarIncidencias.aspx)
    public async Task<IActionResult> Index(EstadoIncidencia? estado, string? texto, int pagina = 1, CancellationToken ct = default)
    {
        var resultado = await servicio.ListarAsync(estado, pagina, texto: texto, ct: ct);
        var recientes = HttpContext.Session.ObtenerVisitas();               // estado de SESIÓN (ver Estado/HistorialVisitas.cs)
        return View(new ListadoIncidenciasViewModel(resultado, estado, texto, recientes));   // Views/Incidencias/Index.cshtml
    }

    // GET /Incidencias/Detalle/5   ← "id" sale de la ruta {id?}
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        var detalle = await servicio.ObtenerAsync(id, ct);
        if (detalle is null)
            return NotFound();                                              // 404

        HttpContext.Session.RegistrarVisita(id, detalle.Incidencia.Titulo);
        await CargarCategoriasAsync(ct, seleccionada: detalle.Incidencia.CategoriaId);
        return View(detalle);                                               // Views/Incidencias/Detalle.cshtml
    }

    // GET /Incidencias/Crear   → muestra el formulario vacío
    [HttpGet]
    public async Task<IActionResult> Crear(CancellationToken ct)
    {
        await CargarCategoriasAsync(ct);
        return View(new IncidenciaFormulario());
    }

    // POST /Incidencias/Crear  → recibe el formulario
    [HttpPost]
    [ValidateAntiForgeryToken]   // protege contra CSRF (el <form> genera el token automáticamente)
    public async Task<IActionResult> Crear(IncidenciaFormulario formulario, CancellationToken ct)
    {
        // 1) Validación de la interfaz (atributos del ViewModel)
        if (!ModelState.IsValid)
        {
            await CargarCategoriasAsync(ct);   // el desplegable hay que volver a rellenarlo: no viaja en el POST
            return View(formulario);
        }

        // 2) Caso de uso (reglas de negocio)
        var resultado = await servicio.CrearAsync(formulario.AComando(), ct);
        if (!resultado.Exito)
        {
            ModelState.AddModelError(string.Empty, resultado.Error!);   // error general del formulario
            await CargarCategoriasAsync(ct);
            return View(formulario);
        }

        // 3) Patrón Post-Redirect-Get: tras un POST correcto, SIEMPRE redirigir.
        TempData["Mensaje"] = $"Incidencia {resultado.Valor!.Id} creada correctamente.";
        return RedirectToAction(nameof(Detalle), new { id = resultado.Valor.Id });
    }

    // POST /Incidencias/Iniciar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.GestionarIncidencias)]
    public async Task<IActionResult> Iniciar(int id, CancellationToken ct) =>
        TrasCambio(id, await servicio.IniciarAsync(id, ct), "Incidencia iniciada.");

    // POST /Incidencias/Resolver/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.GestionarIncidencias)]
    public async Task<IActionResult> Resolver(int id, CancellationToken ct) =>
        TrasCambio(id, await servicio.ResolverAsync(id, ct), "Incidencia resuelta.");

    // POST /Incidencias/Cerrar/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.GestionarIncidencias)]
    public async Task<IActionResult> Cerrar(int id, CancellationToken ct) =>
        TrasCambio(id, await servicio.CerrarAsync(id, ct), "Incidencia cerrada.");

    // POST /Incidencias/Reabrir/5   (laboratorio 2 del día 2)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.GestionarIncidencias)]
    public async Task<IActionResult> Reabrir(int id, CancellationToken ct) =>
        TrasCambio(id, await servicio.ReabrirAsync(id, ct), "Incidencia reabierta.");

    // POST /Incidencias/CambiarCategoria/5   (laboratorio 1 del día 3; campo categoriaId vacío = sin categoría)
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.GestionarIncidencias)]
    public async Task<IActionResult> CambiarCategoria(int id, int? categoriaId, CancellationToken ct) =>
        TrasCambio(id, await servicio.CambiarCategoriaAsync(id, categoriaId, ct), "Categoría actualizada.");

    // POST /Incidencias/Comentar/5   (campo Texto del formulario de la ficha; el autor es el usuario autenticado)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Comentar(int id, ComentarioFormulario formulario, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Detalle), new { id });
        }

        return TrasCambio(id, await servicio.ComentarAsync(id, formulario.AComando(), ct), "Comentario añadido.");
    }

    /// <summary>
    /// Común a todas las acciones POST de la ficha: deja un mensaje en TempData y vuelve al detalle.
    /// TempData sobrevive a UNA redirección (se guarda en una cookie): ideal para Post-Redirect-Get.
    /// </summary>
    private IActionResult TrasCambio(int id, Resultado<IncidenciaDto> resultado, string mensajeExito)
    {
        if (resultado.Tipo == TipoError.NoEncontrado)
            return NotFound();

        if (resultado.Exito)
            TempData["Mensaje"] = mensajeExito;
        else
            TempData["Error"] = resultado.Error;

        return RedirectToAction(nameof(Detalle), new { id });
    }

    /// <summary>
    /// Opciones del desplegable de categorías. ViewBag (dinámico, igual que en MVC 5) es cómodo para datos
    /// auxiliares de la vista; lo principal (el formulario) sigue yendo en el modelo fuertemente tipado.
    /// </summary>
    private async Task CargarCategoriasAsync(CancellationToken ct, int? seleccionada = null) =>
        ViewBag.Categorias = new SelectList(await servicio.ListarCategoriasAsync(ct), nameof(CategoriaDto.Id), nameof(CategoriaDto.Nombre), seleccionada);
}
