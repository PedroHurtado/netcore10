# 3. Arquitectura moderna: MVC, Razor Pages y API REST

ASP.NET Core es **un único framework** con varios modelos de programación que comparten la misma base (host, middleware, DI, configuración, enrutamiento, seguridad). Se pueden **combinar en la misma aplicación**.

```
                    ┌───────────── ASP.NET Core ─────────────┐
                    │ Host · Middleware · DI · Config · Auth │
                    └──────┬─────────┬──────────┬────────┬───┘
                           │         │          │        │
                       MVC (vistas) Razor   API REST   Blazor
                                    Pages  (Minimal APIs
                                            o controladores)
```

## 3.1 MVC (Modelo – Vista – Controlador)

```
Petición ──▶ Controlador ──▶ Modelo (servicios, datos)
                 │
                 ▼
               Vista (Razor .cshtml) ──▶ HTML
```

- **Controlador**: recibe la petición, coordina, decide qué devolver.
- **Modelo**: datos y lógica (en la práctica, *ViewModels* + servicios).
- **Vista**: plantilla Razor que genera HTML.

```csharp
public class IncidenciasController(IIncidenciaService servicio) : Controller
{
    public async Task<IActionResult> Index() => View(await servicio.ListarAsync());   // Views/Incidencias/Index.cshtml
}
```

**Cuándo:** aplicaciones con muchas pantallas y lógica de navegación compleja; equipos que vienen de **ASP.NET MVC 5** (la migración es la más directa).

## 3.2 Razor Pages

Modelo **centrado en la página**: cada URL es un fichero `.cshtml` con su clase `PageModel` asociada. Es lo más parecido conceptualmente a Web Forms (página + *code-behind*), pero **sin ViewState ni eventos de controles**.

```
Pages/
  Incidencias/
    Index.cshtml        ← /Incidencias
    Index.cshtml.cs     ← IndexModel : PageModel  → OnGet()
    Crear.cshtml        ← /Incidencias/Crear
    Crear.cshtml.cs     ← CrearModel : PageModel  → OnGet(), OnPost()
```

```csharp
public class CrearModel(IIncidenciaService servicio) : PageModel
{
    [BindProperty] public CrearIncidenciaRequest Entrada { get; set; } = default!;

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        await servicio.CrearAsync(Entrada);
        return RedirectToPage("Index");
    }
}
```

**Cuándo:** formularios CRUD, aplicaciones centradas en pantallas independientes, **migraciones desde Web Forms** (el modelo mental "página + código" se conserva).

## 3.3 API REST

La aplicación no genera HTML: expone **recursos** mediante HTTP y devuelve JSON. El cliente puede ser una SPA (Angular, React), una app móvil u otro sistema.

| Verbo | Ruta | Acción | Respuesta típica |
|---|---|---|---|
| `GET` | `/api/incidencias` | Listar | `200 OK` |
| `GET` | `/api/incidencias/5` | Obtener una | `200 OK` / `404 Not Found` |
| `POST` | `/api/incidencias` | Crear | `201 Created` + cabecera `Location` |
| `PUT` | `/api/incidencias/5` | Reemplazar | `204 No Content` |
| `DELETE` | `/api/incidencias/5` | Borrar | `204 No Content` |
| `POST` | `/api/incidencias/5/resolver` | Acción de negocio | `200 OK` / `400 Bad Request` |

Los errores se devuelven en formato estándar **ProblemDetails** (RFC 9457):

```json
{ "type": "...", "title": "Conflict", "status": 409, "detail": "No se pueden abrir más de 5 incidencias a la vez." }
```

Dos estilos en ASP.NET Core:

**Minimal APIs** (lo que usamos hoy):

```csharp
app.MapGet("/api/incidencias/{id:int}", async (int id, IIncidenciaService s) =>
    await s.ObtenerAsync(id) is { } i ? Results.Ok(i) : Results.NotFound());
```

**Controladores API** (día 2):

```csharp
[ApiController, Route("api/[controller]")]
public class IncidenciasController(IIncidenciaService s) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<IncidenciaResponse>> Get(int id) => ...;
}
```

| | Minimal APIs | Controladores |
|---|---|---|
| Código | Mínimo, funcional | Clases, atributos, convenciones |
| Filtros | Endpoint filters | Action filters (más maduros) |
| Rendimiento | Ligeramente superior; compatible con Native AOT | Muy bueno |
| Familiaridad para quien viene de Web API 2 | Menor | Mayor |

## 3.4 Blazor (mención)

Componentes de interfaz en C# en lugar de JavaScript. Se renderizan en el servidor (estático o interactivo por SignalR) o en el navegador (WebAssembly). Es una opción a considerar para reescribir interfaces Web Forms muy interactivas, pero queda fuera del alcance principal de este curso.

## 3.5 ¿Cuál elijo?

| Escenario | Recomendación |
|---|---|
| Migración de **Web Forms** con formularios CRUD | **Razor Pages** |
| Migración de **ASP.NET MVC 5** | **MVC** (controladores y vistas casi 1:1) |
| Backend para SPA / móvil / integraciones | **API REST** |
| Interfaz muy interactiva sin JavaScript | Blazor |
| Aplicación grande | **Combinación**: Razor Pages o MVC para la interfaz + API para integraciones |

En el **Gestor de Incidencias** empezamos hoy por la **API REST** (es lo que menos "ruido" añade para aprender los fundamentos) y el día 2 añadiremos interfaz con MVC y Razor Pages sobre los **mismos servicios**.

## Preguntas de repaso

1. ¿Qué código devuelve un `POST` que crea un recurso y qué cabecera debe incluir?
2. Un compañero propone migrar una aplicación Web Forms de 40 formularios de mantenimiento. ¿MVC o Razor Pages? ¿Por qué?
3. ¿Se pueden tener controladores MVC, Razor Pages y Minimal APIs en la misma aplicación?

## Referencias

> Enlaces comprobados el 4 de octubre de 2026.

**Documentación oficial**

- [Elección de una interfaz de usuario de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/tutorials/choose-web-ui?view=aspnetcore-10.0) — Comparativa oficial entre MVC, Razor Pages y Blazor.
- [Información general de ASP.NET Core MVC](https://learn.microsoft.com/es-es/aspnet/core/mvc/overview?view=aspnetcore-10.0)
- [Arquitectura y conceptos de Razor Pages](https://learn.microsoft.com/es-es/aspnet/core/razor-pages/?view=aspnetcore-10.0)
- [Introducción a las API: Minimal APIs frente a controladores](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/apis?view=aspnetcore-10.0) — Cuándo elegir cada estilo.
- [Referencia rápida de las API mínimas](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0)
- [Creación de API web con controladores](https://learn.microsoft.com/es-es/aspnet/core/web-api/?view=aspnetcore-10.0)
- [Creación de respuestas en API mínimas](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0) — `Results.Ok`, `Results.NotFound`, `Results.Problem`...
- [Control de errores en las API de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0) — ProblemDetails.
- [Blazor de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/blazor/?view=aspnetcore-10.0)
- [Procedimientos recomendados de diseño de API web](https://learn.microsoft.com/es-es/azure/architecture/best-practices/api-design) (Azure Architecture Center) — Recursos, verbos, códigos de estado, paginación y versionado.

**Estándares**

- [RFC 9110 — HTTP Semantics](https://www.rfc-editor.org/rfc/rfc9110) (inglés) — Definición normativa de los métodos (§9) y de los códigos de estado (§15), como `201 Created` con `Location`.
- [RFC 9457 — Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457) (inglés) — Formato estándar de errores que genera `AddProblemDetails`.
