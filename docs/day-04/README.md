# Día 4 — Migración desde Web Forms y MVC 5, seguridad, logging y pruebas

**Módulos del temario:** Módulo 3, 2ª parte (conversión de lógica de negocio y eventos · migración de controles y vistas de Web Forms a Razor/MVC · gestión del estado · herramientas y guías oficiales de Microsoft · estrategias de migración desde MVC), Módulo 5 completo (autenticación y autorización · Identity y OAuth2/OpenID Connect · migración de sistemas de autenticación legacy · CSRF y XSS) y Módulo 6 completo (optimización de rendimiento · logging y monitorización · herramientas para pruebas y validación).

## Enfoque del día

- Es el día con **más temario**: tres módulos. Se mantiene el estilo de los días 2 y 3: **teoría breve sobre el código ya terminado** de [src/day-04](../../src/day-04/), con demostraciones en vivo.
- La **migración** (capítulos 1 a 3) se explica con ejemplos de SIREI y Noticom: es la preparación directa del caso práctico de mañana.
- La **seguridad** se ve funcionando: la aplicación tiene ya inicio de sesión con Identity, usuarios de demostración, roles, política por defecto y token para la API. **OAuth2/OIDC se explica, no se practica** (necesita un proveedor de identidad real).
- La práctica ronda el 27 % del tiempo con **tres laboratorios guiados**, con todo el código (comprobado paso a paso) y tablas de errores típicos: uno de migración, uno de seguridad y uno de pruebas.

## Objetivos

Al terminar el día el alumno será capaz de:

1. Traducir una página Web Forms (controles, eventos, *postbacks*) a controlador + vista Razor, repartiendo la lógica del *code-behind* por las capas.
2. Sustituir ViewState y el abuso de Session por ruta, query string, formulario, sesión bien usada o caché, según el caso.
3. Enumerar los cambios necesarios para llevar una aplicación ASP.NET MVC 5 a ASP.NET Core y qué herramientas oficiales ayudan.
4. Configurar autenticación con ASP.NET Core Identity y autorizar por rol y por política, seguro por defecto.
5. Explicar OAuth2/OIDC y elegir cómo migrar la autenticación de una aplicación legacy (Forms, Membership, Identity 2, Windows).
6. Explicar y aplicar las defensas contra CSRF, XSS y *open redirect*.
7. Escribir logs estructurados útiles y seguros, y exponer una comprobación de salud.
8. Escribir pruebas unitarias (dominio y casos de uso con dobles) y de integración (`WebApplicationFactory`).

## Agenda (9:00 – 14:00)

| Hora | Bloque | Tipo | Material |
|---|---|---|---|
| 9:00 – 9:15 | Repaso del día 3 y puesta en común del análisis de SIREI | — | [Resumen día 3](../day-03/08-resumen.md), [Lab 3 del día 3](../day-03/labs/lab-03-analisis-sirei.md) |
| 9:15 – 9:50 | **De Web Forms a MVC**: controles, eventos y lógica de negocio | Teoría + demo | [01](01-webforms-a-mvc.md) |
| 9:50 – 10:15 | **Gestión del estado**: ViewState, Session y alternativas | Teoría + demo | [02](02-gestion-estado.md) |
| 10:15 – 10:35 | **Migración desde MVC 5** y herramientas de Microsoft | Teoría | [03](03-migracion-mvc5-herramientas.md) |
| 10:35 – 11:05 | **Lab 1** — De Web Forms a MVC: la búsqueda de incidencias | Práctica guiada | [Lab 1](labs/lab-01-busqueda-webforms.md) |
| 11:05 – 11:20 | *Descanso* | | |
| 11:20 – 11:55 | **Autenticación y autorización** con Identity | Teoría + demo | [04](04-autenticacion-autorizacion.md) |
| 11:55 – 12:15 | **OAuth2/OIDC** y migración de la autenticación legacy | Teoría | [05](05-oauth-oidc-y-legacy.md) |
| 12:15 – 12:30 | **CSRF y XSS** | Teoría + demo | [06](06-csrf-xss.md) |
| 12:30 – 13:00 | **Lab 2** — Solo los técnicos gestionan incidencias | Práctica guiada | [Lab 2](labs/lab-02-politica-tecnicos.md) |
| 13:00 – 13:20 | **Logging, monitorización** y rendimiento | Teoría + demo | [07](07-logging-monitorizacion.md) |
| 13:20 – 13:30 | **Pruebas** unitarias y de integración | Teoría + demo | [08](08-pruebas.md) |
| 13:30 – 13:52 | **Lab 3** — Pruebas unitarias y de integración | Práctica guiada | [Lab 3](labs/lab-03-pruebas.md) |
| 13:52 – 14:00 | Repaso y avance del día 5 | — | [Resumen](09-resumen.md) |

