# 5. Middleware y pipeline de solicitudes

## 5.1 Concepto

Un **middleware** es un componente que:

1. Recibe el `HttpContext` (petición + respuesta).
2. Puede hacer algo **antes** de pasar al siguiente.
3. Decide si **llama al siguiente** (`next`) o **cortocircuita** y responde él mismo.
4. Puede hacer algo **después**, cuando el resto del pipeline ha terminado.

Los middleware se encadenan formando el **pipeline**:

```
 Petición ─▶ ┌─────────────┐   ┌─────────────┐   ┌──────────────┐   ┌──────────┐
             │ Correlation │──▶│ Tiempo      │──▶│ StaticFiles  │──▶│ Endpoint │
             │ Id          │   │ Respuesta   │   │              │   │ (tu API) │
 Respuesta ◀─│             │◀──│             │◀──│              │◀──│          │
             └─────────────┘   └─────────────┘   └──────────────┘   └──────────┘
```

Es el equivalente a los `HttpModule` y a los eventos de `Global.asax` (`BeginRequest`, `AuthenticateRequest`...) de .NET Framework, pero **explícito, ordenado y en código**.

## 5.2 El orden importa

Los middleware se ejecutan **en el orden en que se añaden**. Orden recomendado por Microsoft:

```csharp
app.UseExceptionHandler();   // 1. Captura errores de todo lo que va después
app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();        // 2. Ficheros estáticos (cortocircuita si encuentra el fichero)
app.UseRouting();            // 3. Decide qué endpoint corresponde (implícito en WebApplication)
app.UseCors();
app.UseAuthentication();     // 4. ¿Quién eres?
app.UseAuthorization();      // 5. ¿Puedes hacer esto?
app.UseSession();
app.MapControllers();        // 6. Endpoints
```

> En las plantillas de .NET 9 y 10, los ficheros estáticos se sirven con `app.MapStaticAssets()`, que es un **endpoint** y por tanto se declara junto al resto de `Map...`, después de autenticación y autorización. `UseStaticFiles()` sigue siendo un middleware y va antes del enrutamiento.

Errores típicos por orden incorrecto:

- `UseAuthorization` antes de `UseAuthentication` → el usuario siempre es anónimo.
- `UseExceptionHandler` al final → no captura las excepciones de los middleware anteriores.
- `UseStaticFiles` después de la autenticación → cada imagen o CSS pasa por la autenticación (coste innecesario), o al revés: si se quiere proteger ficheros estáticos, debe ir después.

## 5.3 Formas de escribir middleware

### a) En línea con `app.Use`

```csharp
app.Use(async (context, next) =>
{
    // ANTES
    context.Response.Headers["X-Curso"] = "ASP.NET Core - Dia 1";
    await next(context);
    // DESPUÉS
});
```

### b) Terminal con `app.Run`

No recibe `next`: **termina** el pipeline.

```csharp
app.Run(async context => await context.Response.WriteAsync("Fin"));
```

### c) Ramas con `app.Map` / `app.MapWhen`

```csharp
app.Map("/ping", rama => rama.Run(async ctx => await ctx.Response.WriteAsync("pong")));

app.MapWhen(ctx => ctx.Request.Query.ContainsKey("debug"), rama => { ... });
```

### d) Clase por convención (proyecto: `CorrelationIdMiddleware`)

```csharp
public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context /*, servicios scoped aquí */)
    {
        var id = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
        context.Response.OnStarting(() => { context.Response.Headers["X-Correlation-Id"] = id; return Task.CompletedTask; });

        using (logger.BeginScope("CorrelationId:{CorrelationId}", id))
            await next(context);
    }
}

// Registro
app.UseMiddleware<CorrelationIdMiddleware>();
// o con método de extensión: app.UseCorrelationId();
```

- Se instancia **una vez** (vive toda la aplicación).
- Servicios **scoped** → como **parámetros de `InvokeAsync`**, nunca en el constructor.

### e) Clase por factoría: `IMiddleware` (proyecto: `TiempoRespuestaMiddleware`)

```csharp
public class TiempoRespuestaMiddleware(ILogger<TiempoRespuestaMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next) { ... }
}

builder.Services.AddTransient<TiempoRespuestaMiddleware>();   // ¡obligatorio registrarlo!
app.UseMiddleware<TiempoRespuestaMiddleware>();
```

- Se resuelve del contenedor de DI **en cada petición** → puede recibir servicios scoped en el constructor.
- Más fácil de probar con tests unitarios.

## 5.4 Modificar la respuesta: cuidado con las cabeceras

