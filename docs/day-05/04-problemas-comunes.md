# 4. Resolución de problemas comunes

Los errores de una migración se repiten: casi siempre son los mismos treinta. Este capítulo es la **chuleta** para reconocerlos por el síntoma. Primero, cómo diagnosticar; después, el catálogo.

## 4.1 Caja de herramientas de diagnóstico

| Herramienta | Qué te dice | Cómo |
|---|---|---|
| **Página de excepción para desarrolladores** | La excepción, la pila, la consulta, las cabeceras, la ruta | Automática en `Development`. Si no la ves, revisa `ASPNETCORE_ENVIRONMENT` |
| **Log de la consola** | Una línea por petición (HTTP logging) con código y duración; tus `[LoggerMessage]`; los avisos del framework | Sube el nivel de una categoría en `appsettings.Development.json`: `"Microsoft.AspNetCore": "Information"` |
| **CorrelationId** | Todas las líneas de una misma petición | Cabecera `X-Correlation-Id` de la respuesta (día 1) → búscala en el log |
| **F12 → Red** | Qué se pidió, con qué verbo, qué código volvió, qué cookies viajaron, a dónde redirigió | Marca *Preservar registro* para no perder las redirecciones |
| **F12 → Consola** | Errores de JavaScript y **violaciones de la CSP** | "Refused to execute inline script because it violates..." |
| **F12 → Aplicación** | Cookies (`.GestorIncidencias.Auth`, `.Sesion`, antiforgery), `sessionStorage` | — |
| **Fichero `.http`** | Repetir una petición a mano, con o sin token | REST Client / Visual Studio |
| **Pruebas** | Si lo que funcionaba sigue funcionando | `dotnet test`; para una pantalla migrada, una prueba de integración que la recorra |
| **`/salud`** | ¿Responden las bases de datos? | `curl http://localhost:5196/salud` |
| **ToQueryString / log de EF Core** | El SQL que genera una consulta | `consulta.ToQueryString()` o `"Microsoft.EntityFrameworkCore.Database.Command": "Information"` |

> **Método:** 1) reproduce el fallo; 2) mira el **código HTTP** (F12 o log) — dice en qué capa buscar; 3) lee el mensaje **entero** (las excepciones de ASP.NET Core suelen decir la solución); 4) cambia una sola cosa cada vez.

| Código | Suele significar | Mira primero |
|---|---|---|
| **404** | Ninguna ruta coincide | Rutas, `[Area]`, nombre del controlador/acción, verbo (`[HttpPost]` y llega un GET) |
| **405** | La ruta existe, pero no con ese verbo | `[HttpGet]`/`[HttpPost]`, `method` del formulario |
| **400** | El *model binding*, la validación o el **antiforgery** han rechazado la petición | Token antiforgery, tipos de los parámetros, `[ApiController]` |
| **401 / 302 al login** | No hay usuario autenticado | Cookie, token, orden de `UseAuthentication` |
| **403 / 302 a AccesoDenegado** | Hay usuario, pero no cumple la política | Roles del usuario, nombre de la política |
| **500** | Excepción no controlada | Página de excepción / log |
| **500.30 / 500.31 / 502.5** (IIS) | La aplicación no ha llegado a arrancar | Visor de eventos de Windows, `stdoutLogEnabled`, *runtime* instalado (ver 4.8) |

## 4.2 Arranque, configuración e inyección de dependencias

