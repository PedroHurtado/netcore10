# 1. De Web Forms a MVC: controles, eventos y lógica de negocio

Ayer hicimos el **inventario** de SIREI. Hoy empezamos a **convertir** sus pantallas. Este capítulo es la "tabla de traducción" entre los dos modelos de programación: qué pasa con los controles de servidor, con los eventos y, sobre todo, con la lógica de negocio que vive en el *code-behind*.

No hay que aprenderse las tablas de memoria: son la chuleta para el caso práctico del día 5.

## 1.1 Dos modelos de programación distintos

Web Forms intentó que programar para la web se pareciera a programar en Windows Forms: **controles con estado** y **eventos**. ASP.NET Core MVC vuelve a lo que la web es en realidad: **peticiones y respuestas HTTP**.

```
 WEB FORMS (una página que "recuerda")              MVC (peticiones independientes)
 ─────────────────────────────────────              ─────────────────────────────────
 GET  Buscar.aspx                                   GET  /Incidencias?texto=vpn
   Page_Load (IsPostBack = false)                     IncidenciasController.Index(texto)
   → HTML + __VIEWSTATE oculto (estado de              → HTML (sin estado oculto)
     todos los controles, en base64)
                                                    GET  /Incidencias?texto=vpn&pagina=2
 POST Buscar.aspx  (clic en "Página 2")               Index(texto, pagina: 2)
   reconstruye los controles desde __VIEWSTATE        → HTML
   Page_Load (IsPostBack = true)
   gvResultados_PageIndexChanging(...)              POST /Incidencias/Resolver/5
   → HTML + __VIEWSTATE (actualizado)                 Resolver(5) → 302 a /Incidencias/Detalle/5
```

| | Web Forms | ASP.NET Core MVC / Razor Pages |
|---|---|---|
| Unidad de trabajo | La **página** (`.aspx` + `.aspx.cs`) | La **acción** (método del controlador) o el *handler* (`OnGet`, `OnPost`) |
| Cómo llega el usuario | Casi todo es un **POST a la misma página** (*postback*) | Cada operación tiene su **URL** y su **verbo** (GET para leer, POST para cambiar) |
| Estado entre peticiones | Automático (**ViewState**) | **No hay**: lo que hace falta viaja en la URL, en el formulario o se vuelve a leer (capítulo 2) |
| HTML | Lo generan los **controles** (`<asp:GridView>`) | Lo escribes tú en **Razor**, con ayuda de los Tag Helpers |
| Eventos | `Button_Click`, `SelectedIndexChanged`, `RowCommand`... | No hay eventos de servidor: hay **peticiones** que llegan a una acción |
| Ciclo de vida | `Init`, `Load`, eventos de controles, `PreRender`, `Render`... | El **pipeline** (middleware, día 1) + **filtros** + la acción |

> **Idea clave:** en Web Forms el servidor guarda la ilusión de que la página "sigue viva" entre clic y clic. En MVC **cada petición empieza de cero**. Todo lo demás se deriva de ahí.

### ¿Y Blazor?

Microsoft presenta **Blazor** como el camino natural para equipos de Web Forms, porque su modelo de **componentes con eventos** se parece mucho más. Este curso usa MVC y Razor Pages porque es lo que pide el temario y lo que ya usa Noticom, pero conviene saber que existe: Microsoft publica un libro gratuito, *Blazor para desarrolladores de ASP.NET Web Forms* (ver referencias). Para pantallas de gestión con formularios y listados, MVC y Razor Pages son una opción igual de válida y más sencilla de desplegar.

## 1.2 Controles: de `<asp:...>` a HTML + Razor