### Guion de las demostraciones

| Bloque | Qué enseñar en directo |
|---|---|
| 01 Web Forms → MVC | Pizarra: el ciclo GET/*postback* frente a GET/POST · recorrer la tabla de controles señalando cada equivalencia en `Views/Incidencias/` · el `ExpedienteDetalle.aspx` del día 3 convertido (apartado 1.4) · comparar `IncidenciasController` con `Pages/Paginas/Incidencias/Index.cshtml.cs` |
| 02 Estado | Abrir 3 o 4 fichas y volver al listado ("Vistas recientemente") · F12 → Aplicación → Cookies: `.GestorIncidencias.Sesion` (HttpOnly) · `HistorialVisitas.cs` · cerrar sesión y ver que el historial desaparece · `ListarCategoriasAsync` con `HybridCache` |
| 03 MVC 5 | Recorrer las tablas con el código de Noticom si está disponible · la conversión "a mano" del apartado 3.3 |
| 04 Identity | Abrir `/Incidencias` sin sesión (302 con `ReturnUrl`) · entrar como luis · `/Usuarios` como luis (acceso denegado) y como admin · 5 contraseñas incorrectas y ver el bloqueo en `/Usuarios` · F12: la cookie `.GestorIncidencias.Auth` · el orden del pipeline en Program.cs · el `.http`: 401 sin token, pedir token, comentar y ver el autor |
| 05 OIDC | Pizarra: el flujo *Authorization Code* · qué cambiaría en Program.cs con Entra ID |
| 06 CSRF / XSS | Crear una incidencia con `<script>alert(1)</script>` en el título y verla como texto · ver el campo `__RequestVerificationToken` en el HTML · `curl -X POST .../Cuenta/Login` sin token → 400 · `/Cuenta/Login?ReturnUrl=https://example.com` y entrar: vuelve a `/` |
| 07 Logging | Consola: el EventId `[1001]` y el `CorrelationId` al crear una incidencia; el `dbug [1004]` al cerrar una abierta por la API · arrancar con `ASPNETCORE_ENVIRONMENT=Production` y ver el JSON · `GET /salud` |
| 08 Pruebas | `dotnet test` · abrir una prueba de cada tipo · romper la regla de `Cerrar` en el dominio y ver qué prueba falla |

## Proyecto de ejemplo

La solución completa al final del día está en [src/day-04](../../src/day-04/). Parte de `src/day-03` e incorpora ya los **laboratorios 1 y 2 del día 3** (cambiar la categoría e informe por prioridad, con sus ampliaciones en la API). Los laboratorios de hoy **no** están incluidos: los hace cada alumno sobre su copia.

```
src/day-04/
├── global.json                              ← NUEVO: "dotnet test" con Microsoft.Testing.Platform
├── GestorIncidencias.Domain/
│   └── Incidencias/Incidencia.cs            ← + CambiarCategoria (lab 1 del día 3)
├── GestorIncidencias.Application/
│   ├── Abstracciones/IUsuarioActual.cs      ← NUEVO: quién hace la petición
│   ├── Seguridad/Roles.cs                   ← NUEVO: Roles.Tecnico, Roles.Administrador, TiposClaim
│   ├── Configuracion/IncidenciasOptions.cs  ← + ClaveUsuariosDemo
│   └── Incidencias/
│       ├── IncidenciaService.cs             ← autor = usuario actual; CambiarCategoria; ResumenPorPrioridad
│       └── IncidenciaLog.cs                 ← NUEVO: mensajes de log con [LoggerMessage]
├── GestorIncidencias.Infrastructure/        ← + FrameworkReference ASP.NET Core (Identity)
│   ├── DependencyInjection.cs               ← + Identity (contraseñas, bloqueo), HybridCache, health check
│   ├── Identidad/
│   │   ├── IdentidadDbContext.cs            ← NUEVO: tablas AspNet* (contexto separado)
│   │   └── InicializadorIdentidad.cs        ← NUEVO: roles y usuarios de demostración
│   └── Persistencia/
│       ├── EfIncidenciaConsultas.cs         ← categorías con HybridCache; ResumenPorPrioridad
│       └── InicializadorBaseDatos.cs        ← idempotente (para las pruebas de integración)
├── GestorIncidencias.Web/
│   ├── Program.cs                           ← cookie, token, FallbackPolicy, antiforgery global, sesión, HTTP logging, /salud
│   ├── appsettings.Production.json          ← NUEVO: logs en JSON
│   ├── Seguridad/                           ← NUEVO: UsuarioActualHttp, Esquemas
│   ├── Estado/HistorialVisitas.cs           ← NUEVO: ejemplo de Session
│   ├── Controllers/
│   │   ├── CuentaController.cs              ← NUEVO: login, logout, acceso denegado
│   │   ├── UsuariosController.cs            ← NUEVO: [Authorize(Roles = Administrador)]
│   │   ├── Api/CuentaApiController.cs       ← NUEVO: POST /api/cuenta/token
│   │   └── Api/IncidenciasApiController.cs  ← solo con token; PUT categoria; resumen/prioridades
│   ├── Views/Cuenta/, Views/Usuarios/       ← NUEVO
│   └── Views/...                            ← usuario en la cabecera, categoría en la ficha, "Vistas recientemente", "Por prioridad"
└── tests/                                   ← NUEVO
    ├── GestorIncidencias.UnitTests/         ← Dominio + casos de uso (xUnit v3, NSubstitute, FakeTimeProvider)
    └── GestorIncidencias.IntegrationTests/  ← WebApplicationFactory: seguridad y API
```

Para ejecutarlo:

```bash
cd src/day-04
dotnet run --project GestorIncidencias.Web
dotnet test                                   # 27 pruebas
```

| URL | Qué muestra |
|---|---|
| http://localhost:5196/ | Panel (público) con **Por categoría** y **Por prioridad** |
| /Cuenta/Login | Inicio de sesión (en Development, con la tabla de usuarios de demostración) |
| /Incidencias | Listado paginado (requiere sesión) con "Vistas recientemente" |
| /Incidencias/Detalle/2 | Ficha: cambiar categoría y comentar como el usuario actual |
| /Paginas/Incidencias | Listado con Razor Pages |
| /Usuarios | Usuarios, roles y bloqueos (solo **admin**) |
| /api/incidencias | API: **401** sin token. Usar `GestorIncidencias.Web.http` |
| /salud | Comprobación de salud (pública) |
| /openapi/v1.json | Documento OpenAPI (solo Development) |

Usuarios de demostración (solo en Development; la contraseña está en `appsettings.Development.json`, clave `Incidencias:ClaveUsuariosDemo`):

| Usuario | Nombre | Rol |
|---|---|---|
| `ana@demo.local` | Ana García | Tecnico |
| `luis@demo.local` | Luis Pérez | — |
| `admin@demo.local` | Marta Ruiz | Administrador |

Como seguimos con InMemory, **cada arranque empieza de cero**: incidencias, usuarios, bloqueos y comentarios.

## Referencias

> Enlaces comprobados el 8 de octubre de 2026. Cada documento de teoría y cada laboratorio tiene su propia sección de referencias.

- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0)
- [Introducción a Identity](https://learn.microsoft.com/es-es/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)
- [Introducción a la autorización](https://learn.microsoft.com/es-es/aspnet/core/security/authorization/introduction?view=aspnetcore-10.0)
- [Prevención de ataques CSRF](https://learn.microsoft.com/es-es/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0) y [XSS](https://learn.microsoft.com/es-es/aspnet/core/security/cross-site-scripting?view=aspnetcore-10.0)
- [Registro en .NET y ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/logging/?view=aspnetcore-10.0)
- [Pruebas de integración en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
