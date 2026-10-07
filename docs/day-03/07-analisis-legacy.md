# 7. Análisis de aplicaciones legacy y estrategias de migración

Este capítulo abre el **Módulo 3** (migración desde .NET Framework). Antes de convertir ni una página de SIREI a Razor (día 4) hay que responder tres preguntas:

1. **¿Qué tenemos?** (inventario)
2. **¿Qué hacemos con cada pieza?** (migrar, refactorizar, reescribir, retirar)
3. **¿En qué orden y cómo conviven lo viejo y lo nuevo mientras tanto?** (estrategia)

> Migrar no es traducir línea a línea. Es la ocasión de **sacar la lógica de negocio de las páginas** (lo que hicimos el día 2) y de **arreglar el acceso a datos** (lo de esta mañana). Pero cuidado: también es la forma más rápida de convertir un proyecto de 3 meses en uno de 18. El análisis sirve para decidir **qué no** hacer.

## 7.1 Inventario: qué hay que mirar

| Área | Qué buscar | Dónde | Por qué importa |
|---|---|---|---|
| **Pantallas** | Nº de `.aspx`/`.ascx`, cuáles se usan de verdad | Proyecto + logs de IIS | Las que nadie usa no se migran |
| **Lógica de negocio** | ¿En el *code-behind*? ¿En una capa BLL? ¿En procedimientos almacenados? | `*.aspx.cs`, `App_Code`, BD | Decide cuánto hay que reescribir |
| **Acceso a datos** | `SqlConnection`, `DataSet`, `.xsd`, `SqlDataSource`, EF6/EDMX | Código y markup | Capítulo 5 |
| **Estado** | `Session[...]`, `ViewState[...]`, `Application[...]`, `Cache[...]`, campos ocultos | Código | ViewState no existe en ASP.NET Core; Session funciona distinto (día 4) |
| **Controles** | `GridView`, `UpdatePanel`, controles de terceros (Telerik, DevExpress, AjaxControlToolkit) | Markup `.aspx` | Los de terceros pueden no tener equivalente |
| **Seguridad** | Forms Authentication, Windows, Membership, roles en `Web.config` | `Web.config`, `Global.asax` | Día 4 (Identity, OIDC) |
| **Infraestructura de `System.Web`** | `Global.asax`, `HttpModule`, `HttpHandler` (`.ashx`), `HttpContext.Current` | Proyecto | No existen en ASP.NET Core: se convierten en middleware |
| **Configuración** | `Web.config`: cadenas, `appSettings`, transformaciones | `Web.config` | Pasa a `appsettings.json` + opciones (día 1) |
| **Integraciones** | Servicios WCF/ASMX, colas, ficheros compartidos, correo, COM | Referencias de servicio | WCF servidor no existe en .NET moderno (alternativas: CoreWCF, REST, gRPC) |
| **Informes** | Crystal Reports, ReportViewer (RDLC) | Proyecto | Suelen ser el mayor bloqueo |
| **Dependencias** | Paquetes NuGet / DLL sueltas: ¿tienen versión para .NET moderno? | `packages.config`, `bin/` | Una sola DLL sin versión moderna puede condicionar todo |
| **Pruebas** | ¿Hay? ¿Qué cubren? | Solución | Sin pruebas, la migración es a ciegas: se escriben antes |

## 7.2 Decidir qué hacer con cada pieza

Para cada pantalla o módulo, dos preguntas: **¿cuánto valor aporta?** y **¿cuánto cuesta migrarlo?**

```
           Valor de negocio
                ▲
         alto   │  REFACTORIZAR          MIGRAR PRIMERO
                │  (vale la pena,        (se usa mucho y es
                │   pero con cuidado)     fácil: victoria rápida)
                │
         bajo   │  RETIRAR / NO MIGRAR   MIGRAR "TAL CUAL"
                │  (¿alguien lo usa?)    (si sale casi gratis)
                └───────────────────────────────────────────▶
                     alto                    bajo        Coste / riesgo
```

| Decisión | Significa | Ejemplo |
|---|---|---|
| **Retirar** | No se migra; se elimina o se sustituye por otra herramienta | Pantalla de "exportar a Access" que nadie usa desde 2019 |
| **Migrar tal cual** | Misma lógica y estructura, nueva tecnología | Una consulta sencilla: `GridView` → tabla Razor |
| **Refactorizar** | Se migra separando capas: lógica a Domain/Application, datos a Infrastructure | La pantalla principal de expedientes, con reglas en el `btnGuardar_Click` |
| **Reescribir** | Se diseña de nuevo; el legacy solo sirve de especificación | Un proceso que nadie entiende y que se rehace a partir de entrevistas con usuarios |
| **Mantener (de momento)** | Sigue en .NET Framework, convive con lo nuevo | Los informes de Crystal Reports, hasta tener alternativa |