| Web Forms | ASP.NET Core | Ejemplo en el proyecto |
|---|---|---|
| `Site.Master` + `ContentPlaceHolder` | `_Layout.cshtml` + `@RenderBody()` / `@RenderSection` | `Views/Shared/_Layout.cshtml` |
| `<asp:Label>` / `<asp:Literal>` | `@expresion` (codificado en HTML, capítulo 6) | `@incidencia.Titulo` |
| `<asp:TextBox>` | `<input asp-for="Titulo" />` | `Views/Incidencias/Crear.cshtml` |
| `<asp:TextBox TextMode="MultiLine">` | `<textarea asp-for="Descripcion">` | ídem |
| `<asp:TextBox TextMode="Password">` | `<input asp-for="Clave" />` + `[DataType(DataType.Password)]` | `Views/Cuenta/Login.cshtml` |
| `<asp:DropDownList>` + `DataSource` | `<select asp-for="..." asp-items="...">` + `SelectList` | Desplegable de categorías |
| `<asp:CheckBox>` | `<input asp-for="Recordarme" />` (un `bool`) | Login |
| `<asp:Button OnClick="...">` | `<button type="submit">` dentro de un `<form asp-action="...">` | Botones de la ficha |
| `<asp:LinkButton>` (POST disfrazado de enlace) | Un **enlace** (`<a asp-action>`) si es lectura; un **botón en un formulario** si cambia datos | — |
| `<asp:HyperLink>` | `<a asp-controller="..." asp-action="..." asp-route-id="...">` | Enlaces del listado |
| `<asp:GridView>` / `<asp:Repeater>` / `<asp:ListView>` | `<table>` + `@foreach` | `Views/Incidencias/Index.cshtml` |
| Paginación del `GridView` | `Pagina<T>` + enlaces con `?pagina=N` (día 3) | Paginador del listado |
| `<asp:RequiredFieldValidator>`, `RangeValidator`... | `[Required]`, `[Range]`... en el ViewModel + `asp-validation-for` | `Models/IncidenciaFormulario.cs` |
| `<asp:ValidationSummary>` | `<div asp-validation-summary="ModelOnly">` | Formularios |
| `<asp:Panel Visible="false">` | `@if (...) { ... }` | Botones según el estado |
| `<asp:HiddenField>` | `<input type="hidden" asp-for="...">` | `ReturnUrl` del login |
| `UserControl` (`.ascx`) | **Vista parcial** (`<partial name="_Estado">`) o **View Component** (si necesita datos propios) | `Views/Shared/_Estado.cshtml` |
| `<asp:LoginView>`, `LoginStatus` | `@if (User.Identity.IsAuthenticated) { ... }` | Cabecera del layout |
| `<asp:UpdatePanel>` (AJAX parcial) | `fetch` desde un `.js` que pide una vista parcial o JSON. Ojo con la CSP: nada de JavaScript *inline* | — |
| Controles de terceros (Telerik, DevExpress, AjaxControlToolkit) | Versión del fabricante para ASP.NET Core, otra librería JavaScript, o **rediseñar** | — 🚩 |

Dos consecuencias prácticas:

1. **El HTML lo controlas tú.** Se acabaron los `id="ctl00_MainContent_gvResultados_ctl02_lnkVer"`. Los ids y nombres los decides tú (o los genera `asp-for` a partir del ViewModel).
2. **Los controles de terceros son el mayor riesgo** de una migración de Web Forms (ya salió en el análisis de ayer). Un `GridView` se migra en una hora; un *grid* de Telerik con agrupación, filtros y exportación a Excel, no.

## 1.3 Eventos: de `Button_Click` a acciones

| Web Forms | ASP.NET Core MVC | Razor Pages |
|---|---|---|
| `Page_Load` con `!IsPostBack` | Acción **GET** | `OnGet` / `OnGetAsync` |
| `Page_Load` con `IsPostBack` | (No existe: cada POST va a **su** acción) | — |
| `btnGuardar_Click` | Acción **POST** `Guardar` | `OnPost` / `OnPostAsync` |
| Dos botones en la misma página (`btnGuardar`, `btnEliminar`) | **Dos formularios** con distinto `asp-action` (o un formulario y `formaction`) | *Handlers* con nombre: `OnPostGuardar`, `OnPostEliminar` + `asp-page-handler` |
| `ddlEstado_SelectedIndexChanged` con `AutoPostBack="true"` | Formulario **GET** con un botón "Filtrar"; o un `.js` que envía el formulario al cambiar | Igual |
| `gvResultados_RowCommand` (`CommandName="Resolver"`, `CommandArgument="5"`) | Un formulario por fila: `<form asp-action="Resolver" asp-route-id="5">` | `asp-page-handler="Resolver" asp-route-id="5"` |
| `gvResultados_PageIndexChanging` | Enlace `?pagina=N` (GET) | Igual |
| `Response.Redirect("Detalle.aspx?id=5")` | `return RedirectToAction("Detalle", new { id = 5 })` | `return RedirectToPage("Detalle", new { id = 5 })` |
| `Server.Transfer(...)` | No existe. Se devuelve otra vista (`return View("Otra", modelo)`) o se redirige | Ídem |
| `Page.IsValid` | `ModelState.IsValid` | Ídem |
| `lblMensaje.Text = "Guardado"` | `TempData["Mensaje"] = "..."` + redirección (Post-Redirect-Get, día 2) | Ídem |
| `Application_Error` en `Global.asax` | `UseExceptionHandler` (middleware) | Ídem |