Las cabeceras se envían en cuanto se empieza a escribir el cuerpo. Después de `await next(context)` normalmente **ya es tarde** para añadir cabeceras (`InvalidOperationException: Headers are read-only`).

Solución: registrar un *callback* con `context.Response.OnStarting(...)`, que se ejecuta justo antes de enviar las cabeceras. Así lo hacen los dos middleware del proyecto.

> **Curiosidad real del proyecto:** la primera versión de la cabecera `X-Curso` llevaba "Día" con tilde y **todas las peticiones fallaban con un 500** (`Invalid non-ASCII or control character in header`). RFC 9110 indica que los valores de cabecera se restringen normalmente a US-ASCII, y Kestrel, por defecto, solo admite ASCII en las cabeceras de respuesta (se puede cambiar con `KestrelServerOptions.ResponseHeaderEncodingSelector`, pero lo correcto es no usar caracteres no ASCII). Es un error fácil de cometer en aplicaciones en español.

## 5.5 Middleware incluidos en ASP.NET Core (selección)

| Middleware | Uso |
|---|---|
| `UseExceptionHandler` / `UseDeveloperExceptionPage` | Gestión de errores |
| `UseHsts`, `UseHttpsRedirection` | Seguridad de transporte |
| `UseStaticFiles` / `MapStaticAssets` | Ficheros estáticos (`MapStaticAssets` añade compresión y *fingerprinting*) |
| `UseDefaultFiles` | `/` → `index.html` |
| `UseRouting` / endpoints | Enrutamiento |
| `UseCors` | Peticiones de otros orígenes |
| `UseAuthentication` / `UseAuthorization` | Seguridad (día 4) |
| `UseSession` | Estado de sesión |
| `UseResponseCompression`, `UseOutputCache`, `UseRateLimiter` | Rendimiento y protección (día 4) |

## 5.6 Gestión de errores según el entorno

```csharp
builder.Services.AddProblemDetails();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();   // devuelve ProblemDetails (500) sin detalles internos
    app.UseHsts();
}
// En Development, WebApplication activa automáticamente la página detallada de excepciones.
```

Prueba en el proyecto: `GET /demo/error` (solo existe en Development) y compara con lo que vería un usuario en producción.

## Preguntas de repaso

1. ¿Qué ocurre si un middleware no llama a `next`?
2. ¿Por qué un middleware por convención no debe recibir un `DbContext` en el constructor?
3. ¿Cuál es el equivalente en ASP.NET Core de un `IHttpModule` que registraba la duración de cada petición?

## Referencias

> Enlaces comprobados el 4 de octubre de 2026.

**Documentación oficial**

- [Middleware de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/middleware/?view=aspnetcore-10.0) — Conceptos, `Use`/`Run`/`Map` y la sección **Orden del middleware** con el orden recomendado.
- [Escribir middleware personalizado](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/middleware/write?view=aspnetcore-10.0) — Middleware por convención y dependencias en `InvokeAsync`.
- [Activación de middleware basada en factoría (`IMiddleware`)](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/middleware/extensibility?view=aspnetcore-10.0)
- [WebApplication: middleware añadido automáticamente](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/minimal-apis/webapplication?view=aspnetcore-10.0) — `UseDeveloperExceptionPage` se añade el primero cuando el entorno es Development.
- [Controlar errores en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0) y [Control de errores en las API](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0)
- [Aplicación de HTTPS (`UseHttpsRedirection`, HSTS)](https://learn.microsoft.com/es-es/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0)
- [Archivos estáticos](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0) y [`MapStaticAssets`](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/map-static-files?view=aspnetcore-10.0) — Compresión y huella digital en tiempo de compilación.
- [Registro de eventos (incluye *scopes*)](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/logging/?view=aspnetcore-10.0)
- [`HttpResponse.OnStarting`](https://learn.microsoft.com/es-es/dotnet/api/microsoft.aspnetcore.http.httpresponse.onstarting?view=aspnetcore-10.0) — Referencia de la API usada para escribir cabeceras.
- [`KestrelServerOptions.ResponseHeaderEncodingSelector`](https://learn.microsoft.com/es-es/dotnet/api/microsoft.aspnetcore.server.kestrel.core.kestrelserveroptions.responseheaderencodingselector?view=aspnetcore-10.0) — Codificación de las cabeceras de respuesta (ASCII por defecto).
- [Migración de módulos HTTP a middleware](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/areas/http-modules?view=aspnetcore-10.0)

**Estándares**

- [RFC 9110 §5.5 — Field Values](https://www.rfc-editor.org/rfc/rfc9110#section-5.5) (inglés) — *"Field values are usually constrained to the range of US-ASCII characters"*.
