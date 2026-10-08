# 3. Migración desde ASP.NET MVC 5 y herramientas de Microsoft

**Noticom** es una aplicación ASP.NET MVC 5 (.NET Framework). La buena noticia: MVC 5 y ASP.NET Core MVC comparten **las mismas ideas** (controladores, acciones, vistas Razor, *model binding*, filtros). La migración es sobre todo **mecánica**: cambia el "esqueleto" del proyecto y la infraestructura de `System.Web`, mucho menos el código de controladores y vistas.

## 3.1 Qué se parece y qué no

```
 MVC 5 (.NET Framework 4.x)                      ASP.NET Core MVC (.NET 10)
 ─────────────────────────────                   ────────────────────────────
 Global.asax  Application_Start                  Program.cs
 App_Start/RouteConfig.cs                          app.MapControllerRoute(...)
 App_Start/FilterConfig.cs                         AddControllersWithViews(o => o.Filters.Add(...))
 App_Start/BundleConfig.cs                         wwwroot + MapStaticAssets (+ minificación al compilar, día 2)
 App_Start/WebApiConfig.cs                         (Web API ya ES MVC: [ApiController])
 App_Start/UnityConfig.cs  (o Autofac, Ninject)    builder.Services.Add...  (DI integrada, día 1)
 Startup.cs (OWIN: cookies, Identity 2)            builder.Services.AddIdentity / AddAuthentication
 Web.config  <appSettings>, <connectionStrings>    appsettings.json + IOptions (día 1)
 Views/Web.config  (espacios de nombres)           Views/_ViewImports.cshtml
 packages.config                                   <PackageReference> en el .csproj
 Content/, Scripts/                                wwwroot/
 System.Web.dll (HttpContext, HttpRequest...)      Microsoft.AspNetCore.Http (otro modelo)
```

### Controladores

| MVC 5 | ASP.NET Core |
|---|---|
| `using System.Web.Mvc;` | `using Microsoft.AspNetCore.Mvc;` |
| `ActionResult` | `IActionResult` (o `ActionResult<T>` en API) |
| `return HttpNotFound();` | `return NotFound();` |
| `return new HttpStatusCodeResult(400);` | `return BadRequest();` / `StatusCode(400)` |
| `return Json(datos, JsonRequestBehavior.AllowGet);` | `return Json(datos);` — ⚠️ con **System.Text.Json** y en **camelCase** por defecto (MVC 5 usaba Newtonsoft y PascalCase): el JavaScript que lee esas respuestas puede romperse |
| `[Bind(Include = "Titulo,Descripcion")]` | `[Bind("Titulo,Descripcion")]` — mejor aún: un ViewModel solo con esos campos |
| `[ChildActionOnly]` + `@Html.Action("Menu")` | **View Component** + `@await Component.InvokeAsync("Menu")` |
| `[HandleError]` | `UseExceptionHandler` (middleware) |
| `[OutputCache(Duration = 60)]` | `[OutputCache]` del middleware de Output Cache (día 2) o `[ResponseCache]` |
| `ApiController` (Web API 2) + `IHttpActionResult` | `ControllerBase` + `[ApiController]` + `ActionResult<T>` |
| `Request.QueryString["x"]`, `Request.Form["x"]` | Parámetros de la acción (*model binding*); si hace falta, `Request.Query["x"]` |
| `HttpContext.Current.User` | `User` (propiedad del controlador) |
| `Server.MapPath("~/App_Data/x.txt")` | `IWebHostEnvironment.ContentRootPath` / `WebRootPath` |

### Vistas

| MVC 5 | ASP.NET Core |
|---|---|
| `@Html.BeginForm(...)`, `@Html.TextBoxFor(...)` | **Siguen funcionando**. Los Tag Helpers (`asp-for`) son la forma recomendada |
| `@Html.Partial("_X")` | `<partial name="_X" />` (o `await Html.PartialAsync`) |
| `@Html.Action("Menu", "Home")` | `@await Component.InvokeAsync("Menu")` |
| `@Styles.Render("~/Content/css")`, `@Scripts.Render(...)` | `<link href="~/css/site.min.css" asp-append-version="true" />` |
| `@helper Fila(...) { ... }` y `App_Code/*.cshtml` | **No existen**. Vistas parciales, Tag Helpers o funciones locales en un bloque `@functions` |
| `@Url.Content("~/...")` | `~/` en atributos `src`/`href` (lo resuelve Razor) |
| Espacios de nombres en `Views/Web.config` | `@using` en `_ViewImports.cshtml` |

### Datos

Noticom usa **EF6**. Dos caminos:

1. **Migrar también a EF Core** (lo del día 3). Es lo recomendable a medio plazo.
2. **Mantener EF6 de momento**: EF 6.3 y posteriores funcionan en .NET moderno, así que se puede migrar **primero la web** y después el acceso a datos. Los modelos **EDMX** (diseñador visual) dan más trabajo: el diseñador solo funciona en proyectos .NET Framework.

## 3.2 Pasos para migrar Noticom

Para una aplicación MVC 5 pequeña o mediana (como Noticom), el enfoque habitual es **crear un proyecto nuevo y llevar el código**, no "convertir" el proyecto viejo:

