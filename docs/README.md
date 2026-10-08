# Planificación del curso — Desarrollo en ASP.NET Core

| | |
|---|---|
| **Duración** | 25 horas — 5 días, de 9:00 a 14:00 |
| **Metodología** | Teoría breve + demostración + laboratorio en cada bloque. La parte práctica supera el 40 % del tiempo. |
| **Proyecto conductor** | *Gestor de Incidencias*: una aplicación que crece cada día hasta ser una aplicación ASP.NET Core completa, y que sirve de referencia para migrar los casos SIREI (Web Forms) y Noticom (MVC). |
| **Base de datos** | EF Core con proveedor **InMemory** (sin instalaciones). |

## Distribución por días

La distribución de los días 2 a 5 es orientativa y se ajustará al ritmo del grupo.

| Día | Módulos del temario | Contenido | Proyecto al final del día |
|---|---|---|---|
| **1** | M1 completo + M2 (1ª parte) | Evolución de .NET · Web Forms vs ASP.NET Core · MVC, Razor Pages y API REST · Estructura de un proyecto · Middleware y pipeline · Inyección de dependencias · Configuración, opciones y entornos | API REST con Minimal APIs, middleware propio, DI, opciones y EF Core InMemory |
| **2** | M2 (2ª parte) | Clean Architecture · Hexagonal vs Clean vs Vertical Slice · Controladores MVC y vistas Razor · Razor Pages y controladores API. *Día con más teoría y demostración; laboratorios muy guiados* | Solución en capas (Domain / Application / Infrastructure / Web) con interfaz MVC, Razor Pages y API sobre los mismos casos de uso |
| **3** | M4 + M3 (1ª parte) | EF Core: configuración, relaciones y migraciones · Patrones de acceso a datos · De DataSet/DataTable a EF Core · Rendimiento de consultas · Análisis de aplicaciones legacy y estrategias de migración | Persistencia completa con EF Core; inicio del análisis de SIREI |
| **4** | M3 (2ª parte) + M5 + M6 | Web Forms → Razor/MVC: controles, eventos, estado (Session/ViewState) · Herramientas de Microsoft · Migración desde MVC 5 · Autenticación y autorización, Identity, OAuth2/OIDC · CSRF y XSS · Logging, rendimiento y pruebas | Aplicación securizada con Identity, logging estructurado y pruebas |
| **5** | M7 (caso práctico) | Método de migración por pantalla · Migración guiada de SIREI (Web Forms) y Noticom (MVC 5) · Ejercicios de controladores, vistas y servicios · Resolución de problemas comunes · Cierre del curso | Aplicaciones migradas como áreas de la solución (`/Sirei`, `/Noticom`), con pruebas |

## Índice de material

- [Día 1 — Introducción y fundamentos](day-01/README.md)
- [Día 2 — Clean Architecture y MVC con Razor](day-02/README.md)
- [Día 3 — Datos y persistencia con EF Core, y análisis de aplicaciones legacy](day-03/README.md)
- [Día 4 — Migración desde Web Forms y MVC 5, seguridad, logging y pruebas](day-04/README.md)
- [Día 5 — Caso práctico: migración de SIREI y Noticom, y cierre del curso](day-05/README.md)
  - [Cierre del curso: repaso, lista de comprobación, hoja de ruta y autoevaluación](day-05/05-cierre-curso.md)

## Referencias generales

> Enlaces comprobados el 4 de octubre de 2026. Cada documento del curso incluye sus propias referencias.

- [Documentación de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/?view=aspnetcore-10.0) — Documentación oficial en español.
- [Directiva de soporte técnico oficial de .NET](https://dotnet.microsoft.com/es-es/platform/support/policy/dotnet-core) — .NET 10 es la versión LTS vigente (soporte hasta el 14/11/2028).
- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0) — Guía oficial de migración (Módulo 3).
- [Documentación de Entity Framework Core](https://learn.microsoft.com/es-es/ef/core/) — Acceso a datos (Módulo 4).