| Síntoma | Causa | Solución |
|---|---|---|
| `Unable to resolve service for type 'IExpedienteService' while attempting to activate 'ExpedientesController'` | El servicio no está registrado | `services.AddScoped<IExpedienteService, ExpedienteService>()` en el `AddXxx()` de su capa |
| `Cannot consume scoped service 'IncidenciasDbContext' from singleton '...'` | Un singleton (o un `IHostedService`) recibe un servicio Scoped en el constructor | Inyectar `IServiceScopeFactory` y crear un ámbito (como `InicializadorBaseDatos`) |
| `OptionsValidationException` al arrancar | `ValidateOnStart` ha encontrado una opción inválida | El mensaje dice qué propiedad; revisa `appsettings.{Entorno}.json` |
| Un valor de configuración "no cambia" | Se está leyendo de otro fuente con más prioridad (variable de entorno, secretos de usuario, `appsettings.{Entorno}.json`) | Orden: appsettings → appsettings.{Entorno} → secretos → variables de entorno → línea de comandos |
| `ConfigurationManager.AppSettings["X"]` devuelve `null` | En .NET moderno no se lee `Web.config` | `IConfiguration` / `IOptions<T>` |
| `HttpContext.Current` no existe | No hay contexto estático en ASP.NET Core | `IHttpContextAccessor` en Web, o mejor un puerto (`IUsuarioActual`) |
| `Server.MapPath` no existe | — | `IWebHostEnvironment.ContentRootPath` / `WebRootPath` |
| `Encoding.GetEncoding(1252)` lanza `NotSupportedException` | .NET moderno solo trae UTF-8/UTF-16/ASCII/Latin1 de serie | `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` al arrancar |
| Acentos rotos (`ExpedienteDetalle.aspx.cs` copiado: "trÃ¡mite") | Ficheros legacy guardados en **Windows-1252** abiertos como UTF-8 | Convertir a UTF-8 al copiarlos (VS: *Guardar con codificación*; VS Code: *Volver a abrir con codificación*) |

## 4.3 Rutas, áreas y vistas

| Síntoma | Causa | Solución |
|---|---|---|
| 404 en `/Sirei/Expedientes` | Falta `[Area("Sirei")]` en el controlador, o la ruta del área | `[Area]` + `MapAreaControllerRoute(...)` **antes** de la ruta `default` |
| `InvalidOperationException: The view 'Index' was not found. The following locations were searched: /Views/Expedientes/Index.cshtml...` | Sin `[Area]`, MVC busca las vistas fuera del área | Mismo arreglo; la lista de rutas buscadas es la pista |
| En un área, `asp-action` y `asp-for` salen **tal cual** en el HTML | El área no tiene `_ViewImports.cshtml` (o le falta `@addTagHelper`) | Copiar `_ViewImports.cshtml` y `_ViewStart.cshtml` a `Areas/X/Views/` |
| Desde una página de un área, los enlaces del menú tienen `href=""` | Heredan el área actual (`asp-area` ambiental) y esa ruta no existe | `asp-area=""` en los enlaces del layout que no son del área |
| La vista no tiene estilos ni menú | Falta `_ViewStart.cshtml` en el área (no hay `Layout`) | `@{ Layout = "_Layout"; }` |
| `@Html.Action("Menu")` no compila | No existe en ASP.NET Core | View Component: `@await Component.InvokeAsync("Menu")` |
| `@helper` o `App_Code/*.cshtml` no compilan | No existen | Vista parcial, Tag Helper o función en `@functions` |
| `@Styles.Render` / `@Scripts.Render` | No existe el *bundling* de System.Web.Optimization | `<link href="~/css/site.min.css" asp-append-version="true" />` |
| Los acentos salen como `&#xE1;` en el **código fuente** de la página | Razor codifica todo lo que no es ASCII básico. En pantalla se ve bien | Es correcto. Si molesta: `services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All))` |

## 4.4 Formularios y *model binding*

