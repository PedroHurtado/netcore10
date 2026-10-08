# 7. Logging, monitorización y rendimiento

Una aplicación en producción es una caja negra si no **cuenta lo que hace**. Desde el día 1 usamos `ILogger` y el `CorrelationId`; hoy completamos el cuadro: logs **estructurados** y de alto rendimiento, una línea por petición, formato JSON en producción, comprobaciones de salud y el repaso de todo lo que hemos hecho por el rendimiento.

## 7.1 Repaso: `ILogger`, categorías y niveles

```csharp
public class IncidenciaService(..., ILogger<IncidenciaService> logger)   // categoría = nombre completo de la clase
```

| Nivel | Para qué | Ejemplo en el proyecto |
|---|---|---|
| `Trace` / `Debug` | Detalle para desarrollo | Operación rechazada por una regla de negocio (1004) |
| `Information` | El flujo normal, lo que interesa auditar | Incidencia creada, inicio de sesión correcto |
| `Warning` | Algo raro que no impide seguir | Límite de abiertas alcanzado, login fallido, cuenta bloqueada |
| `Error` | Ha fallado una operación | Excepción no controlada (la escribe `UseExceptionHandler`) |
| `Critical` | La aplicación no puede seguir | Sin conexión a la base de datos al arrancar |

Qué se escribe se decide **por configuración**, por categoría (el prefijo más largo gana):

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",                    // el framework, solo avisos...
    "Microsoft.AspNetCore.HttpLogging": "Information",    // ...salvo la línea por petición
    "GestorIncidencias": "Debug"                          // nuestro código, más detalle (solo Development)
  }
}
```

> **Equivalencia:** en .NET Framework se usaba log4net, NLog o `System.Diagnostics.Trace` configurados en `Web.config`. `ILogger` es la **abstracción**; detrás puede haber la consola, NLog, Serilog o un exportador de OpenTelemetry sin cambiar el código que escribe los logs.

## 7.2 Logs estructurados: plantillas, no interpolación

```csharp
// ✅ Plantilla: {Id} y {Usuario} viajan como PROPIEDADES con nombre
logger.LogInformation("Incidencia {Id} creada por {Usuario}", incidencia.Id, usuario.Nombre);

// ❌ Interpolación: el log recibe un texto ya montado; se pierden las propiedades
logger.LogInformation($"Incidencia {incidencia.Id} creada por {usuario.Nombre}");
```

Con la plantilla, un recolector de logs puede responder a "**todas** las operaciones de Ana García ayer" o "¿cuántas incidencias se crearon por hora?" sin expresiones regulares. Además, con la interpolación el texto se construye **siempre**, aunque el nivel esté desactivado. El analizador **CA2254** avisa de este error.

Así se ve la diferencia en el JSON de producción (salida real de la aplicación):

```json
{"Timestamp":"2026-10-08T05:57:09.434Z","EventId":9,"LogLevel":"Information",
 "Category":"Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware",
 "Message":"Request and Response: ...",
 "State":{"Method":"GET","Path":"/Incidencias","StatusCode":302,"Duration":5.2898},
 "Scopes":[ ..., {"CorrelationId":"06fb54aea50844038089ba827633bbbc"} ]}
```

`State` lleva cada propiedad por separado y `Scopes` lleva el **CorrelationId** del día 1: todas las líneas de una misma petición se pueden agrupar.

### `[LoggerMessage]`: el compilador escribe el código

Los mensajes de los casos de uso están en [Application/Incidencias/IncidenciaLog.cs](../../src/day-04/GestorIncidencias.Application/Incidencias/IncidenciaLog.cs):

```csharp
internal static partial class IncidenciaLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Incidencia {Id} creada con prioridad {Prioridad} por {Usuario}")]
    public static partial void IncidenciaCreada(this ILogger logger, int id, Prioridad prioridad, string usuario);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning,
        Message = "Límite de incidencias abiertas alcanzado ({Max})")]
    public static partial void LimiteAbiertasAlcanzado(this ILogger logger, int max);
    // ...
}

// En IncidenciaService:
logger.IncidenciaCreada(incidencia.Id, incidencia.Prioridad, usuario.Nombre);
```

| Ventaja | Por qué |
|---|---|
| **Rendimiento** | La plantilla se analiza al compilar; si el nivel está desactivado no se hace nada; sin *boxing* de los `int` |
| **EventId fijo** | Se puede buscar y alertar por número ("avísame si aparece el 1003 más de 10 veces en una hora") |
| **Tipado** | Si cambias un parámetro, el compilador avisa en todos los usos |
| **Catálogo** | Todos los mensajes del módulo, juntos y con nombre |

Salida en la consola de Development (real):

```
info: GestorIncidencias.Application.Incidencias.IncidenciaService[1001]
      => ... => CorrelationId:978bd8b134414d95a1145a425eb387b7 => ...IncidenciasApiController.Crear (GestorIncidencias.Web)
      Incidencia 205 creada con prioridad Alta por Ana García
