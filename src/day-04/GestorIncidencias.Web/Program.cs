using System.Text.Json.Serialization;
using GestorIncidencias.Application;
using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Configuracion;
using GestorIncidencias.Infrastructure;
using GestorIncidencias.Web.Cache;
using GestorIncidencias.Web.Middleware;
using GestorIncidencias.Web.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WebMarkupMin.AspNetCoreLatest;

// =====================================================================
// 1) SERVICIOS
//    Program.cs es la "raíz de composición": aquí se juntan todas las capas.
//    Cada capa registra lo suyo con un método de extensión.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<IncidenciasOptions>()
    .BindConfiguration(IncidenciasOptions.Seccion)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddApplication();      // casos de uso       (GestorIncidencias.Application)
builder.Services.AddInfrastructure();   // EF Core, repositorio, consultas, caché e Identity (GestorIncidencias.Infrastructure)

// ---------------------------------------------------------------------
// Día 4 — AUTENTICACIÓN: ¿quién eres?
// AddIdentity (en AddInfrastructure) ya registra la cookie "Identity.Application" como esquema por defecto.
// Aquí se ajusta lo que depende de la web y se añade el esquema de token para la API.
// ---------------------------------------------------------------------
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = ".GestorIncidencias.Auth";
    o.LoginPath = "/Cuenta/Login";                    // sin sesión → 302 aquí (con ?ReturnUrl=...)
    o.LogoutPath = "/Cuenta/Logout";
    o.AccessDeniedPath = "/Cuenta/AccesoDenegado";    // con sesión pero sin permiso (403) → 302 aquí
    o.ExpireTimeSpan = TimeSpan.FromHours(8);         // una jornada de trabajo...
    o.SlidingExpiration = true;                       // ...que se renueva mientras haya actividad
    // Por defecto la cookie ya es HttpOnly (JavaScript no la puede leer → un XSS no puede robarla)
    // y SameSite=Lax (el navegador no la envía en POST desde otras webs → primera barrera contra CSRF).
});

// Token para la API (POST /api/cuenta/token). Lo usa IncidenciasApiController con [Authorize(AuthenticationSchemes = ...)].
builder.Services.AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme, o => o.BearerTokenExpiration = TimeSpan.FromHours(1));

// ---------------------------------------------------------------------
// Día 4 — AUTORIZACIÓN: ¿puedes hacer esto?
// FallbackPolicy = la que se aplica a todo endpoint SIN [Authorize] ni [AllowAnonymous].
// Resultado: SEGURO POR DEFECTO. Lo público hay que marcarlo explícitamente con [AllowAnonymous].
// ---------------------------------------------------------------------
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// Quién es el usuario actual, para la capa de Aplicación (autor de los comentarios, logs).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioActual, UsuarioActualHttp>();

// ---------------------------------------------------------------------
// Día 4 — ESTADO: sesión (equivalente a Session["..."] de Web Forms, ver Estado/HistorialVisitas.cs)
// La sesión se guarda en un IDistributedCache. AddDistributedMemoryCache = memoria de ESTE servidor:
// vale para un solo servidor; con varios, Redis o SQL Server (AddStackExchangeRedisCache...).
// ---------------------------------------------------------------------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = ".GestorIncidencias.Sesion";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromMinutes(20);   // como el timeout="20" de <sessionState> en Web.config
});

// MVC: controladores + vistas Razor. Incluye también los controladores API.
// Día 4: AutoValidateAntiforgeryToken = TODOS los POST/PUT/PATCH/DELETE validan el token antiforgery,
// aunque alguien olvide poner [ValidateAntiForgeryToken] en una acción nueva. Red de seguridad frente a CSRF.
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Razor Pages: páginas con su PageModel (carpeta Pages/). Validan el token antiforgery siempre, sin configurar nada.
builder.Services.AddRazorPages();

// Minificación del HTML generado por Razor (quita espacios, comentarios, comillas innecesarias...).
// Por defecto WebMarkupMin no actúa en Development; lo activamos para poder verlo en el curso.
// DisablePoweredByHttpHeaders: no enviar "X-HTML-Minification-Powered-By: WebMarkupMin"
// (no aporta nada al cliente y revela qué librería usa el servidor).
builder.Services.AddWebMarkupMin(o =>
    {
        o.AllowMinificationInDevelopmentEnvironment = true;
        o.DisablePoweredByHttpHeaders = true;
    })
    .AddHtmlMinification();

// Output Cache: guarda en memoria del servidor el HTML ya minificado (ver Cache/CacheHtmlPolicy.cs).
// Día 4: solo para usuarios ANÓNIMOS (la política no guarda ni sirve caché a usuarios autenticados).
builder.Services.AddOutputCache(o => o.AddPolicy(CacheHtmlPolicy.Nombre, new CacheHtmlPolicy()));