| Síntoma | Causa | Solución |
|---|---|---|
| **400** al enviar un formulario | Falta el token antiforgery: `<form>` escrito a mano con `action="..."` (el Tag Helper solo añade el token si **no** hay `action` explícito) | `asp-action`/`asp-controller` en lugar de `action`, o `@Html.AntiForgeryToken()` |
| 400 en una petición `fetch` POST | No envía el token | Leerlo del formulario (`__RequestVerificationToken`) y mandarlo en la cabecera `RequestVerificationToken` |
| El parámetro llega a `0` o `null` | El `name` del campo no coincide con el parámetro o la propiedad | Comparar los `name` del HTML (F12 → Red → Carga útil) con el código. Con prefijos: `[Bind(Prefix = "...")]` |
| `ModelState` no válido "sin motivo" | Una propiedad `string` no anulable es `[Required]` implícitamente (`Nullable` habilitado) | `string?` si es opcional |
| Decimales: `3,5` se lee como `35` o falla | La query string se enlaza con cultura **invariable** (punto); el formulario, con la **actual** | `UseRequestLocalization` con `es-ES` y, en la query string, usar punto |
| Casillas de verificación múltiples llegan vacías | No comparten `name` o no tienen `value` | `name="LoteIds" value="@id"` → `List<int> LoteIds` |
| Tras un error de validación, el desplegable aparece vacío | Las opciones no viajan en el POST | Volver a cargarlas antes de `return View(...)` |
| `[Bind(Include = "...")]` no compila | Sintaxis de MVC 5 | `[Bind("Titulo,Descripcion")]` — mejor, un ViewModel con solo esos campos |

## 4.5 Seguridad y sesión

| Síntoma | Causa | Solución |
|---|---|---|
| Bucle de redirecciones al login | La página de login (o su CSS) exige autenticación | `[AllowAnonymous]` en el login; `MapStaticAssets().AllowAnonymous()` |
| `[Authorize]` en un *handler* de Razor Pages "no hace nada" | Solo funciona en la clase | `IAuthorizationService.AuthorizeAsync` dentro del *handler* (Lab 2 del día 4) |
| `InvalidOperationException: The AuthorizationPolicy named 'X' was not found` | Política usada y no registrada | `AddPolicy(...)` en Program.cs |
| El AJAX pinta la página de **login** dentro de la tabla | `fetch` sigue el 302 y recibe el login con 200 | Cabecera `X-Requested-With: XMLHttpRequest` → la cookie responde 401 |
| `Session has not been configured for this application` | Falta `AddSession` / `UseSession` | Registrarlos (día 4). Y preguntarse si hace falta la sesión |
| `Session["X"] = objeto` no compila | La sesión guarda bytes/texto | Serializar a JSON (`SetString`), y guardar poco |
| La sesión se pierde con varios servidores | `AddDistributedMemoryCache` es memoria local | Redis / SQL Server como `IDistributedCache` |
| La cookie TempData es enorme o da 400 | Se guarda demasiado en `TempData` | Solo mensajes cortos |

## 4.6 Datos (EF6 / ADO.NET → EF Core)

| Síntoma | Causa | Solución |
|---|---|---|
| Una regla que depende de los hijos **no salta** | Falta `Include`: la colección llega vacía (EF Core no tiene *lazy loading* activado por defecto) | `Include(...)` en el repositorio que carga el agregado |
| `NullReferenceException` en una navegación (`lote.Remesa.Nombre`) | Lo mismo: navegación no cargada | `Include` o proyección (`l.Remesa != null ? l.Remesa.Nombre : null`) |
| `The LINQ expression ... could not be translated` | Método de C# que no tiene traducción a SQL (o `GroupBy` que EF6 evaluaba en memoria) | Reescribir la consulta, o `AsEnumerable()` **después** de filtrar y paginar |
| La página tarda segundos | `ToList()` antes de `Where`, consultas en un bucle (N+1), sin paginar | Filtros en el `IQueryable`, `Contains` con lista de ids, `Skip/Take` (día 3) |
| `DbUpdateConcurrencyException` | Otro proceso cambió la fila (token de concurrencia) | Avisar al usuario y recargar (como `ExpedienteService`) |
| Los cambios no se guardan | La entidad se leyó con `AsNoTracking` o con una proyección | Cargar con el repositorio (con seguimiento) para escribir |
| Fechas desplazadas 1 o 2 horas | Legacy en hora local, código nuevo en UTC | `ValueConverter` en un único sitio |
| En pruebas todo va bien y en SQL Server falla (o al revés) | **InMemory** no es relacional: no comprueba claves ajenas, distingue mayúsculas, no traduce SQL | Pruebas de integración contra SQL Server (Testcontainers / LocalDB) antes de publicar |
| Error al leer filas antiguas: `Data is Null` | `IsRequired()` en una columna que en la BD admite `NULL` | Ajustar la configuración al esquema real |

