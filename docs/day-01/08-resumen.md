# 8. Resumen del día 1 y avance del día 2

## Lo que hemos aprendido

| Concepto | En una frase | Dónde está en el proyecto |
|---|---|---|
| .NET moderno | Multiplataforma, open source, una versión LTS cada dos años; hoy .NET 10 | `TargetFramework` en el `.csproj` |
| Web Forms → Core | No hay ViewState, ni PostBack, ni controles de servidor: se trabaja con HTTP | — |
| Modelos de programación | MVC, Razor Pages, API REST y Blazor sobre la misma base | `Endpoints/IncidenciasEndpoints.cs` |
| Host | `WebApplication.CreateBuilder` → `Build` → `Run` | `Program.cs` |
| Middleware | Cadena ordenada de componentes que procesan cada petición | `Middleware/`, `Program.cs` |
| DI | Las clases declaran dependencias; el contenedor las crea | `Program.cs`, `Services/` |
| Lifetimes | Transient / Scoped / Singleton; nunca un Singleton que dependa de un Scoped | `Lifetimes/`, `DatosDemoInitializer` |
| Configuración | Fuentes en capas; la última gana; entornos | `appsettings*.json` |
| Options | Configuración tipada y validada al arrancar | `Options/IncidenciasOptions.cs` |
| EF Core InMemory | Persistencia sin servidor de BD; mismo código que con SQL Server | `Data/`, `EfIncidenciaRepository` |

## Chuleta de `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<MisOpciones>().BindConfiguration("Seccion").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddDbContext<MiDbContext>(o => o.UseInMemoryDatabase("Bd"));
builder.Services.AddScoped<IServicio, Servicio>();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler();
app.UseMiMiddleware();
app.UseStaticFiles();
app.MapGet("/api/x", (IServicio s) => s.Hacer());

app.Run();
```

## Avance del día 2

Partiremos de `src/day-01` y lo convertiremos en `src/day-02`:

1. **Clean Architecture**: separaremos el proyecto en `Domain`, `Application`, `Infrastructure` y `Web`. Lo que hoy está en carpetas pasará a proyectos con dependencias controladas.
2. **Controladores MVC y vistas**: interfaz web para listar, crear y resolver incidencias.
3. **Razor Pages**: la misma funcionalidad con el modelo de páginas y comparación con Web Forms.
4. **Controladores API** frente a Minimal APIs y validación automática del modelo.
