# Lab 2 — Middleware propio y orden del pipeline

**Duración:** 30 min · **Teoría relacionada:** [05 — Middleware y pipeline](../05-middleware-pipeline.md)

## Objetivo

Escribir middleware de las cuatro formas posibles, observar el orden de ejecución y configurar la gestión de errores según el entorno.

## Paso 1 — Observar el orden con `app.Use`

Añade **al principio** del pipeline (justo después de `var app = builder.Build();`):

```csharp
app.Use(async (ctx, next) =>
{
    Console.WriteLine($"[A] Entrando  {ctx.Request.Path}");
    await next(ctx);
    Console.WriteLine($"[A] Saliendo  {ctx.Response.StatusCode}");
});

app.Use(async (ctx, next) =>
{
    Console.WriteLine("[B] Entrando");
    await next(ctx);
    Console.WriteLine("[B] Saliendo");
});
```

Llama a `GET /api/incidencias` y mira la consola.

✅ **Comprueba:** el orden es `A entra → B entra → endpoint → B sale → A sale` (como capas de cebolla).

Ahora pide `/css/site.css`. ¿Se ejecutan A y B? ¿Y si mueves los dos `app.Use` **después** de `UseStaticFiles()`? Explica por qué.

Elimina estos dos middleware de prueba cuando termines.

## Paso 2 — Cortocircuito y ramas

```csharp
app.Map("/ping", rama => rama.Run(async ctx => await ctx.Response.WriteAsync("pong")));
```

✅ **Comprueba:** `/ping` devuelve `pong` sin llegar a ningún endpoint.

## Paso 3 — Middleware en línea con cabecera

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Curso"] = "ASP.NET Core - Día 1";
    await next(context);
});
```

Ejecuta cualquier petición. **Algo falla.** Lee el error en la página de excepciones y corrígelo.

<details>
<summary>Pista</summary>

Las cabeceras HTTP solo admiten caracteres ASCII. Quita la tilde de "Día".
</details>

## Paso 4 — Middleware por convención: `CorrelationIdMiddleware`

Crea `Middleware/CorrelationIdMiddleware.cs`:

1. Si la petición trae la cabecera `X-Correlation-Id`, usa ese valor; si no, genera un `Guid`.
2. Devuelve el identificador en la cabecera de respuesta usando `context.Response.OnStarting(...)`.
3. Abre un *scope* de logging con `logger.BeginScope("CorrelationId:{CorrelationId}", id)` alrededor de `await next(context)`.
4. Crea el método de extensión `UseCorrelationId()`.

Regístralo en `Program.cs`: `app.UseCorrelationId();`

Para ver los *scopes* en consola, añade en `appsettings.Development.json`:

```json
"Logging": { "Console": { "IncludeScopes": true } }
```

✅ **Comprueba:** envía una petición con `X-Correlation-Id: mi-peticion-123` y verifica que vuelve en la respuesta.

## Paso 5 — Middleware por factoría: `TiempoRespuestaMiddleware`

Crea `Middleware/TiempoRespuestaMiddleware.cs` implementando `IMiddleware`:

- Mide el tiempo con `Stopwatch.GetTimestamp()` y `Stopwatch.GetElapsedTime(inicio)`.
- Añade la cabecera `X-Tiempo-Respuesta-ms` (usa `CultureInfo.InvariantCulture` para que el decimal sea un punto).
- Después de `await next(context)`, escribe un log: `GET /api/incidencias → 200 en 3.21 ms`.

Regístralo:

```csharp
builder.Services.AddTransient<TiempoRespuestaMiddleware>();
// ...
app.UseMiddleware<TiempoRespuestaMiddleware>();
```

✅ **Comprueba:** ¿qué pasa si olvidas el `AddTransient`? Lee el mensaje de error.

## Paso 6 — Errores según el entorno

```csharp
builder.Services.AddProblemDetails();
// ...
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
}
```

Añade un endpoint que lance una excepción:

```csharp
app.MapGet("/demo/error", () => { throw new InvalidOperationException("Error de prueba"); });
```

1. En Development: página detallada con la traza.
2. Arranca en Production: `dotnet run --environment Production` → respuesta JSON ProblemDetails sin detalles internos.

## Reto (opcional)

1. Crea un middleware `ModoMantenimientoMiddleware` que, si existe el fichero `mantenimiento.txt` en la raíz del proyecto, responda `503 Service Unavailable` a todo lo que empiece por `/api` (y deje pasar el resto).
2. ¿En qué posición del pipeline lo pondrías? ¿Antes o después de `UseStaticFiles`?

## Para reflexionar

- ¿Qué `HttpModule` o código de `Global.asax` de tus aplicaciones actuales podrías convertir en middleware?
