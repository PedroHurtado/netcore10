# Día 5 — Caso práctico: migración de SIREI y Noticom, y cierre del curso

**Módulos del temario:** Módulo 7 completo (caso práctico): migración de una aplicación Web Forms sencilla a ASP.NET Core (**SIREI**) · migración de una aplicación MVC sencilla a ASP.NET Core (**Noticom**) · ejercicios guiados de creación de controladores, vistas y servicios · resolución de problemas comunes. Y el **cierre** del curso.

## Enfoque del día

- Es el día **práctico**: casi dos tercios del tiempo son laboratorios (el temario pide un mínimo del 30-40 %).
- No hay temario nuevo: se aplica lo de los días 1 a 4 con **una receta única** de migración por pantalla ([capítulo 1](01-metodo-migracion.md)).
- El código legacy de partida está en [legacy/](legacy/): fragmentos representativos de SIREI (Web Forms) y Noticom (MVC 5), con las señales de alarma marcadas. Si tenéis el código real, mejor.
- La solución completa, con las dos aplicaciones migradas como **áreas** de la solución del curso, está en [src/day-05](../../src/day-05/).
- Se cierra con un **taller** sobre pantallas reales y el [repaso final del curso](05-cierre-curso.md).

## Objetivos

Al terminar el día el alumno será capaz de:

1. Aplicar una receta de migración pantalla a pantalla: leer el legacy, fijar el contrato de URLs, sacar las reglas al dominio y construir caso de uso, controlador y vistas.
2. Migrar pantallas Web Forms (eventos, `GridView`, `ViewState`, `Session`) a un área MVC con concurrencia optimista y URLs antiguas redirigidas.
3. Migrar una pantalla MVC 5 con mucho JavaScript: modos de pantalla como enum, columnas decididas en el servidor, AJAX con vistas parciales y `fetch`, JavaScript sin Razor compatible con la CSP.
4. Mapear el esquema de una base de datos existente con EF Core sin modificarla.
5. Diagnosticar los fallos más frecuentes de una migración a partir del síntoma (código HTTP, log, F12, pruebas).
6. Planificar la migración de una pantalla real de SIREI o Noticom.

## Agenda (9:00 – 14:00)

| Hora | Bloque | Tipo | Material |
|---|---|---|---|
| 9:00 – 9:15 | Repaso del día 4 y planteamiento del caso práctico | — | [Resumen día 4](../day-04/09-resumen.md) |
| 9:15 – 9:35 | **Método**: migrar una pantalla de principio a fin | Teoría | [01](01-metodo-migracion.md) |
| 9:35 – 9:50 | **Caso SIREI**: análisis y decisiones | Teoría + demo | [02](02-caso-sirei.md) |
| 9:50 – 11:00 | **Lab 1** — SIREI: de Web Forms a MVC | Práctica guiada | [Lab 1](labs/lab-01-sirei-expedientes.md) |
| 11:00 – 11:15 | *Descanso* | | |
| 11:15 – 11:30 | **Caso Noticom**: análisis y decisiones | Teoría + demo | [03](03-caso-noticom.md) |
| 11:30 – 12:30 | **Lab 2** — Noticom: de MVC 5 a ASP.NET Core | Práctica guiada | [Lab 2](labs/lab-02-noticom-lotes.md) |
| 12:30 – 12:45 | **Resolución de problemas comunes** | Teoría | [04](04-problemas-comunes.md) |
| 12:45 – 13:20 | **Lab 3** — Taller de averías | Práctica | [Lab 3](labs/lab-03-resolucion-problemas.md) |
| 13:20 – 13:40 | **Lab 4** — Plan de migración de una pantalla real + puesta en común | Taller | [Lab 4](labs/lab-04-taller-pantallas-reales.md) |
| 13:40 – 14:00 | **Cierre del curso**: repaso, hoja de ruta, autoevaluación y evaluación | — | [05](05-cierre-curso.md) |

Práctica: 70 + 60 + 35 + 20 = **185 de 300 minutos (62 %)**.

### Guion de las demostraciones

| Bloque | Qué enseñar en directo |
|---|---|
| 02 SIREI | Abrir [ExpedienteDetalle.aspx.cs](legacy/sirei/ExpedienteDetalle.aspx.cs) y señalar cada 🚩 · `Expediente.Actualizar` · `/Sirei/Expedientes/Detalle/1`: intentar cerrarlo (error, lo escrito se conserva) · dos pestañas y concurrencia (log `[2003]`) · `/ExpedienteDetalle.aspx?id=3` → 301 en F12 · `ExpedienteConfiguracion`: `HasColumnName("IdEstado")` |
| 03 Noticom | La vista real ([examples/prueba.txt](../../examples/prueba.txt)): `TipoEjecucion`, `ocultarColumnaGridview`, `_Logon_` · las cuatro pestañas de `/Noticom` como luis y como ana (columnas distintas, sin JS) · F12 → Red: `Tabla?...` con `X-Requested-With` · la URL cambia al paginar · consola sin errores de CSP · crear una remesa como ana |
| 04 Problemas | La tabla de códigos HTTP del capítulo 4.1 · la página de excepción con un servicio sin registrar · F12 → Consola con un script inline |

