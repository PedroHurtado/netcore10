# 6. Razor Pages y controladores API (sobre los mismos casos de uso)

Gracias a Clean Architecture, añadir otra interfaz **no toca el negocio**. En este capítulo vemos dos interfaces más sobre el mismo `IIncidenciaService`:

```
  Navegador ──▶ IncidenciasController (MVC) ──┐
  Navegador ──▶ Razor Pages (Pages/Paginas) ──┼──▶ IIncidenciaService ──▶ Domain + Infrastructure
  SPA / app ──▶ IncidenciasApiController ─────┘
```

## 6.1 Razor Pages

Ayer vimos la idea: **cada URL es un fichero** `.cshtml` con su clase `PageModel`. En el proyecto están en [Pages/Paginas/Incidencias](../../src/day-02/GestorIncidencias.Web/Pages/Paginas/Incidencias/).

```
Pages/
├── _ViewImports.cshtml
├── _ViewStart.cshtml               ← reutiliza Views/Shared/_Layout.cshtml
└── Paginas/Incidencias/
    ├── Index.cshtml + .cs          → /Paginas/Incidencias
    └── Crear.cshtml + .cs          → /Paginas/Incidencias/Crear
```

### La misma pantalla "Crear" en MVC y en Razor Pages

```csharp
// MVC: dos acciones dentro de un controlador con muchas más
public class IncidenciasController(IIncidenciaService servicio) : Controller
{
    [HttpGet]  public IActionResult Crear() => View(new IncidenciaFormulario());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(IncidenciaFormulario formulario, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(formulario);
        // ... caso de uso ...
        return RedirectToAction(nameof(Detalle), new { id });
    }
}
```

```csharp
// Razor Pages: una clase solo para esta página
public class CrearModel(IIncidenciaService servicio) : PageModel
{
    [BindProperty]
    public IncidenciaFormulario Formulario { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();
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
```

| | MVC | Razor Pages |
|---|---|---|
| Unidad de organización | Controlador (varias pantallas) | Página (una pantalla) |
| GET / POST | Acciones `[HttpGet]` / `[HttpPost]` | `OnGet` / `OnPost` |
| Datos del formulario | Parámetro de la acción | Propiedad con `[BindProperty]` |
| Volver a pintar con errores | `return View(modelo)` | `return Page()` |
| Redirigir | `RedirectToAction(...)` | `RedirectToPage(...)` |
| Antiforgery | `[ValidateAntiForgeryToken]` | **Automático** |
| Ruta | Plantilla global `{controller}/{action}/{id?}` | Ubicación del fichero (o `@page "/ruta"`) |
| En los `asp-for` | `asp-for="Titulo"` | `asp-for="Formulario.Titulo"` (es una propiedad del PageModel) |

### Varios botones en una página: *handlers* con nombre

En el listado, cada fila tiene un botón "Resolver". Un método `OnPost{Nombre}` responde a cada botón:

```csharp
public async Task<IActionResult> OnPostResolverAsync(int id, CancellationToken ct) { ... }
```

```cshtml
<form method="post" asp-page-handler="Resolver" asp-route-id="@incidencia.Id">
    <button type="submit">Resolver</button>
</form>
```

Es lo más parecido a `btnResolver_Click` de Web Forms, pero cada clic es un POST normal, sin ViewState.

### ¿MVC o Razor Pages?

| Usa **Razor Pages** si... | Usa **MVC** si... |
|---|---|
| Pantallas independientes, tipo formulario/CRUD | Muchas pantallas que comparten lógica de navegación |
| Migras desde **Web Forms** (página + código) | Migras desde **MVC 5** (controladores casi 1:1) |
| Prefieres "todo lo de una pantalla junto" (encaja con Vertical Slice) | El equipo ya conoce MVC |

Ambas se pueden mezclar en la misma aplicación, como hacemos en el proyecto.

## 6.2 Controladores API

Ayer hicimos la API con Minimal APIs. Hoy está hecha con un **controlador API**: [IncidenciasApiController.cs](../../src/day-02/GestorIncidencias.Web/Controllers/Api/IncidenciasApiController.cs).