dbug: GestorIncidencias.Application.Incidencias.IncidenciaService[1004]
      => ... => CorrelationId:c6e2dfb28c694194b9744c06262c3537 => ...IncidenciasApiController.Cerrar (GestorIncidencias.Web)
      Operación rechazada sobre la incidencia 1: Solo se puede cerrar una incidencia resuelta (estado actual: Abierta).
```

El `[1001]` es el EventId; la línea `=>` son los *scopes* (petición, CorrelationId, acción).

## 7.3 Una línea por petición: HTTP logging

En [Program.cs](../../src/day-04/GestorIncidencias.Web/Program.cs):

```csharp
builder.Services.AddHttpLogging(o =>
{
    o.LoggingFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.RequestPath
                    | HttpLoggingFields.ResponseStatusCode | HttpLoggingFields.Duration;
    o.CombineLogs = true;                     // petición y respuesta en UNA entrada
});
// ...
app.UseRouting();
app.UseHttpLogging();                         // después de UseRouting: respeta la configuración de cada endpoint

app.MapStaticAssets().WithHttpLogging(HttpLoggingFields.None);    // sin una línea por cada CSS
app.MapHealthChecks("/salud").WithHttpLogging(HttpLoggingFields.None);
```

Es el equivalente del **log de IIS**, pero dentro de la aplicación y con el CorrelationId.

> ⚠️ **No** registréis cabeceras ni cuerpos (`RequestHeaders`, `RequestBody`...) en producción: llevan **cookies, tokens, contraseñas** (el formulario de login) y **datos personales**. Si hace falta para depurar algo concreto, solo en desarrollo y solo en ese endpoint.

## 7.4 Qué registrar (y qué no)

| ✅ Registrar | ❌ No registrar nunca |
|---|---|
| Inicios de sesión correctos, fallidos y bloqueos (quién y cuándo) | Contraseñas (ni siquiera las incorrectas) |
| Operaciones de negocio relevantes (crear, cerrar, cambiar de estado) | Tokens, cookies, cabecera `Authorization` |
| Rechazos por permisos (403) | Datos personales innecesarios (DNI, salud, dirección...). Ojo con el RGPD |
| Errores, con la excepción completa | El cuerpo entero de las peticiones |
| Duración de las peticiones | Cadenas de conexión |

En el proyecto, los inicios de sesión se registran en [CuentaController](../../src/day-04/GestorIncidencias.Web/Controllers/CuentaController.cs) (`Inicio de sesión fallido de {Usuario}`, `Cuenta bloqueada...`). El correo del usuario **sí** se registra: es necesario para la auditoría. Es una decisión que conviene acordar con el responsable de protección de datos.

## 7.5 Formato en producción: JSON

[appsettings.Production.json](../../src/day-04/GestorIncidencias.Web/appsettings.Production.json):

```json
"Logging": {
  "Console": {
    "FormatterName": "json",
    "FormatterOptions": { "IncludeScopes": true, "UseUtcTimestamp": true, "TimestampFormat": "yyyy-MM-ddTHH:mm:ss.fffZ" }
  }
}
```

Una línea JSON por evento la entiende cualquier recolector (Seq, Elasticsearch/Kibana, Grafana Loki, Azure Monitor...). Para probarlo:

```bash
ASPNETCORE_ENVIRONMENT=Production dotnet run --project GestorIncidencias.Web --no-launch-profile
```

(En Production no hay datos de demostración: la portada sale con todo a cero.)

## 7.6 Monitorización

### Comprobaciones de salud

```csharp
// Infrastructure: ¿responde la base de datos?
services.AddHealthChecks().AddDbContextCheck<IncidenciasDbContext>("base-de-datos");

