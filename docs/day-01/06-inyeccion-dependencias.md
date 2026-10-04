# 6. Inyección de dependencias (DI)

## 6.1 El problema

```csharp
// Estilo habitual en aplicaciones legacy
public class IncidenciaService
{
    public void Crear(string titulo)
    {
        var repo = new SqlIncidenciaRepository(ConfigurationManager.ConnectionStrings["Bd"].ConnectionString);
        var log  = LogManager.GetLogger("Incidencias");
        if (DateTime.Now.Hour > 20) { ... }
        repo.Insertar(titulo);
    }
}
```

- **Acoplamiento**: el servicio decide qué implementación usar (SQL) y cómo construirla.
- **No testeable**: no se puede probar sin base de datos, ni controlar `DateTime.Now`.
- **Dependencias ocultas**: para saber qué necesita la clase hay que leer todo su código.

## 6.2 La solución: inversión de dependencias + inyección

```csharp
public class IncidenciaService(
    IIncidenciaRepository repositorio,          // abstracción, no implementación
    TimeProvider reloj,                         // el "ahora" también es una dependencia
    IOptionsSnapshot<IncidenciasOptions> opciones,
    ILogger<IncidenciaService> logger) : IIncidenciaService
{ ... }
```

- La clase **declara** lo que necesita en su constructor.
- Un **contenedor** (el de ASP.NET Core, `IServiceProvider`) crea los objetos y les entrega sus dependencias.
- Cambiar de implementación es cambiar **una línea** en `Program.cs`.