```csharp
[ApiController]
[Route("api/incidencias")]
public class IncidenciasApiController(IIncidenciaService servicio) : ControllerBase
{
    [HttpGet("{id:int}", Name = "ObtenerIncidencia")]
    public async Task<ActionResult<IncidenciaDto>> Obtener(int id, CancellationToken ct)
    {
        var incidencia = await servicio.ObtenerAsync(id, ct);
        return incidencia is null ? NotFound() : incidencia;
    }

    [HttpPost]
    public async Task<ActionResult<IncidenciaDto>> Crear(CrearIncidenciaRequest request, CancellationToken ct)
    {
        // Sin "if (!ModelState.IsValid)": [ApiController] devuelve 400 automáticamente.
        var resultado = await servicio.CrearAsync(new CrearIncidenciaComando(request.Titulo, request.Descripcion, request.Prioridad), ct);
        return resultado.Exito
            ? CreatedAtRoute("ObtenerIncidencia", new { id = resultado.Valor!.Id }, resultado.Valor)
            : Problema(resultado);
    }
}
```

Qué aporta `[ApiController]`:

| Comportamiento | Sin `[ApiController]` | Con `[ApiController]` |
|---|---|---|
| Modelo no válido | Hay que comprobar `ModelState` a mano | **400 ValidationProblem automático** |
| Origen de un parámetro complejo | Hay que poner `[FromBody]` | Se infiere del cuerpo |
| Rutas | Convencionales o por atributos | **Obligatorio** por atributos |
| Errores 4xx | Respuesta vacía | ProblemDetails (RFC 9457) |

### De `Resultado` a código HTTP

El dominio dice **qué** ha fallado (`TipoError`); la API decide **cómo** se comunica en HTTP:

```csharp
private ObjectResult Problema<T>(Resultado<T> resultado) =>
    Problem(detail: resultado.Error, statusCode: resultado.Tipo switch
    {
        TipoError.NoEncontrado => StatusCodes.Status404NotFound,
        TipoError.Conflicto    => StatusCodes.Status409Conflict,
        _                      => StatusCodes.Status400BadRequest
    });
```

El controlador MVC hace lo mismo con otra "traducción": `NotFound()` o un mensaje en `TempData`. **Mismo caso de uso, distinta presentación**: eso es separar capas.

### ¿Minimal APIs o controladores?

Las dos son igual de válidas en .NET 10 (comparativa en el [capítulo 3 del día 1](../day-01/03-arquitecturas-modernas.md#33-api-rest)). Los controladores encajan mejor si el equipo viene de Web API 2 / MVC 5 o si la aplicación ya usa MVC para las vistas; Minimal APIs, en servicios pequeños o con Vertical Slice.

## Preguntas de repaso

1. ¿Qué URL tiene la página `Pages/Paginas/Incidencias/Crear.cshtml`?
2. ¿Por qué en la Razor Page se escribe `asp-for="Formulario.Titulo"` y en la vista MVC `asp-for="Titulo"`?
3. ¿Qué devuelve la API si se envía `{"titulo":"abc"}`? ¿Quién genera esa respuesta?
4. ¿Cuántas líneas de `IncidenciaService` hubo que cambiar para añadir la API y las Razor Pages?

## Referencias

> Enlaces comprobados el 6 de octubre de 2026.

- [Arquitectura y conceptos de Razor Pages](https://learn.microsoft.com/es-es/aspnet/core/razor-pages/?view=aspnetcore-10.0) — `PageModel`, handlers, `[BindProperty]`.
- [Tutorial: Introducción a Razor Pages](https://learn.microsoft.com/es-es/aspnet/core/tutorials/razor-pages/razor-pages-start?view=aspnetcore-10.0)
- [Creación de API web con controladores](https://learn.microsoft.com/es-es/aspnet/core/web-api/?view=aspnetcore-10.0) — `[ApiController]` y sus comportamientos.
- [Tipos de valor devuelto de acciones de controlador](https://learn.microsoft.com/es-es/aspnet/core/web-api/action-return-types?view=aspnetcore-10.0) — `ActionResult<T>`.
- [Control de errores en las API de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0) — ProblemDetails.
