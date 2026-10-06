using System.Text.Json.Serialization;
using GestorIncidencias.Application;
using GestorIncidencias.Application.Configuracion;
using GestorIncidencias.Infrastructure;
using GestorIncidencias.Web.Cache;
using GestorIncidencias.Web.Middleware;
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
builder.Services.AddInfrastructure();   // EF Core, repositorio (GestorIncidencias.Infrastructure)

// MVC: controladores + vistas Razor. Incluye también los controladores API.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Razor Pages: páginas con su PageModel (carpeta Pages/).
builder.Services.AddRazorPages();

// Minificación del HTML generado por Razor (quita espacios, comentarios, comillas innecesarias...).
// Por defecto WebMarkupMin no actúa en Development; lo activamos para poder verlo en el curso.
builder.Services.AddWebMarkupMin(o => o.AllowMinificationInDevelopmentEnvironment = true)
    .AddHtmlMinification();

// Output Cache: guarda en memoria del servidor el HTML ya minificado (ver Cache/CacheHtmlPolicy.cs).
builder.Services.AddOutputCache(o => o.AddPolicy(CacheHtmlPolicy.Nombre, new CacheHtmlPolicy()));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// =====================================================================
// 2) PIPELINE
// =====================================================================
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");   // página de error amigable (vista Razor)
    app.UseHsts();
}

app.UseCorrelationId();
app.UseHttpsRedirection();
app.UseRouting();

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
app.MapStaticAssets();

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
    app.MapOpenApi();   // /openapi/v1.json (solo documenta los controladores API)
}

app.Run();
