using System.Text.Json.Serialization;
using GestorIncidencias.Application;
using GestorIncidencias.Application.Configuracion;
using GestorIncidencias.Infrastructure;
using GestorIncidencias.Web.Middleware;

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

// Ficheros de wwwroot optimizados (compresión, caché, huella en el nombre). Sustituye a UseStaticFiles.
app.MapStaticAssets();

// Ruta CONVENCIONAL de MVC: /{controlador}/{acción}/{id?}
//   /                          → HomeController.Index
//   /Incidencias               → IncidenciasController.Index
//   /Incidencias/Detalle/3     → IncidenciasController.Detalle(id: 3)
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Razor Pages: la ruta sale de la ubicación del fichero (/Paginas/Incidencias → Pages/Paginas/Incidencias/Index.cshtml).
app.MapRazorPages()
    .WithStaticAssets();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();   // /openapi/v1.json (solo documenta los controladores API)
}

app.Run();