// Web: pública y sin log (la consulta el balanceador cada pocos segundos)
app.MapHealthChecks("/salud").AllowAnonymous().WithHttpLogging(HttpLoggingFields.None);
```

`GET /salud` → `200 Healthy` o `503 Unhealthy`. Lo usan el **balanceador** (para dejar de enviar tráfico a un servidor enfermo), la **monitorización** (alertas) y, en contenedores, el orquestador. Se pueden añadir comprobaciones de cualquier dependencia: una API externa, espacio en disco, una cola...

### Métricas y trazas: OpenTelemetry

Los logs cuentan **qué pasó**; las **métricas** cuentan **cuánto** (peticiones por segundo, duración, errores) y las **trazas** cuentan **por dónde** pasó una petición entre varios servicios. ASP.NET Core ya publica métricas (`http.server.request.duration`, conexiones activas...) y trazas (`Activity`: el `TraceId` que aparece en los logs). **OpenTelemetry** es el estándar para enviarlas a una herramienta de monitorización:

```csharp
// Paquetes: OpenTelemetry.Extensions.Hosting, OpenTelemetry.Instrumentation.AspNetCore,
//           OpenTelemetry.Exporter.OpenTelemetryProtocol
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddAspNetCoreInstrumentation())
    .WithTracing(t => t.AddAspNetCoreInstrumentation())
    .UseOtlpExporter();                  // a Grafana, Jaeger, Azure Monitor, el panel de Aspire...
```

No lo hemos añadido al proyecto porque necesita un destino (un colector) instalado. Para verlo en local, el **panel de .NET Aspire** se puede ejecutar como contenedor independiente y recibe logs, métricas y trazas por OTLP.

### Herramientas de diagnóstico en caliente

| Herramienta | Para qué |
|---|---|
| `dotnet-counters` | Ver métricas en directo (CPU, memoria, GC, peticiones/s) de un proceso en marcha |
| `dotnet-trace` | Grabar una traza de rendimiento (qué métodos consumen CPU) |
| `dotnet-dump` | Volcado de memoria para analizar fugas o bloqueos |

Se instalan con `dotnet tool install -g dotnet-counters` (etc.) y se conectan a un proceso por su PID, también en un servidor.

## 7.7 Rendimiento: lo que ya hemos hecho

El Módulo 6 pide "optimización de rendimiento". Lo hemos ido haciendo cada día; este es el resumen:

| Técnica | Día | Dónde |
|---|---|---|
| Todo `async` con `CancellationToken` (no bloquear hilos esperando a la BD) | 1-4 | Controladores, servicios, repositorios |
| Minificación de HTML y CSS, huella en el nombre + `immutable`, compresión de estáticos | 2 | `MapStaticAssets`, WebMarkupMin, tarea `MinificarCss` |
| Output Cache de páginas (solo anónimos) | 2 / 4 | `CacheHtmlPolicy` |
| Consultas: proyección, paginación, recuentos en SQL, evitar N+1, índices | 3 | `EfIncidenciaConsultas` |
| Caché de datos de referencia con protección contra estampidas | 4 | `HybridCache` en categorías |
| Logs de alto rendimiento | 4 | `[LoggerMessage]` |

Y lo que hay que vigilar al migrar código legacy:

| 🚩 En el legacy | ✅ En ASP.NET Core |
|---|---|
| `.Result` / `.Wait()` sobre tareas (*sync over async*) | `await` en toda la cadena: con el pool de hilos de ASP.NET Core, bloquear hilos lleva a la saturación |
| `new HttpClient()` en cada llamada | `IHttpClientFactory` (`AddHttpClient`) |
| ViewState de cientos de KB | Sin ViewState (capítulo 2) |
| `Session` con objetos grandes | Volver a consultar, paginado |
| Cargar tablas enteras y filtrar en memoria | Filtrar en SQL (día 3) |

> **Medir antes de optimizar.** `Duration` en el log de peticiones dice qué páginas son lentas; las herramientas de 7.6 dicen por qué. Para micro-optimizaciones de código existe **BenchmarkDotNet**, y para pruebas de carga herramientas como k6, JMeter o Azure Load Testing.

## Referencias

> Enlaces comprobados el 8 de octubre de 2026.

- [Registro en .NET y ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/logging/?view=aspnetcore-10.0)
- [Generación de código fuente de registro en tiempo de compilación](https://learn.microsoft.com/es-es/dotnet/core/extensions/logging/source-generation) — `[LoggerMessage]`.
- [Registro HTTP en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/http-logging/?view=aspnetcore-10.0)
- [Formato de registro de consola](https://learn.microsoft.com/es-es/dotnet/core/extensions/logging/console-log-formatter) — El formateador JSON.
- [Comprobaciones de estado en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)
- [Observabilidad de .NET con OpenTelemetry](https://learn.microsoft.com/es-es/dotnet/core/diagnostics/observability-with-otel)
- [Métricas de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/metrics/overview?view=aspnetcore-10.0)
- [dotnet-counters](https://learn.microsoft.com/es-es/dotnet/core/diagnostics/dotnet-counters)
- [Procedimientos recomendados de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/best-practices?view=aspnetcore-10.0) — Rendimiento: async, `HttpClient`, caché, grandes objetos...