| # | Paso | Qué hacer |
|---|---|---|
| 1 | **Inventario** | Igual que con SIREI (Lab 3 del día 3): dependencias NuGet sin versión moderna, uso de `System.Web`, autenticación, EF6/EDMX |
| 2 | **Bibliotecas primero** | Las capas que no dependen de `System.Web` (modelo, servicios) se pasan a **SDK-style** con `<TargetFrameworks>net48;net10.0</TargetFrameworks>`: siguen sirviendo a la app vieja y ya sirven a la nueva |
| 3 | **Proyecto nuevo** | `dotnet new mvc` (o partir de la estructura de `src/day-04`) |
| 4 | **Configuración** | `Web.config` → `appsettings.json` + clases de opciones; `Global.asax` y `App_Start` → `Program.cs` |
| 5 | **Controladores** | Copiar, cambiar `using`, corregir los tipos de retorno (tabla 3.1). Lo que use `HttpContext.Current` se cambia por inyección |
| 6 | **Vistas** | Copiar; `Views/Web.config` → `_ViewImports.cshtml`; sustituir `@Styles.Render`/`@Scripts.Render`, `@Html.Action`, `@helper` |
| 7 | **Estáticos** | `Content/` y `Scripts/` → `wwwroot/` |
| 8 | **Autenticación** | ASP.NET Identity 2 / OWIN → ASP.NET Core Identity (capítulo 5.4) |
| 9 | **Probar** | Pantalla a pantalla, comparando con la aplicación vieja. Las pruebas de integración (capítulo 8) ayudan mucho |

Si Noticom es grande o no puede congelarse, se aplica la misma estrategia **incremental** que para SIREI: YARP delante, rutas que se van pasando a la aplicación nueva, System.Web adapters para compartir sesión y autenticación.

## 3.3 Herramientas oficiales de Microsoft

| Herramienta | Para qué sirve | Estado (octubre de 2026) |
|---|---|---|
| **GitHub Copilot upgrade** (antes "app modernization") | Agente en Visual Studio 2026 / VS Code: analiza la solución, propone un plan de actualización y aplica cambios; incluye rutas desde .NET Framework | **La que recomienda Microsoft** |
| **.NET Upgrade Assistant** | Asistente para actualizar proyectos | **Obsoleta** oficialmente; seguiréis viéndola en artículos y vídeos |
| **System.Web adapters** (`Microsoft.AspNetCore.SystemWebAdapters`) | Migración incremental: sesión y autenticación compartidas, y una API parecida a `System.Web` (`HttpContext.Current` "de transición") para bibliotecas compartidas | Activa |
| **YARP** (`Yarp.ReverseProxy`) | Proxy inverso dentro de la aplicación ASP.NET Core: lo no migrado se reenvía al legacy | Activa |
| **Analizadores de .NET** (en el SDK) | Avisan de APIs obsoletas o no disponibles al compilar para `net10.0` | Activos |
| Guía *Migración de ASP.NET Framework a ASP.NET Core* | Guía de decisión, áreas técnicas (sesión, autenticación, módulos, handlers, configuración...) y ejemplos | Documentación viva |

> **Qué esperar de las herramientas:** hacen bien lo **mecánico** (formato del proyecto, paquetes, espacios de nombres, configuración, sugerencias de código). No deciden la arquitectura, no reescriben la lógica de negocio de un *code-behind* y no sustituyen a los controles de terceros. Revisad **cada** cambio que propongan como si fuera el *pull request* de un compañero.

### Una conversión "a mano" en 30 segundos

```csharp
// MVC 5 (Noticom)
public class NoticiasController : Controller
{
    private readonly NoticomEntities db = new NoticomEntities();          // contexto creado a mano

    public ActionResult Detalle(int? id)
    {
        if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        var noticia = db.Noticias.Find(id);
        if (noticia == null) return HttpNotFound();
        ViewBag.Usuario = System.Web.HttpContext.Current.User.Identity.Name;
        return View(noticia);                                             // la entidad EF va a la vista
    }
}
```

```csharp
// ASP.NET Core (.NET 10)
public class NoticiasController(INoticiaService servicio) : Controller   // dependencia inyectada
{
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)   // id en la ruta: /Noticias/Detalle/5
    {
        var noticia = await servicio.ObtenerAsync(id, ct);                    // DTO, no la entidad
        return noticia is null ? NotFound() : View(noticia);                  // User.Identity.Name ya está en la vista
    }
}
```

## Referencias

> Enlaces comprobados el 8 de octubre de 2026.

- [Actualizar de ASP.NET MVC, Web API y Web Forms a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/tooling?view=aspnetcore-10.0) — Herramientas y pasos de actualización.
- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0) — Guía de decisión y áreas técnicas.
- [Información general sobre la actualización de GitHub Copilot](https://learn.microsoft.com/es-es/dotnet/core/porting/github-copilot-upgrade/overview)
- [Introducción al Asistente para actualización de .NET](https://learn.microsoft.com/es-es/dotnet/core/porting/upgrade-assistant-overview) — Marcado como obsoleto.
- [Adaptadores System.Web](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/inc/systemweb-adapters?view=aspnetcore-10.0)
- [Comparar EF Core y EF6](https://learn.microsoft.com/es-es/ef/efcore-and-ef6/) y [Portar de EF6 a EF Core](https://learn.microsoft.com/es-es/ef/efcore-and-ef6/porting/)
- [View Components](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/view-components?view=aspnetcore-10.0) — Sustituto de las acciones hijas (`@Html.Action`).
