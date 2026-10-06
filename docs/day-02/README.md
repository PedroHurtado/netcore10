# Día 2 — Clean Architecture y MVC con Razor

**Módulos del temario:** Módulo 2 (2ª parte): controladores y vistas, Razor Pages, arquitectura limpia.

## Enfoque del día

El día 2 da **más peso a la teoría y a las demostraciones en vivo** que a los laboratorios. El grupo tiene poca experiencia previa en ASP.NET Core, así que:

- La arquitectura y MVC se explican **sobre el código ya terminado** de [src/day-02](../../src/day-02/), que el formador recorre en directo.
- Solo hay **dos laboratorios**, muy guiados: el primero no exige escribir código (recorrer y "romper" la arquitectura) y el segundo da **todo el código para copiar**, con comprobaciones en cada paso y una tabla de errores típicos.
- La práctica ocupa alrededor del 20 % del tiempo (frente al 40 % del día 1).

## Objetivos

Al terminar el día el alumno será capaz de:

1. Explicar por qué el código de negocio no debe depender de la interfaz ni de la base de datos.
2. Describir las cuatro capas de Clean Architecture y la regla de dependencia, y localizarlas en la solución.
3. Comparar Hexagonal, Clean y Vertical Slice, con sus pros, contras y dónde conviene cada una.
4. Leer y modificar un controlador MVC: rutas, acciones, model binding, validación, PRG y antiforgery.
5. Leer y modificar vistas Razor: sintaxis, layout, parciales y Tag Helpers de formulario.
6. Reconocer la misma funcionalidad hecha con Razor Pages y con un controlador API.
7. Añadir una funcionalidad sencilla atravesando todas las capas.

## Agenda (9:00 – 14:00)

| Hora | Bloque | Tipo | Material |
|---|---|---|---|
| 9:00 – 9:15 | Repaso del día 1 y objetivos | — | [Resumen día 1](../day-01/08-resumen.md) |
| 9:15 – 9:35 | ¿Por qué separar en capas? Del *code-behind* a la regla de dependencia | Teoría | [01](01-del-monolito-a-capas.md) |
| 9:35 – 10:25 | **Clean Architecture paso a paso** sobre el proyecto | Teoría + demo | [02](02-clean-architecture.md) |
| 10:25 – 11:10 | **Hexagonal vs Clean vs Vertical Slice** | Teoría + debate | [03](03-hexagonal-clean-vertical-slice.md) |
| 11:10 – 11:30 | **Lab 1** — Recorrido guiado por las capas | Práctica guiada | [Lab 1](labs/lab-01-recorrido-capas.md) |
| 11:30 – 11:45 | *Descanso* | | |
| 11:45 – 12:25 | **Controladores MVC** (demo en vivo) | Teoría + demo | [04](04-controladores-mvc.md) |
| 12:25 – 13:05 | **Vistas Razor y Tag Helpers** (demo en vivo) | Teoría + demo | [05](05-vistas-razor.md) |
| 13:05 – 13:40 | **Lab 2** — "Reabrir incidencia" de punta a punta | Práctica guiada | [Lab 2](labs/lab-02-reabrir-incidencia.md) |
| 13:40 – 13:55 | Razor Pages y controladores API sobre los mismos casos de uso | Teoría + demo | [06](06-razor-pages-y-api.md) |
| 13:55 – 14:00 | Repaso y avance del día 3 | — | [Resumen](07-resumen.md) |

**Material complementario** (fuera de la agenda): [08 — Optimización de la entrega](08-optimizacion-entrega.md): minificación de HTML y CSS, caché del CSS con huella e `immutable`, Output Cache, por qué la compresión del HTML se deja al proxy y la CSP que impide JavaScript y CSS inline.

### Guion de las demostraciones

