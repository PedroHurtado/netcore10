using System.Text.Json.Serialization;
using GestorIncidencias.Web.Data;
using GestorIncidencias.Web.Endpoints;
using GestorIncidencias.Web.Lifetimes;
using GestorIncidencias.Web.Middleware;
using GestorIncidencias.Web.Options;
using GestorIncidencias.Web.Services;
using Microsoft.EntityFrameworkCore;

// =====================================================================
// 1) BUILDER: configuración del host, de la configuración y de los servicios (DI)
// =====================================================================
var builder = WebApplication.CreateBuilder(args);

// --- Configuración --------------------------------------------------
// CreateBuilder ya carga, por orden (la última fuente gana):
//   appsettings.json → appsettings.{Entorno}.json → User Secrets (solo Development)
//   → variables de entorno → argumentos de línea de comandos.
// Aquí añadimos una fuente opcional más, útil para pruebas locales que no se suben a git.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// --- Opciones (patrón Options) ---------------------------------------
builder.Services
    .AddOptions<IncidenciasOptions>()
    .Bind(builder.Configuration.GetSection(IncidenciasOptions.Seccion))
    .ValidateDataAnnotations()   // valida [Required], [Range]...
    .ValidateOnStart();          // falla al arrancar si la configuración es incorrecta

// --- Servicios del framework -----------------------------------------
builder.Services.AddOpenApi();         // documento OpenAPI en /openapi/v1.json
builder.Services.AddProblemDetails();  // errores en formato estándar RFC 9457
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter())); // "Alta" en lugar de 2

// --- Datos: EF Core con base de datos en memoria -----------------------
// AddDbContext registra el DbContext como Scoped (una instancia por petición).
builder.Services.AddDbContext<IncidenciasDbContext>(opt =>
    opt.UseInMemoryDatabase("GestorIncidencias"));

// --- Nuestros servicios (inyección de dependencias) ---------------------
builder.Services.AddSingleton(TimeProvider.System);                         // reloj del sistema
builder.Services.AddScoped<IIncidenciaRepository, EfIncidenciaRepository>(); // depende del DbContext (Scoped)
builder.Services.AddScoped<IIncidenciaService, IncidenciaService>();        // lógica de negocio
builder.Services.AddHostedService<DatosDemoInitializer>();                   // carga de datos al arrancar

// Middleware por factoría (IMiddleware) → debe registrarse en el contenedor.
builder.Services.AddTransient<TiempoRespuestaMiddleware>();

// Demo de lifetimes: la misma clase con tres tiempos de vida distintos.
builder.Services.AddTransient<IOperacionTransient, Operacion>();
builder.Services.AddScoped<IOperacionScoped, Operacion>();
builder.Services.AddSingleton<IOperacionSingleton, Operacion>();
builder.Services.AddTransient<ConsumidorOperaciones>();

// =====================================================================
// 2) APP: construcción y definición del pipeline de middleware
//    ¡EL ORDEN IMPORTA! Cada middleware envuelve a los siguientes.
// =====================================================================
var app = builder.Build();

// Manejo de errores: en Development se muestra la página detallada de excepciones
// (la activa WebApplication automáticamente); en el resto, una respuesta ProblemDetails.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}

app.UseCorrelationId();                         // middleware por convención
app.UseMiddleware<TiempoRespuestaMiddleware>(); // middleware por factoría

// Middleware en línea con app.Use: añade una cabecera a todas las respuestas.
app.Use(async (context, next) =>
{
    // Ojo: las cabeceras HTTP solo admiten ASCII (sin tildes ni eñes).
    context.Response.Headers["X-Curso"] = "ASP.NET Core - Dia 1";
    await next(context);
});

app.UseHttpsRedirection();
app.UseDefaultFiles();   // "/" → "/index.html"
app.UseStaticFiles();    // sirve wwwroot/ y cortocircuita el pipeline si encuentra el fichero

// app.Map: rama del pipeline. Todo lo que empiece por /ping termina aquí.
app.Map("/ping", rama =>
{
    rama.Run(async context => await context.Response.WriteAsync("pong"));
});

// --- Endpoints ------------------------------------------------------
app.MapIncidencias();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapDemos();
}

app.MapGet("/info", (Microsoft.Extensions.Options.IOptions<IncidenciasOptions> opciones, IWebHostEnvironment env) => new
{
    Aplicacion = opciones.Value.NombreAplicacion,
    Entorno = env.EnvironmentName,
    Version = typeof(Program).Assembly.GetName().Version?.ToString()
});

app.Run();
