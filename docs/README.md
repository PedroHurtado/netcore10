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
| **2** | M2 (2ª parte) | Controladores y vistas (MVC) · Razor Pages: cuándo usarlas · Clean Architecture | Solución en capas (Domain / Application / Infrastructure / Web) con interfaz MVC y Razor Pages |
| **3** | M4 + M3 (1ª parte) | EF Core: configuración, relaciones y migraciones · Patrones de acceso a datos · De DataSet/DataTable a EF Core · Rendimiento de consultas · Análisis de aplicaciones legacy y estrategias de migración | Persistencia completa con EF Core; inicio del análisis de SIREI |
| **4** | M3 (2ª parte) + M5 + M6 | Web Forms → Razor/MVC: controles, eventos, estado (Session/ViewState) · Herramientas de Microsoft · Migración desde MVC 5 · Autenticación y autorización, Identity, OAuth2/OIDC · CSRF y XSS · Logging, rendimiento y pruebas | Aplicación securizada con Identity, logging estructurado y pruebas |
| **5** | M7 (caso práctico) | Migración guiada de SIREI (Web Forms) y Noticom (MVC) · Ejercicios de controladores, vistas y servicios · Resolución de problemas comunes | Aplicaciones migradas |

## Índice de material

- [Día 1 — Introducción y fundamentos](day-01/README.md)