| Bloque | Qué enseñar en directo |
|---|---|
| 02 Clean | Abrir los 4 `.csproj` · `Incidencia.cs` (setters privados, `Crear`, `Resolver`) · `IncidenciaService.CambiarEstadoAsync` · `IIncidenciaRepository` en Application y su implementación en Infrastructure · `Program.cs` con `AddApplication()` / `AddInfrastructure()` |
| 03 Comparativa | Señalar en el proyecto el puerto de entrada (`IIncidenciaService`) con sus tres adaptadores · escribir en la pizarra la rebanada `ResolverIncidencia` del apartado 3.4 · debatir las preguntas finales |
| 04 Controladores | Navegar por `/Incidencias`, `/Incidencias?estado=Abierta`, `/Incidencias/Detalle/1` · crear una incidencia con título corto (error de campo) y con el límite superado (error general) · enseñar el 302 y el F5 en las herramientas del navegador (pestaña Red) · quitar el token y ver el 400 |
| 05 Vistas | Ver el código fuente HTML generado por `Crear.cshtml` (atributos `data-val`, token oculto) · crear una incidencia con `<script>` en el título y ver que se muestra como texto · explicar `_Layout`, `_ViewStart`, `_ViewImports` y la parcial `_Estado` |
| 06 Razor Pages / API | Abrir `/Paginas/Incidencias` y comparar con `/Incidencias` · ejecutar las peticiones de `GestorIncidencias.Web.http` (400 automático, 201 + Location, 409) |

## Proyecto de ejemplo

La solución completa al final del día está en [src/day-02](../../src/day-02/).

```
src/day-02/
├── GestorIncidencias.slnx
├── Directory.Build.props                  ← net10.0, Nullable e ImplicitUsings para todos
├── GestorIncidencias.Domain/              ← SIN dependencias
│   ├── Comun/Resultado.cs                 ← éxito/error + TipoError
│   └── Incidencias/                       ← Incidencia (entidad rica), Prioridad, EstadoIncidencia
├── GestorIncidencias.Application/         → Domain
│   ├── Abstracciones/IIncidenciaRepository.cs   ← puerto de salida
│   ├── Configuracion/IncidenciasOptions.cs
│   ├── Incidencias/                       ← IIncidenciaService + IncidenciaService (casos de uso), DTOs
│   └── DependencyInjection.cs             ← AddApplication()
├── GestorIncidencias.Infrastructure/      → Application
│   ├── Persistencia/                      ← DbContext, configuración EF, repositorio, datos demo
│   └── DependencyInjection.cs             ← AddInfrastructure()
└── GestorIncidencias.Web/                 → Application, Infrastructure
    ├── Program.cs                         ← raíz de composición y pipeline
    ├── Controllers/                       ← HomeController, IncidenciasController (MVC)
    ├── Controllers/Api/                   ← IncidenciasApiController ([ApiController])
    ├── Models/                            ← ViewModels (IncidenciaFormulario...)
    ├── Views/                             ← layout, parciales y vistas Razor
    ├── Pages/Paginas/Incidencias/         ← la misma funcionalidad con Razor Pages
    ├── Middleware/                        ← CorrelationId (del día 1) y Content-Security-Policy (capítulo 8)
    ├── Cache/CacheHtmlPolicy.cs           ← política de Output Cache para el HTML (capítulo 8)
    ├── GestorIncidencias.Web.csproj       ← incluye la tarea que minifica site.css → site.min.css (capítulo 8)
    └── wwwroot/css/site.css               ← se edita este; site.min.css se genera al compilar
```

Para ejecutarlo:

```bash
cd src/day-02
dotnet run --project GestorIncidencias.Web
```

| URL | Qué muestra |
|---|---|
| http://localhost:5196/ | Panel de inicio (MVC) |
| /Incidencias | Listado con filtro por estado (MVC) |
| /Incidencias/Detalle/1 | Ficha con botones Iniciar / Resolver / Cerrar |
| /Incidencias/Crear | Formulario con validación |
| /Paginas/Incidencias | Listado con Razor Pages (botón Resolver con *handler*) |
| /Paginas/Incidencias/Crear | Formulario con Razor Pages |
| /api/incidencias | API REST con controlador (GET, POST, `/{id}/iniciar`, `/resolver`, `/cerrar`) |
| /openapi/v1.json | Documento OpenAPI (solo Development) |

En Development se cargan 4 incidencias de ejemplo y el límite de incidencias abiertas es 10 (ver `appsettings.Development.json`).

## Referencias

> Enlaces comprobados el 6 de octubre de 2026. Cada documento de teoría y cada laboratorio tiene su propia sección de referencias.

- [Arquitecturas de aplicaciones web comunes](https://learn.microsoft.com/es-es/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures) — Libro electrónico oficial de Microsoft (Clean Architecture en ASP.NET Core).
- [Información general de ASP.NET Core MVC](https://learn.microsoft.com/es-es/aspnet/core/mvc/overview?view=aspnetcore-10.0)
- [Arquitectura y conceptos de Razor Pages](https://learn.microsoft.com/es-es/aspnet/core/razor-pages/?view=aspnetcore-10.0)