Los ejemplos de Razor Pages de la tabla están en [Pages/Paginas/Incidencias](../../src/day-04/GestorIncidencias.Web/Pages/Paginas/Incidencias/): `OnGetAsync` es el "Page_Load sin postback" y `OnPostResolverAsync` es el "RowCommand" de cada fila.

> **¿Razor Pages o MVC para migrar Web Forms?** Razor Pages se parece más (una página = un fichero + su "code-behind", el `PageModel`), y por eso a muchos equipos les resulta más cómoda para pantallas sueltas. MVC agrupa por controlador y encaja mejor cuando varias pantallas comparten lógica o cuando también hay API. Las dos usan Razor, Tag Helpers, validación y los **mismos casos de uso**: se pueden mezclar en la misma aplicación, como hace nuestro proyecto.

## 1.4 Conversión de la lógica de negocio

Lo que más trabajo da no son los controles: es **lo que hay dentro de los eventos**. En una página Web Forms típica, un `btnGuardar_Click` hace **todo**: leer los controles, validar, aplicar reglas, acceder a la base de datos y pintar el resultado.

La conversión consiste en **repartir** ese código por las capas del día 2:

```
 btnGuardar_Click (todo junto)                      ASP.NET Core (cada cosa en su sitio)
 ──────────────────────────────                     ────────────────────────────────────
 leer txtObservaciones.Text, ddlEstado...    ──▶    Web: model binding → ViewModel
 if (txtX == "") lblError.Text = "..."       ──▶    Web: [Required] en el ViewModel
 if (estado == 4 && hayPendientes) ...       ──▶    Domain: expediente.Cerrar() → Resultado
 SqlDataAdapter / SqlCommandBuilder          ──▶    Infrastructure: repositorio (EF Core)
 fila["FechaModificacion"] = DateTime.Now    ──▶    Application: reloj.GetUtcNow() (TimeProvider)
 lblMensaje.Text = "Guardado"                ──▶    Web: TempData + RedirectToAction
```

### Ejemplo completo: el `ExpedienteDetalle.aspx` de ayer

En el [Lab 3 del día 3](../day-03/labs/lab-03-analisis-sirei.md) analizamos este *code-behind*. Así quedaría convertido (es un esbozo: no está en el proyecto, pero sigue exactamente el patrón de `Incidencia`).

**1. Domain** — la regla escondida en `btnGuardar_Click` pasa a la entidad:

```csharp
public class Expediente
{
    private readonly List<Tramite> _tramites = [];

    public int Id { get; private set; }
    public string Numero { get; private set; } = string.Empty;
    public string? Observaciones { get; private set; }
    public EstadoExpediente Estado { get; private set; }            // antes: IdEstado = 4 (número mágico)
    public DateTimeOffset FechaModificacion { get; private set; }
    public IReadOnlyCollection<Tramite> Tramites => _tramites.AsReadOnly();

    public Resultado Actualizar(string? observaciones, EstadoExpediente nuevoEstado, DateTimeOffset ahora)
    {
        // La regla que estaba en el evento del botón: ahora se puede probar sin servidor ni base de datos.
        if (nuevoEstado == EstadoExpediente.Cerrado && _tramites.Any(t => t.Pendiente))
            return Resultado.Fallo("No se puede cerrar un expediente con trámites pendientes.", TipoError.Conflicto);

        Observaciones = observaciones;
        Estado = nuevoEstado;
        FechaModificacion = ahora;
        return Resultado.Ok();
    }
}
```

**2. Application** — el caso de uso (mismo patrón que `ModificarAsync` en `IncidenciaService`):

```csharp
public async Task<Resultado> ActualizarAsync(int id, ActualizarExpedienteComando comando, CancellationToken ct)
{
    var expediente = await repositorio.ObtenerPorIdAsync(id, ct);   // con sus trámites (Include)
    if (expediente is null)
        return Resultado.Fallo($"No existe el expediente {id}.", TipoError.NoEncontrado);

    var resultado = expediente.Actualizar(comando.Observaciones, comando.Estado, reloj.GetUtcNow());
    if (resultado.Exito)
        await repositorio.GuardarCambiosAsync(ct);                  // UPDATE parametrizado: adiós a la inyección SQL
    return resultado;
}
```