// ---------------------------------------------------------------------
// Día 4 — LOGGING de peticiones HTTP: UNA línea por petición con método, ruta, código y duración.
// No se registran cabeceras ni cuerpos: llevarían cookies, tokens y datos personales.
// Se activa con la categoría Microsoft.AspNetCore.HttpLogging en appsettings.json.
// ---------------------------------------------------------------------
builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.RequestPath
                    | HttpLoggingFields.ResponseStatusCode | HttpLoggingFields.Duration;
    o.CombineLogs = true;
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// =====================================================================
// 2) PIPELINE
// =====================================================================
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");   // página de error amigable (vista Razor); el error se escribe en el log
    app.UseHsts();
}

app.UseCorrelationId();
app.UseContentSecurityPolicy();   // CSP solo en las páginas (text/html): bloquea JS y CSS inline (política en appsettings.json)
app.UseHttpsRedirection();
app.UseRouting();

// Después de UseRouting: así respeta .WithHttpLogging(...) de los endpoints (ver /salud y los estáticos).
// Y dentro del ámbito de UseCorrelationId: cada línea lleva el CorrelationId.
app.UseHttpLogging();

// ORDEN IMPORTANTE (día 4):
//   UseAuthentication → lee la cookie o el token y rellena HttpContext.User.
//   UseAuthorization  → decide si ese usuario puede llegar al endpoint (401/403 o redirección al login).
// Los dos van DESPUÉS de UseRouting (hay que saber qué endpoint es para conocer su [Authorize])
// y ANTES de UseOutputCache: una página guardada en caché nunca debe saltarse la autorización.
app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

// ORDEN IMPORTANTE: UseOutputCache va ANTES que UseWebMarkupMin.
//   - En la ida, si hay acierto de caché se responde aquí y no se ejecuta nada de lo que sigue.
//   - En la vuelta, WebMarkupMin ya ha minificado, así que lo que se guarda es el HTML minificado.
app.UseOutputCache();

// Cualquier POST/PUT/PATCH/DELETE que termine bien puede haber cambiado incidencias:
// se vacía la caché ANTES de enviar la respuesta (p. ej. el 302 del Post-Redirect-Get).
app.Use(async (context, next) =>
{
    if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        context.Response.OnStarting(async () =>
        {
            if (context.Response.StatusCode < 400)
            {
                var cache = context.RequestServices.GetRequiredService<IOutputCacheStore>();
                await cache.EvictByTagAsync(CacheHtmlPolicy.Etiqueta, default);
            }
        });
    }
    await next(context);
});

// Debe ir antes de los endpoints que generan HTML (MVC y Razor Pages).
app.UseWebMarkupMin();

// Las URLs con huella (site.min.x4wtre2m4d.css) se sirven con "Cache-Control: max-age=31536000, immutable":
// el navegador nunca revalida, así que ETag y Last-Modified sobran. Se quitan solo en esas respuestas;
// las URLs sin huella (no-cache) los conservan porque los necesitan para responder 304.
app.Use((context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        if (headers.CacheControl.ToString().Contains("immutable"))
        {
            headers.Remove(Microsoft.Net.Http.Headers.HeaderNames.ETag);
            headers.Remove(Microsoft.Net.Http.Headers.HeaderNames.LastModified);
        }
        return Task.CompletedTask;
    });
    return next(context);
});

// Ficheros de wwwroot optimizados (compresión, caché, huella en el nombre). Sustituye a UseStaticFiles.
// Día 4: MapStaticAssets crea ENDPOINTS, así que también les afecta la FallbackPolicy. Sin AllowAnonymous,
// la página de login se vería sin estilos (el CSS también exigiría haber iniciado sesión).
app.MapStaticAssets()
    .AllowAnonymous()
    .WithHttpLogging(HttpLoggingFields.None);   // no llenar el log con una línea por cada CSS

// Comprobación de salud para el balanceador o la monitorización: 200 "Healthy" o 503 "Unhealthy".
// Pública (el balanceador no inicia sesión) y sin log (se consulta cada pocos segundos).
app.MapHealthChecks("/salud")
    .AllowAnonymous()
    .WithHttpLogging(HttpLoggingFields.None);

// Ruta CONVENCIONAL de MVC: /{controlador}/{acción}/{id?}
//   /                          → HomeController.Index
//   /Incidencias               → IncidenciasController.Index
//   /Incidencias/Detalle/3     → IncidenciasController.Detalle(id: 3)
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .CacheOutput(CacheHtmlPolicy.Nombre);

// OJO: .WithStaticAssets() encadenado a MapControllerRoute NO hace nada (en .NET 10 ese builder no lleva
// la referencia al IEndpointRouteBuilder que necesita) y las vistas MVC enlazarían site.min.css SIN huella.
// MapControllers() devuelve el builder común a todos los controladores, donde sí funciona
// (los [ApiController] se excluyen solos). No duplica rutas: es la misma fuente de endpoints.
app.MapControllers()
    .WithStaticAssets();

// Razor Pages: la ruta sale de la ubicación del fichero (/Paginas/Incidencias → Pages/Paginas/Incidencias/Index.cshtml).
app.MapRazorPages()
    .WithStaticAssets()
    .CacheOutput(CacheHtmlPolicy.Nombre);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi()        // /openapi/v1.json (solo documenta los controladores API)
        .AllowAnonymous();
}

app.Run();

// Las pruebas de integración (WebApplicationFactory<Program>) necesitan poder referenciar esta clase.
public partial class Program;