**Qué se suele refactorizar siempre:** la lógica de negocio que está en el *code-behind* (pasa al dominio), el SQL que está en las páginas (pasa a Infrastructure) y la configuración leída con `ConfigurationManager` (pasa a opciones).

**Qué se suele dejar como está:** el esquema de la base de datos (otras aplicaciones lo usan), los procedimientos almacenados probados, y el diseño visual (salvo que se pida lo contrario: migrar y rediseñar a la vez duplica el riesgo).

## 7.3 Estrategias: migración completa o coexistencia

### Migración completa (*in situ*, "big bang")

Se reescribe toda la aplicación en ASP.NET Core y, un día, se sustituye la vieja por la nueva.

| ✅ A favor | ❌ En contra |
|---|---|
| Sencilla de entender y de organizar | Nada se puede usar hasta el final |
| Sin infraestructura de convivencia | El legacy sigue cambiando mientras tanto (hay que portar los cambios dos veces) |
| Adecuada para aplicaciones **pequeñas** | El día del cambio concentra todo el riesgo |

### Migración incremental (coexistencia): el patrón *Strangler Fig*

El nombre viene de la **higuera estranguladora**, que crece alrededor de un árbol hasta sustituirlo. Se pone **delante** de la aplicación vieja una aplicación ASP.NET Core que actúa de **proxy inverso** (con **YARP**). Al principio lo reenvía todo al legacy; poco a poco, cada ruta migrada la atiende la aplicación nueva.

```
                         ┌───────────────────────────────────────┐
 Navegador ──────────▶   │  Aplicación ASP.NET Core (.NET 10)    │
                         │                                       │
                         │  /Expedientes/*   → atendido aquí ✅  │
                         │  /Informes/*      → atendido aquí ✅  │
                         │  todo lo demás    → YARP ─────────────┼──▶  SIREI (Web Forms, .NET Framework 4.8)
                         └───────────────┬───────────────────────┘          │
                                         │   System.Web adapters            │
                                         │   (sesión y autenticación        │
                                         │    compartidas)                  │
                                         └──────────── Base de datos ◀──────┘
                                                       (compartida)
```

| ✅ A favor | ❌ En contra |
|---|---|
| Se entrega valor desde el primer mes | Dos aplicaciones en producción durante un tiempo |
| El riesgo se reparte: si algo falla, se devuelve esa ruta al legacy | Hay que compartir sesión, autenticación y BD |
| Se puede parar en cualquier momento con algo útil | Más infraestructura (proxy, configuración de ambos lados) |

La guía oficial de Microsoft considera la **incremental el enfoque preferido para la mayoría de aplicaciones en producción**, y la completa adecuada para aplicaciones **suficientemente pequeñas**.

### ¿Cuál elegir?

| Pregunta | Si la respuesta es "sí"... |
|---|---|
| ¿Tiene pocas pantallas (≈ 10-20) y poca lógica? | Completa |
| ¿Se puede congelar el legacy durante la migración? | Completa es viable |
| ¿Es crítica y no puede estar meses sin cambios? | Incremental |
| ¿Hay módulos con valor muy distinto (unos urgentes, otros que pueden esperar)? | Incremental |
| ¿Tiene piezas que no se pueden migrar todavía (Crystal Reports, COM)? | Incremental (esas rutas se quedan en el legacy) |

## 7.4 Servicios compartidos durante la convivencia

Mientras conviven las dos aplicaciones, tienen que **compartir** cosas:

| Qué | Cómo |
|---|---|
| **Base de datos** | La misma BD para las dos. La nueva **no** cambia el esquema por su cuenta (sin migraciones de EF Core, o solo aditivas y acordadas) |
| **Lógica de negocio** | Biblioteca compartida que compilen las dos: **.NET Standard 2.0** o multi-destino (`<TargetFrameworks>net48;net10.0</TargetFrameworks>`). Nuestro `Domain` (sin dependencias) es el candidato perfecto |
| **Autenticación** | **System.Web adapters**: autenticación remota (la app nueva pregunta a la vieja quién es el usuario) o cookie compartida (si el legacy usa autenticación por cookie OWIN) |
| **Sesión** | **System.Web adapters**: sesión remota (la app nueva lee y escribe la `Session` del legacy a través de una API) |
| **Funcionalidad expuesta** | El legacy publica una API (o al revés) y la otra parte la consume detrás de una interfaz: una **capa anticorrupción** que traduce el modelo viejo al nuevo |