**3. Web** — el controlador sustituye a `Page_Load` y `btnGuardar_Click`:

```csharp
public class ExpedientesController(IExpedienteService servicio) : Controller
{
    // GET /Expedientes/Detalle/5    ← antes: ExpedienteDetalle.aspx?id=5 + Page_Load(!IsPostBack)
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        var expediente = await servicio.ObtenerAsync(id, ct);       // DTO proyectado, no un DataSet en Session
        return expediente is null ? NotFound() : View(new ExpedienteFormulario(expediente));
    }

    // POST /Expedientes/Detalle/5   ← antes: btnGuardar_Click
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Detalle(int id, ExpedienteFormulario formulario, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(formulario);

        var resultado = await servicio.ActualizarAsync(id, formulario.AComando(), ct);
        if (!resultado.Exito)
        {
            ModelState.AddModelError(string.Empty, resultado.Error!);   // antes: lblError.Text = ...
            return View(formulario);
        }

        TempData["Mensaje"] = "Expediente guardado.";                  // antes: lblMensaje.Text = "Guardado"
        return RedirectToAction(nameof(Detalle), new { id });          // Post-Redirect-Get
    }
}
```

Fíjate en lo que **ha desaparecido**: `ViewState["IdExpediente"]` (el id va en la ruta), `Session["ExpedienteActual"]` (se vuelve a cargar en el POST), la concatenación de SQL, `ConfigurationManager` y `DateTime.Now`.

> **¿Y si no hay tiempo de separar capas?** Se puede migrar "tal cual" a un controlador gordo y refactorizar después. Pero la regla de negocio, **como mínimo**, conviene sacarla a un método aparte desde el principio: es lo que más se rompe y lo que más se agradece poder probar (capítulo 8).

## 1.5 Recetas rápidas

| Situación en Web Forms | Cómo se hace en ASP.NET Core |
|---|---|
| Rellenar un desplegable en `Page_Load` | En la acción GET: `ViewBag.Categorias = new SelectList(...)` o una propiedad del ViewModel. **Volver a rellenarlo** si el POST no es válido (no viaja en el formulario) |
| Mantener lo escrito tras un error de validación | Automático: `return View(formulario)` con el mismo ViewModel, y `asp-for` repinta los valores |
| Mostrar u ocultar botones según el estado | `@if (incidencia.Estado == ...)` en la vista. **Y** la regla en el dominio: ocultar no es proteger |
| Confirmar antes de borrar (`OnClientClick="return confirm(...)"`) | Un `.js` en `wwwroot/js` que escucha el `submit` (la CSP del día 2 bloquea el JavaScript *inline*) |
| Formato de fechas en un `BoundField` (`DataFormatString`) | `@fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm")` o `[DisplayFormat]` en el ViewModel |
| Código repetido en varias páginas (`BasePage`) | Filtros, middleware, vistas parciales, View Components o servicios inyectados. Nada de heredar de una página base con lógica |
| `HttpContext.Current.User.Identity.Name` | `User.Identity.Name` en controladores y vistas; `IUsuarioActual` en la capa de aplicación (capítulo 4) |

## 1.6 Lo que vemos en la aplicación

El [Lab 1](labs/lab-01-busqueda-webforms.md) convierte una pantalla Web Forms completa (`BuscarIncidencias.aspx`, con `TextBox`, `DropDownList` con `AutoPostBack`, `GridView` paginado, `ViewState` y `Session`) en una búsqueda del listado de incidencias, atravesando todas las capas.

## Referencias

> Enlaces comprobados el 8 de octubre de 2026.

- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0) — Guía oficial; punto de entrada del Módulo 3.
- [Blazor para desarrolladores de ASP.NET Web Forms](https://learn.microsoft.com/es-es/dotnet/architecture/blazor-for-web-forms-developers/) — Libro gratuito de Microsoft; útil también para entender las equivalencias de conceptos.
- [Tag Helpers en formularios](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/working-with-forms?view=aspnetcore-10.0) — `asp-for`, `asp-items`, `asp-action`...
- [Validación de modelos](https://learn.microsoft.com/es-es/aspnet/core/mvc/models/validation?view=aspnetcore-10.0) — El sustituto de los controles *Validator*.
- [Vistas parciales](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/partial?view=aspnetcore-10.0) y [View Components](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/view-components?view=aspnetcore-10.0) — Los sustitutos de los `UserControl`.
- [Diseño (layout)](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/layout?view=aspnetcore-10.0) — El sustituto de las páginas maestras.
