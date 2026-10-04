# Día 1 — Introducción y fundamentos de ASP.NET Core

**Módulos del temario:** Módulo 1 (completo) y Módulo 2 (configuración del proyecto, middleware, DI, configuración y opciones).

## Objetivos

Al terminar el día el alumno será capaz de:

1. Situar .NET Framework, .NET Core y .NET 10 en el tiempo y entender por qué migrar.
2. Explicar las diferencias de modelo entre Web Forms y ASP.NET Core.
3. Elegir entre MVC, Razor Pages, Minimal APIs / controladores API y Blazor según el caso.
4. Crear un proyecto ASP.NET Core desde la CLI y entender cada fichero que contiene.
5. Construir y ordenar un pipeline de middleware, incluyendo middleware propio.
6. Registrar y consumir servicios con inyección de dependencias, eligiendo el *lifetime* correcto.
7. Usar `appsettings.json`, entornos y el patrón Options con validación.

## Agenda (9:00 – 14:00)

| Hora | Bloque | Tipo | Material |
|---|---|---|---|
| 9:00 – 9:20 | Presentación del curso, entorno y proyecto conductor | — | [Plan del curso](../README.md) |
| 9:20 – 10:00 | Evolución de .NET · Web Forms vs ASP.NET Core | Teoría | [01](01-evolucion-dotnet.md) · [02](02-webforms-vs-aspnetcore.md) |
| 10:00 – 10:20 | MVC, Razor Pages, API REST | Teoría | [03](03-arquitecturas-modernas.md) |
| 10:20 – 10:50 | Estructura de un proyecto ASP.NET Core | Teoría + demo | [04](04-estructura-proyecto.md) |
| 10:50 – 11:20 | **Lab 1** — Crear el proyecto y la primera API | Práctica | [Lab 1](labs/lab-01-primer-proyecto.md) |
| 11:20 – 11:35 | *Descanso* | | |
| 11:35 – 11:55 | Middleware y pipeline | Teoría + demo | [05](05-middleware-pipeline.md) |
| 11:55 – 12:25 | **Lab 2** — Middleware propio | Práctica | [Lab 2](labs/lab-02-middleware.md) |
| 12:25 – 12:50 | Inyección de dependencias | Teoría + demo | [06](06-inyeccion-dependencias.md) |
| 12:50 – 13:20 | **Lab 3** — Servicios, DI y EF Core InMemory | Práctica | [Lab 3](labs/lab-03-inyeccion-dependencias.md) |
| 13:20 – 13:35 | Configuración, opciones y entornos | Teoría + demo | [07](07-configuracion-opciones.md) |
| 13:35 – 13:55 | **Lab 4** — Configuración y entornos | Práctica | [Lab 4](labs/lab-04-configuracion.md) |
| 13:55 – 14:00 | Repaso y avance del día 2 | — | [Resumen](08-resumen.md) |

## Proyecto de ejemplo

La solución completa al final del día está en [src/day-01](../../src/day-01/).

```
src/day-01/
├── GestorIncidencias.slnx
└── GestorIncidencias.Web/
    ├── Program.cs                    ← host, servicios y pipeline
    ├── appsettings*.json             ← configuración por entorno
    ├── GestorIncidencias.Web.http    ← peticiones de prueba
    ├── Data/IncidenciasDbContext.cs  ← EF Core InMemory
    ├── Models/                       ← entidad y DTOs
    ├── Services/                     ← repositorio, lógica de negocio, IHostedService
    ├── Middleware/                   ← CorrelationId (convención) y TiempoRespuesta (IMiddleware)
    ├── Options/                      ← IncidenciasOptions (patrón Options)
    ├── Lifetimes/                    ← demo de Transient / Scoped / Singleton
    ├── Endpoints/                    ← Minimal APIs (incidencias + demos)
    └── wwwroot/                      ← ficheros estáticos
```

Para ejecutarlo:

```bash
cd src/day-01
dotnet run --project GestorIncidencias.Web
```

| URL | Qué muestra |
|---|---|
| http://localhost:5196/ | Página estática que consume la API |
| /api/incidencias | API REST (GET, POST, POST /{id}/resolver) |
| /info | Nombre de la aplicación y entorno |
| /ping | Rama del pipeline creada con `app.Map` |
| /demo/lifetimes | Comparativa de lifetimes de DI (solo Development) |
| /demo/opciones | `IOptions` vs `IOptionsSnapshot` vs `IOptionsMonitor` (solo Development) |
| /demo/configuracion | Fuentes de configuración y entorno (solo Development) |
| /demo/error | Excepción de prueba (página de error distinta según el entorno) |
| /openapi/v1.json | Documento OpenAPI (solo Development) |