## Proyecto de ejemplo

La solución completa al final del curso está en [src/day-05](../../src/day-05/). Parte de `src/day-04` e incorpora los **laboratorios del día 4** (búsqueda por texto, política `GestionarIncidencias` y sus pruebas) y los **laboratorios 1 y 2 de hoy**, con sus ampliaciones.

```
src/day-05/
├── GestorIncidencias.Domain/
│   ├── Expedientes/                         ← NUEVO (SIREI): Expediente, Tramite, EstadoExpediente
│   └── Lotes/                               ← NUEVO (Noticom): Lote, Remesa, EstadoLote, TipoNotificacion
├── GestorIncidencias.Application/
│   ├── Abstracciones/                       ← + IExpedienteRepository/Consultas, ILoteRepository/Consultas
│   ├── Expedientes/                         ← NUEVO: ExpedienteService (concurrencia optimista), DTOs, logs 2001-2004
│   ├── Lotes/                               ← NUEVO: LoteService, DTOs, FiltroLotes, logs 3001-3004
│   └── Incidencias/                         ← + búsqueda por texto (lab 1 del día 4)
├── GestorIncidencias.Infrastructure/
│   ├── Sirei/                               ← NUEVO: SireiDbContext sobre el esquema de SIREI (IdEstado, IdExpediente)
│   ├── Noticom/                             ← NUEVO: NoticomDbContext (el EDMX de EF6 en API fluida)
│   └── Persistencia/IncidenciasDbContext.cs ← solo aplica SUS configuraciones (filtro por espacio de nombres)
├── GestorIncidencias.Web/
│   ├── Areas/Sirei/                         ← NUEVO: ExpedientesController + vistas + URLs .aspx (301)
│   ├── Areas/Noticom/                       ← NUEVO: LotesController (página + fragmento AJAX) + vistas
│   ├── wwwroot/js/noticom/lotes.js          ← NUEVO: fetch, sin Razor, sin jQuery, mejora progresiva
│   ├── Seguridad/Politicas.cs               ← GestionarIncidencias (lab 2 día 4) + GestionarLotes
│   ├── Program.cs                           ← rutas de las áreas; políticas
│   └── Views/Shared/_Layout.cshtml          ← enlaces a SIREI y Noticom; asp-area="" en el menú
└── tests/
    ├── GestorIncidencias.UnitTests/         ← + ExpedienteTests, LoteTests, ExpedienteServiceTests, LoteServiceTests
    └── GestorIncidencias.IntegrationTests/  ← + AutorizacionTests (día 4), CasoPracticoTests (login por formulario)
```

Para ejecutarlo:

```bash
cd src/day-05
dotnet run --project GestorIncidencias.Web
dotnet test                                   # 64 pruebas
```

| URL | Qué muestra |
|---|---|
| http://localhost:5196/ | Panel, con acceso a las aplicaciones migradas |
| /Incidencias?texto=correo | Incidencias con búsqueda por texto (lab 1 del día 4) |
| /Sirei | Búsqueda de expedientes (SIREI migrado) |
| /Sirei/Expedientes/Detalle/1 | Ficha con trámites pendientes: no se puede cerrar |
| /ExpedienteDetalle.aspx?id=3 | URL antigua → 301 a la nueva |
| /Noticom | Lotes en modo Consulta; pestañas Validacion, CrearRemesa, Borrado y Remesas |
| /salud | Comprobación de salud (incluye las bases de datos `sirei` y `noticom`) |

Usuarios de demostración (contraseña en `appsettings.Development.json`):

| Usuario | Rol | Incidencias | SIREI | Noticom |
|---|---|---|---|---|
| `ana@demo.local` | Tecnico | Ver y gestionar | Todo | Consultar, validar, crear remesas |
| `luis@demo.local` | — | Ver, crear y comentar | Todo | Solo consultar |
| `admin@demo.local` | Administrador | Todo | Todo | Todo, incluido borrar lotes |

## Referencias

> Enlaces comprobados el 9 de octubre de 2026. Cada documento de teoría y cada laboratorio tiene su propia sección de referencias.

- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0)
- [Áreas en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/mvc/controllers/areas?view=aspnetcore-10.0)
- [Control de conflictos de simultaneidad (EF Core)](https://learn.microsoft.com/es-es/ef/core/saving/concurrency)
- [Vistas parciales](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/partial?view=aspnetcore-10.0)
- [Control de errores en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