```csharp
// Biblioteca compartida durante la convivencia (GestorIncidencias.Domain.csproj)
<PropertyGroup>
  <TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
```

> Esto es lo que da sentido a todo lo del día 2: si el dominio **no depende** de ASP.NET Core ni de EF Core, se puede compilar para .NET Framework y **usarlo desde SIREI** antes incluso de migrar sus páginas. La lógica se migra **una vez** y la usan las dos aplicaciones.
>
> Letra pequeña: en `netstandard2.0` algunas novedades de C# necesitan ayuda. Por ejemplo, los `record` (como `Resultado`) requieren declarar el tipo `System.Runtime.CompilerServices.IsExternalInit` o usar un paquete de *polyfills*. Se resuelve en una tarde, pero conviene saberlo antes de prometerlo.

## 7.5 Herramientas oficiales (avance del día 4)

| Herramienta | Estado (octubre de 2026) |
|---|---|
| **.NET Upgrade Assistant** | **Obsoleta** oficialmente. Seguiréis encontrándola en artículos y tutoriales |
| **GitHub Copilot upgrade** (antes "app modernization") | La que recomienda Microsoft: agente en Visual Studio 2026 / VS Code que analiza la solución, propone un plan y aplica cambios (incluye rutas desde Web Forms) |
| **System.Web adapters** + **YARP** | Paquetes para la migración incremental |
| Guía *Migración de ASP.NET Framework a ASP.NET Core* | Documentación oficial con la guía de decisión y las áreas técnicas (sesión, autenticación, módulos, handlers...) |

Las herramientas automáticas ayudan con lo **mecánico** (proyectos, paquetes, espacios de nombres, configuración). Las decisiones de este capítulo **no** las toman por vosotros.

## 7.6 Señales de alarma en un análisis

Si aparecen, hay que estimarlas aparte (y pronto):

- 🚩 **Lógica en el *markup*** (`<%# Eval(...) %>` con cálculos, `OnRowDataBound` con reglas).
- 🚩 **`Session` usada como base de datos** (listas enormes, objetos que viven entre pantallas).
- 🚩 **`ViewState` como mecanismo de negocio** (guardar el estado de un asistente de 5 pasos).
- 🚩 **Controles de terceros** sin equivalente en ASP.NET Core.
- 🚩 **Informes** Crystal Reports / RDLC.
- 🚩 **Dependencias COM**, ensamblados sin código fuente, acceso a disco con rutas fijas.
- 🚩 **`HttpContext.Current` por todas partes** (también en la capa de negocio).
- 🚩 **Ninguna prueba** y nadie que sepa explicar qué hace una pantalla.

## 7.7 El análisis de SIREI

En el [Lab 3](labs/lab-03-analisis-sirei.md) empezaréis el análisis de SIREI con una ficha que recoge todo lo anterior. El resultado (inventario, clasificación y propuesta de estrategia) es la entrada del caso práctico del día 5.

## Referencias

> Enlaces comprobados el 7 de octubre de 2026.

- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0) — Guía oficial con la guía de decisión: migración incremental frente a *in situ*.
- [Introducción a la migración incremental de ASP.NET a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/start?view=aspnetcore-10.0)
- [Adaptadores System.Web](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/inc/systemweb-adapters?view=aspnetcore-10.0)
- [Migración del estado de sesión](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/areas/session?view=aspnetcore-10.0) — Incluye la sesión remota compartida.
- [Migración de la autenticación](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/areas/authentication?view=aspnetcore-10.0) — Autenticación remota y cookie compartida.
- [Patrón de la higuera estranguladora](https://learn.microsoft.com/es-es/azure/architecture/patterns/strangler-fig)
- [Patrón de capa contra daños](https://learn.microsoft.com/es-es/azure/architecture/patterns/anti-corruption-layer) — La "capa anticorrupción".
- [Empieza con YARP](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/servers/yarp/getting-started?view=aspnetcore-10.0)
- [Tecnologías de .NET Framework no disponibles en .NET 6+](https://learn.microsoft.com/es-es/dotnet/core/porting/net-framework-tech-unavailable) — Web Forms, WCF servidor, etc.
- [Introducción al Asistente para actualización de .NET](https://learn.microsoft.com/es-es/dotnet/core/porting/upgrade-assistant-overview) — Marcado oficialmente como obsoleto.
- [Información general sobre la actualización de GitHub Copilot](https://learn.microsoft.com/es-es/dotnet/core/porting/github-copilot-upgrade/overview) — La herramienta que recomienda Microsoft en su lugar.
- [.NET Standard](https://learn.microsoft.com/es-es/dotnet/standard/net-standard) — Bibliotecas compartidas entre .NET Framework y .NET 10.
