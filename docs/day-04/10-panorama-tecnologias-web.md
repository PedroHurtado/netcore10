# 10. Tecnologías web anteriores a .NET Core y a qué migrarlas

**Material complementario** del día 4. Es una guía de consulta para el caso práctico del día 5 y para los proyectos reales: qué tecnologías web había antes de .NET Core (en .NET y fuera de .NET), qué ha pasado con cada una y **a qué se puede llevar** hoy.

**Punto de partida:** el **backend** va a estar en **.NET 10** (ASP.NET Core). El **frontend** no tiene por qué ser .NET: puede seguir renderizándose en el servidor (Razor, Blazor) o pasar a un framework de JavaScript (React, Angular, Vue...) que consume una API de ASP.NET Core. La decisión del frontend es **independiente** de la del backend y es la que más condiciona el coste de la migración.

## 10.1 Cómo leer este documento

| Si tienes... | Ve a |
|---|---|
| Una aplicación **Web Forms** (SIREI) | [10.4](#104-web-forms-a-qué-se-puede-llevar) |
| Una aplicación **MVC 5**, Web API 2, WCF, ASMX... (Noticom) | [10.5](#105-el-resto-del-servidor-net) |
| Mucho **JavaScript antiguo**, Silverlight, Flash, ActiveX... | [10.6](#106-tecnologías-de-cliente) |
| Una aplicación **PHP, Java, ASP clásico**... que pasa a .NET | [10.7](#107-backends-no-net-que-pasan-a-net) |
| Dudas sobre **informes, autenticación, estado, empaquetado**... | [10.8](#108-piezas-transversales) |
| Que decidir **qué frontend** usar | [10.9](#109-cómo-elegir-el-frontend) |
| Que planificar **cómo** migrar | [10.10](#1010-estrategia-de-migración) |

Leyenda de dificultad: 🟢 sobre todo mecánica · 🟡 hay que rediseñar partes · 🔴 reescritura (el legacy sirve de especificación).

## 10.2 El panorama: qué había antes de .NET Core

```
 2000                  2005                  2010                  2015           2016 → .NET Core
  │                     │                     │                     │                 │
  ├─ ASP clásico (VBScript, 1996)              │                     │                 │
  ├─ ASP.NET Web Forms (2002) ────────────────────────────────────────────────▶ solo .NET Framework
  │        ├─ ASMX (servicios SOAP, 2002)      │                     │                 │
  │        │        ├─ ASP.NET AJAX / UpdatePanel (2007)             │                 │
  │        │        ├─ WCF (2006) ─────────────────────────────────────────────▶ CoreWCF / gRPC / REST
  │        │        │        ├─ ASP.NET MVC (2009) ── MVC 5 (2013) ────────────▶ ASP.NET Core MVC
  │        │        │        │        ├─ Web API (2012) ── Web API 2 ──────────▶ ASP.NET Core (controladores / Minimal APIs)
  │        │        │        │        ├─ SignalR (2013) ───────────────────────▶ ASP.NET Core SignalR
  │        │        │        │        ├─ OWIN / Katana (2013) ─────────────────▶ middleware de ASP.NET Core
  │                                                                              │
 CLIENTE: ActiveX · Java applets · Flash · Silverlight (2007-2021) · jQuery (2006) · AngularJS (2010-2021) · Knockout · Backbone
 FUERA DE .NET: PHP · Java (JSP, JSF, Struts, Spring MVC) · ColdFusion · Perl/CGI
```

| Familia | Tecnologías | Modelo | Situación en 2026 |
|---|---|---|---|
| Servidor .NET "clásico" | **Web Forms**, ASMX, WCF, ASP.NET AJAX | Controles con estado (ViewState), *postback*, SOAP | Solo en **.NET Framework 4.8.x**: soportado mientras lo esté Windows, pero **sin evolución**. No existe en .NET moderno |
| Servidor .NET "MVC" | **MVC 5**, Web API 2, SignalR 2, OWIN | Peticiones HTTP, controladores, vistas Razor | Solo .NET Framework. Tienen **sucesor directo** en ASP.NET Core |
| Servidor no .NET | ASP clásico, PHP, JSP/JSF/Struts, ColdFusion | Varios (scripts en la página, MVC, componentes) | Siguen existiendo (PHP y Java, muy vivos); el ASP clásico, solo por compatibilidad en IIS |
| Plugins del navegador | **ActiveX**, **Java applets**, **Flash**, **Silverlight** | Código nativo o máquina virtual dentro del navegador | **Muertos**: Flash (fin 31/12/2020), Silverlight (fin 12/10/2021), ActiveX (solo Internet Explorer, retirado en 2022), applets (los navegadores dejaron de soportarlos hacia 2015-2017) |
| JavaScript "de primera generación" | **jQuery**, ASP.NET AJAX, AngularJS 1.x, Knockout, Backbone, ExtJS | Manipulación directa del DOM o MVVM temprano | jQuery sigue mantenido; **AngularJS sin soporte desde el 31/12/2021**; Knockout y Backbone, prácticamente parados |

## 10.3 Tabla resumen: de qué a qué

| Tecnología legacy | Destino recomendado | Alternativas | Dificultad |
|---|---|---|---|
| **ASP.NET Web Forms** | **Razor Pages** o **MVC** (render en servidor) | **Blazor**; **SPA** (React/Angular/Vue) + API de ASP.NET Core | 🟡 / 🔴 |
| ASP.NET MVC 5 | **ASP.NET Core MVC** | Razor Pages | 🟢 |
| ASP.NET Web API 2 | **Controladores API** de ASP.NET Core | Minimal APIs | 🟢 |
| WCF (servidor) | **REST** (ASP.NET Core) o **gRPC** | **CoreWCF** (mantener SOAP sin reescribir) | 🟡 |
| ASMX (servicios SOAP) | REST (ASP.NET Core) | CoreWCF si los clientes exigen SOAP | 🟡 |
| SignalR (ASP.NET) | **ASP.NET Core SignalR** | WebSockets, Server-Sent Events | 🟡 (protocolo incompatible: cambian también los clientes) |
| OWIN / Katana | **Middleware** de ASP.NET Core | — | 🟢 |
| `HttpModule` / `HttpHandler` (`.ashx`) | **Middleware** / **endpoints** (Minimal APIs) | — | 🟢 |
| ASP.NET AJAX / `UpdatePanel` | `fetch` + vista parcial o JSON; **htmx** | Blazor (interactividad en servidor) | 🟡 |
| **jQuery** | **JavaScript estándar** (ES2015+, `fetch`, `querySelector`) | Mantener jQuery (sigue vivo); htmx; Alpine.js | 🟢 / 🟡 |
| **AngularJS 1.x** | **Angular** actual | React, Vue | 🔴 (no hay migración automática: es otro framework) |
| Knockout, Backbone | React, Vue, Angular | Web Components; Blazor | 🔴 |
| **Silverlight** | **Blazor WebAssembly** (C# en el navegador) | SPA con TypeScript | 🔴 |
| **Flash** | HTML5 + JavaScript (Canvas, SVG, `<video>`, CSS) | — | 🔴 |
| **ActiveX** | APIs web estándar (ficheros, cámara, WebUSB...) o una **aplicación de escritorio** auxiliar | Extensión de navegador | 🔴 |
| **Java applets** | HTML5 + JavaScript | Blazor WebAssembly; aplicación de escritorio | 🔴 |
| ASP clásico (VBScript) | **Razor Pages** | MVC | 🔴 |
| PHP / JSP / JSF / Struts / ColdFusion → .NET | **Razor Pages** o **MVC** | SPA + API | 🔴 (cambia el lenguaje) |
| Crystal Reports / ReportViewer (RDLC) | Generación de **PDF/Excel** desde el backend, o **SSRS / Power BI** | Mantener en una aplicación .NET Framework aparte | 🟡 / 🔴 |
| `System.Web.Optimization` (bundling) | **MapStaticAssets** + minificación al compilar; **Vite** si hay SPA | WebOptimizer | 🟢 |
| Forms Authentication / Membership | **ASP.NET Core Identity** u **OIDC** (Entra ID) | — | 🟡 |

## 10.4 Web Forms: a qué se puede llevar

Web Forms **no existe** en .NET moderno y **no hay** conversión automática. Hay tres destinos razonables. Los tres comparten el mismo backend en ASP.NET Core (Domain, Application, Infrastructure del curso): lo que cambia es la capa de presentación.

### Opción A — Razor Pages o MVC (render en el servidor)

El servidor genera el HTML completo, como Web Forms, pero sin ViewState ni *postback*. Es lo que hacemos en el curso.

| ✅ A favor | ❌ En contra |
|---|---|
| El modelo mental más cercano: el servidor pinta páginas; Razor Pages ≈ "página + code-behind" | Interactividad rica (*grids* editables, arrastrar y soltar) requiere JavaScript adicional |
| Un solo proyecto, un solo lenguaje, un solo despliegue | Cada acción del usuario recarga la página (salvo que se añada `fetch` o htmx) |
| SEO, accesibilidad y rendimiento inicial excelentes | |
| Seguridad sencilla: cookie + antiforgery (capítulos 4 y 6) | |

**Cuándo:** aplicaciones de gestión con formularios, listados y fichas (la mayoría de las Web Forms de la Administración, como SIREI). **Es la opción por defecto** si el equipo es de .NET.

### Opción B — Blazor

Componentes `.razor` con C# y **eventos** (`@onclick`), muy parecidos a los controles de Web Forms. Desde .NET 8, una misma aplicación **Blazor Web App** puede mezclar modos de render por componente:

| Modo | Dónde se ejecuta | Para qué |
|---|---|---|
| **SSR estático** | Servidor, sin interactividad (como Razor Pages) | Páginas de consulta |
| **Interactive Server** | Servidor; los eventos viajan por una conexión SignalR | Intranets con buena red. Lo más parecido a Web Forms |
| **Interactive WebAssembly** | Navegador (.NET compilado a WebAssembly) | Trabajo sin conexión constante, mucha interacción; sucesor natural de **Silverlight** |
| **Auto** | Empieza en servidor y pasa a WebAssembly cuando se ha descargado | Lo mejor de ambos, a costa de más complejidad |

| ✅ A favor | ❌ En contra |
|---|---|
| Componentes con eventos: **la transición más natural** desde Web Forms (Microsoft tiene un libro dedicado) | Interactive Server mantiene un **circuito** (estado en memoria) por usuario: consume memoria y depende de la conexión |
| Todo en C#; se comparte código con el backend | WebAssembly: primera descarga más pesada |
| Ecosistema de componentes de terceros (Telerik, DevExpress, Syncfusion, MudBlazor...) | Menos desarrolladores y recursos que React/Angular |

**Cuándo:** pantallas muy interactivas en intranet, equipos 100 % .NET, o aplicaciones que dependían de controles con eventos de servidor (y de terceros, que tienen versión Blazor).

### Opción C — SPA (React, Angular o Vue) + API de ASP.NET Core

El frontend es una aplicación JavaScript/TypeScript independiente; el backend .NET solo expone una **API** (como `IncidenciasApiController`).

| ✅ A favor | ❌ En contra |
|---|---|
| Interfaces muy ricas; el mayor ecosistema y mercado de desarrolladores | **Dos aplicaciones**, dos lenguajes, dos procesos de compilación y despliegue |
| La API sirve también a móviles, otras aplicaciones, integraciones | Más decisiones de seguridad: tokens, CORS, o el patrón **BFF** (*Backend for Frontend*) con cookie |
| Equipos de frontend y backend independientes | Para formularios de gestión sencillos, es **más trabajo** que Razor Pages |

**Cuándo:** la organización ya tiene equipo y estándar de frontend (por ejemplo, Angular), la aplicación será de cara al público con mucha interacción, o la API tiene otros consumidores.

### Comparativa rápida

| Criterio | Razor Pages / MVC | Blazor | SPA + API |
|---|---|---|---|
| Parecido con Web Forms | Medio (páginas) | **Alto** (componentes + eventos) | Bajo |
| Curva para un equipo de Web Forms | Baja | Baja-media | Alta |
| Interactividad sin escribir JavaScript | Baja (sí con htmx) | **Alta** | — (todo es JavaScript/TypeScript) |
| Coste para pantallas de gestión típicas | **Bajo** | Medio | Alto |
| Proyectos y despliegues | 1 | 1 | 2 |
| Encaja con lo que se enseña en el curso | **Sí** | Mismo backend | Mismo backend (la API) |

> **Se pueden mezclar.** Una aplicación ASP.NET Core puede tener Razor Pages, MVC, componentes Blazor y una API a la vez. Es habitual migrar la mayoría de pantallas a Razor Pages y hacer en Blazor (o en un componente JavaScript) solo las dos o tres muy interactivas.

### Qué se lleva cada pieza de una página Web Forms

| Pieza Web Forms | Razor Pages / MVC | Blazor | SPA |
|---|---|---|---|
| `.aspx` (markup) | `.cshtml` | `.razor` | Componente (`.tsx`, `.vue`, plantilla Angular) |
| *Code-behind* (`.aspx.cs`) | `PageModel` / controlador | `@code { }` o clase parcial | Componente + servicio que llama a la API |
| `Button_Click` | Acción / *handler* POST | `@onclick="Guardar"` | Manejador de evento + `fetch` a la API |
| ViewState | No hay: ruta, query string, formulario | Estado del componente (en el circuito o en el navegador) | Estado en el navegador (*store*) |
| `UserControl` (`.ascx`) | Vista parcial / View Component | Componente | Componente |
| Master page | `_Layout.cshtml` | `MainLayout.razor` | *Layout* del *router* |
| *Validators* | DataAnnotations + Tag Helpers | DataAnnotations + `<EditForm>` | Validación en el cliente **y** en la API |
| `GridView` | `<table>` + `@foreach` | `QuickGrid` o componente de terceros | Componente de tabla |
| Lógica de negocio en eventos | **Domain / Application** (igual en las tres opciones) | | |

## 10.5 El resto del servidor .NET

| Legacy | Destino | Notas |
|---|---|---|
| **ASP.NET MVC 5** | ASP.NET Core MVC | Mismas ideas; cambian el arranque, la configuración y `System.Web` (capítulo 3). La más sencilla de migrar |
| **Web API 2** | Controladores con `[ApiController]` o **Minimal APIs** | En ASP.NET Core, MVC y Web API son **el mismo framework** |
| **WCF** (servidor) | **REST** si los clientes se pueden cambiar; **gRPC** para comunicación interna eficiente entre servicios .NET; **CoreWCF** (proyecto comunitario apoyado por Microsoft) para mantener SOAP y los contratos sin reescribir | El **cliente** WCF sí existe en .NET moderno (`System.ServiceModel.*`): consumir servicios SOAP de terceros no es un problema |
| **ASMX** | REST en ASP.NET Core | Si los consumidores exigen SOAP, CoreWCF |
| **SignalR** (ASP.NET) | ASP.NET Core SignalR | Protocolo **no compatible**: hay que cambiar también los clientes JavaScript |
| **OWIN / Katana** | Middleware | El concepto de *pipeline* es el mismo (día 1) |
| `HttpModule`, `HttpHandler` (`.ashx`), `Global.asax` | Middleware, endpoints, `Program.cs` | Ver la guía oficial de migración de módulos y handlers |
| **ASP.NET AJAX**, `ScriptManager`, `UpdatePanel` | `fetch` + vistas parciales; **htmx**; Blazor | El "refresco parcial" se reimplementa; no hay equivalente directo |
| **Entity Framework 6** | **EF Core** | EF 6.3+ funciona en .NET moderno: se puede dejar para después |
| **ADO.NET** con `DataSet` | EF Core, o ADO.NET/Dapper detrás de una interfaz | Día 3, capítulo 5 |
| **Windows Workflow (WF)** | Lógica en código, motores de flujo de terceros | No existe en .NET moderno 🔴 |
| **.NET Remoting**, **AppDomains** | gRPC, procesos separados | No existen en .NET moderno |

## 10.6 Tecnologías de cliente

### Plugins del navegador: hay que sustituirlos sí o sí

| Legacy | Para qué se usaba | Sustituto moderno |
|---|---|---|
| **Silverlight** | Aplicaciones "de escritorio" en el navegador, con C# | **Blazor WebAssembly** (C#, mismo tipo de aplicación); o SPA con TypeScript |
| **Flash** | Animaciones, gráficos, vídeo, juegos | HTML5: `<canvas>`, SVG, CSS, `<video>`, bibliotecas de gráficos (Chart.js, D3, ECharts) |
| **ActiveX** | Acceso al equipo: escáner, firma electrónica, impresoras, ficheros locales | APIs web (File System Access, Web Serial/USB, `getUserMedia`); o una **aplicación auxiliar local** que expone un servicio (el modelo de AutoFirma) |
| **Java applets** | Firma, cálculos, interfaces ricas | HTML5 + JavaScript; aplicación auxiliar local; Blazor WebAssembly |

> En la Administración, ActiveX y applets se usaban sobre todo para **firma electrónica** y **acceso a periféricos**. Su sustituto no es una tecnología web, sino una **aplicación local** que la web invoca. Conviene identificarlo pronto en el análisis: no es un problema del equipo de desarrollo web en exclusiva.

### JavaScript de primera generación

| Legacy | ¿Hay que migrarlo? | A qué |
|---|---|---|
| **jQuery** | **No necesariamente**: sigue mantenido y funciona. Se sustituye cuando se reescribe la pantalla | JavaScript estándar (`fetch`, `querySelector`, `classList`...); **htmx** para "refrescos parciales" declarativos; Alpine.js para pequeñas interacciones |
| **jQuery UI**, plugins varios | Si dependen de versiones antiguas de jQuery, sí | Elementos nativos (`<dialog>`, `<input type="date">`), Web Components, o componentes del framework elegido |
| **AngularJS 1.x** | **Sí**: sin soporte desde 2021 (riesgo de seguridad) | Angular actual (es **otro** framework: reescritura), React o Vue |
| **Knockout**, **Backbone**, **ExtJS antiguo** | Sí, a medio plazo | React, Vue, Angular, o vuelta a render en servidor si la interacción no lo justifica |
| JavaScript *inline* (`onclick="..."`, `<script>` en la página) | Sí, al migrar | Ficheros `.js` en `wwwroot` (la **CSP** del curso lo exige) |
| `UpdatePanel` / `__doPostBack` | Sí (no existen fuera de Web Forms) | `fetch` + vista parcial, htmx, o Blazor |
| Bootstrap 3 / 4 | Opcional | Bootstrap 5 (sin jQuery) u otro sistema de diseño de la organización |

### ¿Y TypeScript, Vite, npm...?

Si se elige una SPA, entran en juego herramientas que no existían en el mundo Web Forms: **TypeScript** (JavaScript con tipos; casi obligatorio en proyectos medianos), **Vite** (compilación y servidor de desarrollo), **npm** (paquetes) y herramientas de pruebas de frontend. Es un coste de aprendizaje real que hay que contar al elegir.

## 10.7 Backends no .NET que pasan a .NET

Cuando la organización decide unificar en .NET una aplicación escrita en otra tecnología, la migración es siempre una **reescritura** (cambia el lenguaje), pero los conceptos tienen equivalente:

| Legacy | Se parece a... en ASP.NET Core | Destino recomendado |
|---|---|---|
| **ASP clásico** (VBScript, `<% %>`) | Razor Pages (HTML con código incrustado) | Razor Pages |
| **PHP** "clásico" (una página = un script) | Razor Pages | Razor Pages |
| **PHP** con framework (Laravel, Symfony) | MVC (controladores, rutas, *middleware*, ORM ≈ EF Core) | MVC |
| **JSP** + *servlets* | Vistas Razor + controladores / middleware | MVC |
| **JSF** (componentes con estado, como Web Forms) | Blazor (componentes) o Razor Pages | Razor Pages o Blazor (mismas consideraciones que Web Forms) |
| **Struts / Spring MVC** | ASP.NET Core MVC (casi uno a uno: controladores, vistas, filtros/interceptores, inyección de dependencias) | MVC |
| **Hibernate / JPA** | EF Core | EF Core |
| **ColdFusion** | Razor Pages | Razor Pages |

> **¿Merece la pena pasar a .NET una aplicación PHP o Java que funciona?** Solo si hay una razón organizativa clara (unificar equipos, plataforma, soporte). Si no, lo razonable puede ser **integrarla** con las aplicaciones .NET mediante APIs, no reescribirla.

## 10.8 Piezas transversales

| Área | Legacy | Moderno (backend .NET) |
|---|---|---|
| **Autenticación** | Forms Authentication, Membership, Windows, login casero con `Session` | ASP.NET Core **Identity**, **OIDC** con Entra ID u otro proveedor, Negotiate (Windows). Con SPA: cookie con BFF o tokens (capítulos 4 y 5) |
| **Informes** | Crystal Reports, ReportViewer (RDLC) — atados a .NET Framework/Web Forms | Generar **PDF/Excel** desde el backend con una biblioteca, **SSRS** como servicio aparte, **Power BI**; o mantener temporalmente una pequeña aplicación .NET Framework solo para informes 🚩 |
| **Estado** | ViewState, Session para todo, `Application`, `Cache` | Ruta y query string, TempData, Session acotada, HybridCache, base de datos (capítulo 2) |
| **Empaquetado de CSS/JS** | `System.Web.Optimization` (`BundleConfig`) | `MapStaticAssets` + minificación al compilar (día 2); **Vite** si hay SPA |
| **Tiempo real** | Sondeo con `setInterval`, SignalR 2 | ASP.NET Core **SignalR**, Server-Sent Events |
| **Servicios** | WCF, ASMX | REST, gRPC, CoreWCF (10.5) |
| **Configuración** | `Web.config`, `ConfigurationManager` | `appsettings.json`, variables de entorno, opciones (día 1) |
| **Tareas programadas** | Temporizadores en `Global.asax`, tareas de Windows que llaman a una página | `BackgroundService` / `IHostedService`, o un servicio/worker aparte |
| **Hosting** | IIS en Windows | IIS (sigue siendo válido), Kestrel detrás de un proxy, **contenedores** (Linux o Windows) |
| **Controles de terceros** | Telerik, DevExpress, Infragistics, AjaxControlToolkit para Web Forms | Versiones del mismo fabricante para ASP.NET Core / Blazor (licencia aparte), o componentes del framework de frontend elegido. AjaxControlToolkit no tiene sucesor 🚩 |

## 10.9 Cómo elegir el frontend

```
¿La aplicación es sobre todo formularios, listados y fichas?
├── Sí ─▶ ¿Hay 2-3 pantallas MUY interactivas (grids editables, arrastrar y soltar...)?
│         ├── No ─▶ RAZOR PAGES / MVC
│         └── Sí ─▶ RAZOR PAGES / MVC para la mayoría
│                    + Blazor (o un componente JS) para esas pantallas
└── No (interfaz muy rica, "tipo aplicación de escritorio")
          ├── ¿El equipo es .NET y no hay estándar de frontend en la organización?
          │     └── Sí ─▶ BLAZOR (Server en intranet, WebAssembly/Auto si hay que reducir carga del servidor)
          └── ¿Hay equipo/estándar de frontend, o la API tendrá otros consumidores?
                └── Sí ─▶ SPA (el framework que marque la organización) + API ASP.NET Core
```

| Criterio | Pesa a favor de |
|---|---|
| Equipo con experiencia solo en .NET | Razor Pages / MVC, Blazor |
| Estándar corporativo de frontend (por ejemplo, Angular) | SPA |
| Usuarios internos en red corporativa | Razor Pages, Blazor Server |
| Usuarios externos, muchos y con mala conexión | Razor Pages (ligero) o SPA/Blazor WebAssembly con buen diseño de carga |
| La API tendrá otros consumidores (móvil, otras apps) | SPA + API (aunque una API se puede añadir a cualquier opción) |
| Plazo corto y presupuesto ajustado | Razor Pages / MVC |
| Accesibilidad y SEO | Render en servidor (Razor Pages, MVC, Blazor SSR) |

> **Regla práctica:** no elijas la tecnología de frontend más ambiciosa "por si acaso". El coste de una SPA para una aplicación de gestión con 30 formularios es mucho mayor que el de Razor Pages, y el usuario final no lo nota.

## 10.10 Estrategia de migración

Las estrategias del día 3 (capítulo 7) se aplican igual, elija el frontend que elija:

| Estrategia | Cómo | Cuándo |
|---|---|---|
| **Completa** ("big bang") | Se reescribe todo y se sustituye de una vez | Aplicaciones pequeñas que se pueden congelar |
| **Incremental** (*Strangler Fig*) | Aplicación ASP.NET Core con **YARP** delante: las rutas migradas las atiende la nueva; el resto se reenvía al legacy. Sesión y autenticación compartidas con **System.Web adapters** | Aplicaciones medianas y grandes, críticas, o con piezas que no se pueden migrar todavía. **La recomendada por Microsoft** para la mayoría |

Particularidades según el destino:

- **Razor Pages / MVC / Blazor**: la convivencia con Web Forms por rutas (YARP) es directa: cada pantalla migrada es una ruta nueva.
- **SPA**: lo habitual es **primero la API** (el backend en ASP.NET Core, usado ya por el legacy o por nuevas pantallas) y **después** el frontend, pantalla a pantalla o módulo a módulo. Durante la transición pueden convivir páginas antiguas y la SPA bajo el mismo dominio (otra vez YARP).
- **En todos los casos**: la lógica de negocio va primero a **Domain/Application**, que no dependen del frontend. Si mañana se cambia Razor Pages por Angular, esa parte no se toca. Esa es la mejor protección contra elegir mal el frontend.

## Referencias

> Enlaces comprobados el 8 de octubre de 2026.

- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0) — Guía oficial: estrategias, áreas técnicas, herramientas.
- [Actualizar de ASP.NET MVC, Web API y Web Forms a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/tooling?view=aspnetcore-10.0)
- [Blazor para desarrolladores de ASP.NET Web Forms](https://learn.microsoft.com/es-es/dotnet/architecture/blazor-for-web-forms-developers/) — Libro gratuito de Microsoft.
- [Modos de representación de Blazor](https://learn.microsoft.com/es-es/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0)
- [Elegir una interfaz de usuario web de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/tutorials/choose-web-ui?view=aspnetcore-10.0) — Razor Pages, MVC, Blazor y SPA comparados por Microsoft.
- [Tecnologías de .NET Framework no disponibles en .NET](https://learn.microsoft.com/es-es/dotnet/core/porting/net-framework-tech-unavailable) — Web Forms, WCF servidor, WF, Remoting, AppDomains...
- [CoreWCF](https://github.com/CoreWCF/CoreWCF) — WCF para .NET moderno.
- [gRPC en .NET](https://learn.microsoft.com/es-es/aspnet/core/grpc/?view=aspnetcore-10.0)
- [ASP.NET Core SignalR](https://learn.microsoft.com/es-es/aspnet/core/signalr/introduction?view=aspnetcore-10.0)
- [Patrón de la higuera estranguladora](https://learn.microsoft.com/es-es/azure/architecture/patterns/strangler-fig) y [YARP](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/servers/yarp/getting-started?view=aspnetcore-10.0)
- [htmx](https://htmx.org/) — Interactividad declarativa sobre HTML renderizado en servidor.
