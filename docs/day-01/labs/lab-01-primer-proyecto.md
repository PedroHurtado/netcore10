# Lab 1 — Crear el proyecto y la primera API

**Duración:** 30 min · **Teoría relacionada:** [04 — Estructura del proyecto](../04-estructura-proyecto.md), [03 — API REST](../03-arquitecturas-modernas.md)

## Objetivo

Crear desde la CLI la solución del **Gestor de Incidencias**, entender sus ficheros y exponer los primeros endpoints REST. En este lab los datos estarán en una lista estática; en el Lab 3 la sustituiremos por EF Core InMemory con inyección de dependencias.

## Paso 1 — Crear la solución

En una carpeta de trabajo propia (por ejemplo `practicas/`):

```bash
dotnet --list-sdks                                   # comprobar que hay un SDK 10.x
dotnet new sln -n GestorIncidencias
dotnet new web -n GestorIncidencias.Web -o GestorIncidencias.Web
dotnet sln add GestorIncidencias.Web
cd GestorIncidencias.Web
dotnet run
```

Abre la URL que aparece en consola. Debe mostrar `Hello World!`.

✅ **Comprueba:** abre `GestorIncidencias.slnx`, el `.csproj` y `Properties/launchSettings.json`. ¿En qué puerto arranca? ¿Con qué entorno?

## Paso 2 — Modelo de dominio

Crea la carpeta `Models/` y el fichero `Incidencia.cs` con:

- `enum Prioridad { Baja, Media, Alta, Critica }`
- `enum EstadoIncidencia { Abierta, EnCurso, Resuelta, Cerrada }`
- `class Incidencia` con `Id`, `Titulo`, `Descripcion?`, `Prioridad`, `Estado`, `FechaAlta`, `FechaResolucion?`

Crea también `Models/Dtos.cs` con los *records* `CrearIncidenciaRequest` e `IncidenciaResponse` (consulta la solución en `src/day-01/GestorIncidencias.Web/Models/`).

> **¿Por qué DTOs?** Para no exponer la entidad interna. El cliente no debe poder enviar `Id`, `Estado` ni `FechaAlta`.

## Paso 3 — Primeros endpoints (versión provisional)

Sustituye `Program.cs` por:

```csharp
using System.Text.Json.Serialization;
using GestorIncidencias.Web.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// PROVISIONAL: lista estática. ¡No es thread-safe! La sustituiremos en el Lab 3.
var incidencias = new List<Incidencia>
{
    new() { Id = 1, Titulo = "La impresora no imprime", Prioridad = Prioridad.Baja, FechaAlta = DateTimeOffset.UtcNow }
};

app.MapGet("/api/incidencias", () => incidencias.Select(IncidenciaResponse.Desde));

app.MapGet("/api/incidencias/{id:int}", (int id) =>
    incidencias.FirstOrDefault(i => i.Id == id) is { } i
        ? Results.Ok(IncidenciaResponse.Desde(i))
        : Results.NotFound())
   .WithName("ObtenerIncidencia");

app.MapPost("/api/incidencias", (CrearIncidenciaRequest req) =>
{
    var nueva = new Incidencia
    {
        Id = incidencias.Count == 0 ? 1 : incidencias.Max(i => i.Id) + 1,
        Titulo = req.Titulo,
        Descripcion = req.Descripcion,
        Prioridad = req.Prioridad ?? Prioridad.Media,
        FechaAlta = DateTimeOffset.UtcNow
    };
    incidencias.Add(nueva);
    return Results.CreatedAtRoute("ObtenerIncidencia", new { id = nueva.Id }, IncidenciaResponse.Desde(nueva));
});

app.Run();
```

## Paso 4 — Probar con un fichero `.http`

Crea `GestorIncidencias.Web.http` (puedes copiar el de la solución) y ejecuta:

- `GET /api/incidencias` → `200` con un elemento.
- `GET /api/incidencias/99` → `404`.
- `POST /api/incidencias` con `{ "titulo": "No funciona la VPN", "prioridad": "Alta" }` → `201` y cabecera `Location`.

✅ **Comprueba:** ¿qué devuelve el `POST` si envías `"prioridad": "Urgente"`? ¿Y si no envías `titulo`?

## Paso 5 — Ficheros estáticos

1. Crea `wwwroot/index.html` y `wwwroot/css/site.css` (cópialos de la solución).
2. Arranca y abre `/`. ¿Qué ves? Todavía "Hello World" o un 404: falta el middleware.
3. Añade antes de los `Map...`:

   ```csharp
   app.UseDefaultFiles();
   app.UseStaticFiles();
   ```

4. Elimina el `MapGet("/", ...)` si aún existe y vuelve a probar.

✅ **Comprueba:** la página muestra la lista de incidencias consumiendo tu API.

## Paso 6 — Hot Reload

Ejecuta `dotnet watch`, cambia el texto de `index.html` o el de un endpoint y guarda. Observa cómo se aplica sin reiniciar.

## Reto (opcional)

1. Añade `POST /api/incidencias/{id}/resolver` que cambie el estado a `Resuelta` y fije `FechaResolucion`. Devuelve `404` si no existe y `400` si ya estaba resuelta.
2. Añade un endpoint `GET /info` que devuelva el nombre de la aplicación y el entorno (`app.Environment.EnvironmentName`).

## Para reflexionar

- ¿Qué pasaría con la lista estática si dos usuarios hacen `POST` a la vez?
- ¿Qué tiene de malo que la lógica de crear la incidencia esté dentro de la *lambda* del endpoint? (Piensa en el `Button_Click` de Web Forms.)

## Referencias

> Enlaces comprobados el 4 de octubre de 2026.

- [Tutorial: Creación de una API mínima con ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/tutorials/min-web-api?view=aspnetcore-10.0) — Tutorial oficial paso a paso, muy próximo a este laboratorio.
- [Plantillas predeterminadas de `dotnet new`](https://learn.microsoft.com/es-es/dotnet/core/tools/dotnet-new-sdk-templates) — `web`, `webapi`, `mvc`, `webapp`, `sln`...
- [Controladores de ruta en API mínimas](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-10.0) — `MapGet`, `MapPost`, nombres de endpoint y grupos de rutas.
- [Enlace de parámetros en API mínimas](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/minimal-apis/parameter-binding?view=aspnetcore-10.0) — De dónde sale cada parámetro (ruta, cuerpo, servicios).
- [Creación de respuestas en API mínimas](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0) — `Results.CreatedAtRoute`, `Results.NotFound`...
- [Archivos estáticos](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0) — `UseDefaultFiles` y `UseStaticFiles`.
- [Uso de archivos .http](https://learn.microsoft.com/es-es/aspnet/core/test/http-files?view=aspnetcore-10.0)
- [`dotnet watch`](https://learn.microsoft.com/es-es/dotnet/core/tools/dotnet-watch) y [Recarga activa](https://learn.microsoft.com/es-es/aspnet/core/test/hot-reload?view=aspnetcore-10.0)