> La sintaxis `class X(IA a, IB b)` es un **constructor primario** (C# 12). Equivale a declarar campos privados y un constructor que los asigna.

## 6.3 Registro de servicios

```csharp
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<IncidenciasDbContext>(opt => opt.UseInMemoryDatabase("GestorIncidencias"));
builder.Services.AddScoped<IIncidenciaRepository, EfIncidenciaRepository>();
builder.Services.AddScoped<IIncidenciaService, IncidenciaService>();
builder.Services.AddHostedService<DatosDemoInitializer>();
```

Se lee: *"cuando alguien pida `IIncidenciaRepository`, dale un `EfIncidenciaRepository`"*.

## 6.4 Tiempos de vida (*lifetimes*)

| Lifetime | Se crea... | Ejemplos típicos |
|---|---|---|
| **Transient** | Cada vez que se pide | Servicios ligeros y sin estado |
| **Scoped** | Una vez por **petición HTTP** | `DbContext`, repositorios, servicios de negocio, "usuario actual" |
| **Singleton** | Una vez para **toda la aplicación** | Caché en memoria, configuración, `TimeProvider`, clientes HTTP compartidos |

### Demo en el proyecto: `GET /demo/lifetimes`

La misma clase `Operacion` (que genera un `Guid` al construirse) se registra tres veces:

```csharp
builder.Services.AddTransient<IOperacionTransient, Operacion>();
builder.Services.AddScoped<IOperacionScoped, Operacion>();
builder.Services.AddSingleton<IOperacionSingleton, Operacion>();
```

El endpoint la recibe directamente **y** a través de otro servicio (`ConsumidorOperaciones`). Resultado:

| | Dentro de la misma petición | Entre peticiones |
|---|---|---|
| Transient | **Distinto** en endpoint y consumidor | Distinto |
| Scoped | **Igual** en endpoint y consumidor | **Distinto** |
| Singleton | Igual | **Igual siempre** |

### Regla de oro: dependencias cautivas (*captive dependencies*)

> Un servicio **no debe depender de otro con un lifetime más corto**.

Un *Singleton* que recibe un *Scoped* en el constructor lo "captura" para siempre: el `DbContext` de la primera petición se reutilizaría en todas (errores de concurrencia, datos obsoletos, fugas de memoria). En Development, ASP.NET Core **detecta** este error al arrancar (`ValidateScopes`).

Caso real en el proyecto: `DatosDemoInitializer` es un `IHostedService` (siempre Singleton) y necesita el repositorio (Scoped). Solución: **crear un ámbito** propio.

```csharp
public class DatosDemoInitializer(IServiceScopeFactory scopeFactory, ...) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repositorio = scope.ServiceProvider.GetRequiredService<IIncidenciaRepository>();
        ...
    }
}
```

## 6.5 Dónde se inyecta

| Lugar | Cómo |
|---|---|
| Clases (servicios, controladores, PageModels) | **Constructor** (recomendado) |
| Minimal APIs | Parámetros de la *lambda* (`(IIncidenciaService servicio) => ...`) |
| Middleware por convención | Constructor (singletons) o parámetros de `InvokeAsync` (scoped) |
| Vistas Razor | `@inject IServicio servicio` |
| Excepcional | `IServiceProvider.GetRequiredService<T>()` (*service locator*: evitar en código de negocio) |

## 6.6 Más opciones de registro

```csharp
// Varias implementaciones de la misma interfaz → se inyecta IEnumerable<INotificador>
builder.Services.AddScoped<INotificador, NotificadorEmail>();
builder.Services.AddScoped<INotificador, NotificadorTeams>();

// Servicios con clave (.NET 8+)
builder.Services.AddKeyedScoped<INotificador, NotificadorEmail>("email");
public class X([FromKeyedServices("email")] INotificador n) { }

// No registrar si ya existe
builder.Services.TryAddSingleton<ICache, MemoriaCache>();

// Factoría
builder.Services.AddScoped<IRepo>(sp => new Repo(sp.GetRequiredService<IConfiguration>()["X"]!));
```

## 6.7 EF Core InMemory en el contenedor

Durante el curso usamos el proveedor **InMemory** de EF Core: la base de datos vive en la memoria del proceso y se pierde al reiniciar. Así no hace falta instalar SQL Server y el código es **el mismo** que con una base de datos real.

```csharp
builder.Services.AddDbContext<IncidenciasDbContext>(opt => opt.UseInMemoryDatabase("GestorIncidencias"));
// Mañana con SQL Server bastaría con:  opt.UseSqlServer(connectionString)
```

- `AddDbContext` registra el contexto como **Scoped**.
- Por eso `EfIncidenciaRepository` también es Scoped.
- Limitaciones de InMemory: no es una base de datos relacional, las transacciones no se admiten y no ejecuta SQL. Microsoft **desaconseja usarlo para pruebas** (no reproduce el comportamiento de la base de datos real); en este curso lo usamos solo para no depender de un servidor. Profundizaremos en el día 3.

## 6.8 Beneficio inmediato: sustitución

Gracias a `IIncidenciaRepository`, podemos cambiar EF Core por otra implementación (una lista en memoria, un servicio externo, un mock en pruebas) **sin tocar** `IncidenciaService` ni los endpoints. Es lo que haremos en el Lab 3.

## Preguntas de repaso

1. ¿Qué lifetime le darías a un servicio que guarda el usuario autenticado de la petición actual?
2. ¿Por qué `DatosDemoInitializer` no recibe `IIncidenciaRepository` en el constructor?
3. ¿Por qué se inyecta `TimeProvider` en lugar de usar `DateTimeOffset.UtcNow` directamente?

## Referencias

> Enlaces comprobados el 4 de octubre de 2026.

**Documentación oficial**

- [Inserción de dependencias en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-10.0) — Registro, lifetimes y servicios con claves.
- [Inserción de dependencias en .NET](https://learn.microsoft.com/es-es/dotnet/core/extensions/dependency-injection/overview) — El contenedor `Microsoft.Extensions.DependencyInjection` en detalle, incluida la validación del ámbito.
- [Instrucciones para la inserción de dependencias](https://learn.microsoft.com/es-es/dotnet/core/extensions/dependency-injection/guidelines) — Buenas prácticas y antipatrones (dependencias cautivas, *service locator*, `IDisposable`).
- [Host web: `ValidateScopes` en Development](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/host/web-host?view=aspnetcore-10.0) — La validación de ámbitos se activa cuando el entorno es Development.
- [Tareas en segundo plano con servicios hospedados](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0) y [Uso de servicios con ámbito dentro de un servicio en segundo plano](https://learn.microsoft.com/es-es/dotnet/core/extensions/scoped-service) — El patrón `IServiceScopeFactory` de `DatosDemoInitializer`.
- [Duración, configuración e inicialización de DbContext](https://learn.microsoft.com/es-es/ef/core/dbcontext-configuration/) — Por qué el `DbContext` es Scoped.
- [Proveedor de bases de datos en memoria de EF Core](https://learn.microsoft.com/es-es/ef/core/providers/in-memory/) — Incluye la advertencia de que no se recomienda para pruebas.
- [Elección de una estrategia de pruebas con EF Core](https://learn.microsoft.com/es-es/ef/core/testing/choosing-a-testing-strategy) — Limitaciones de InMemory (transacciones, SQL sin formato) y alternativas.
- [¿Qué es la clase `TimeProvider`?](https://learn.microsoft.com/es-es/dotnet/standard/datetime/timeprovider-overview) — Abstracción del tiempo incluida desde .NET 8.
- [Constructores principales (C# 12)](https://learn.microsoft.com/es-es/dotnet/csharp/whats-new/tutorials/primary-constructors)
- [Principios arquitectónicos](https://learn.microsoft.com/es-es/dotnet/architecture/modern-web-apps-azure/architectural-principles) — Inversión de dependencias y separación de responsabilidades.

**Lectura clásica**

- Martin Fowler, [Inversion of Control Containers and the Dependency Injection pattern](https://martinfowler.com/articles/injection.html) (inglés) — Artículo que popularizó el término "inyección de dependencias".