## 4.7 JavaScript, CSP y estáticos

| Síntoma | Causa | Solución |
|---|---|---|
| Consola: *Refused to execute inline script because it violates the following Content Security Policy directive: "script-src 'self'"* | `<script>` inline (con o sin Razor) | Mover a `wwwroot/js/...` y datos en `data-*` |
| Consola: *Refused to execute inline event handler* | `onclick="..."` | `addEventListener` (delegación de eventos) |
| Consola: *Refused to apply inline style* | `style="..."` | Clase CSS |
| El JS lee `undefined` donde antes había un valor | El servidor ahora devuelve JSON en **camelCase** (`ok`, no `Ok`) | Ajustar el JS, o `AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null)` mientras se migra |
| 404 en `/Scripts/jquery.js` o `/Content/site.css` | En ASP.NET Core los estáticos viven en `wwwroot` | Mover a `wwwroot/js`, `wwwroot/css` |
| Un cambio en el `.js` no se ve | Caché del navegador | `asp-append-version="true"` (con `MapStaticAssets` el nombre lleva huella) |
| `$ is not defined` | Se usaba jQuery del layout de MVC 5 | Incluirlo, o (mejor) reescribir con `fetch`/`querySelector` |

## 4.8 Publicación en IIS

| Síntoma | Causa | Solución |
|---|---|---|
| **HTTP 500.19** | Falta el *ASP.NET Core Hosting Bundle* (el módulo ANCM) | Instalar el *Hosting Bundle* de .NET 10 y reiniciar IIS |
| **HTTP 500.30** *In-process start failure* | La aplicación lanza una excepción al arrancar (configuración, BD, opciones) | `stdoutLogEnabled="true"` en el `web.config` generado, o Visor de eventos |
| **HTTP 500.31** *Failed to locate .NET runtime* | Falta el *runtime* de .NET 10 en el servidor | Instalar el *Hosting Bundle* (incluye el *runtime*) |
| Funciona en local y en el servidor sale "Development" | Variable `ASPNETCORE_ENVIRONMENT` | `<environmentVariable>` en `web.config` o en el *pool* |
| Subir ficheros grandes da 413 / 404.13 | Límite de Kestrel/IIS (30 MB por defecto) | `[RequestSizeLimit]` + `maxAllowedContentLength` en `web.config` |
| Sin acceso a `\\ficheros01\...` | La identidad del *pool* no tiene permisos en la carpeta compartida | Cuenta de servicio con permisos, o almacenamiento distinto |

## Referencias

> Enlaces comprobados el 9 de octubre de 2026.

- [Control de errores en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
- [Solución de problemas de ASP.NET Core en IIS](https://learn.microsoft.com/es-es/aspnet/core/test/troubleshoot-azure-iis?view=aspnetcore-10.0)
- [Prevención de ataques CSRF (incluye AJAX)](https://learn.microsoft.com/es-es/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [Enlace de modelos y cultura](https://learn.microsoft.com/es-es/aspnet/core/mvc/models/model-binding?view=aspnetcore-10.0#globalization-behavior-of-model-binding-route-data-and-query-strings)
- [Carga de datos relacionados (EF Core)](https://learn.microsoft.com/es-es/ef/core/querying/related-data/)
- [Limitaciones del proveedor InMemory](https://learn.microsoft.com/es-es/ef/core/testing/choosing-a-testing-strategy)
- [Content Security Policy (MDN)](https://developer.mozilla.org/es/docs/Web/HTTP/CSP)
