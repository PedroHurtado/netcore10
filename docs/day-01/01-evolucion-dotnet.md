# 1. Evolución de .NET: de .NET Framework a .NET Core y .NET 10

## 1.1 Tres etapas

```
2002 ─────────────── 2016 ─────────────── 2020 ─────────────── 2025 ──▶
│  .NET Framework     │  .NET Core          │  .NET 5+ ("One .NET")│ .NET 10 (LTS)
│  Solo Windows       │  Multiplataforma    │  Unificación         │
│  ASP.NET Web Forms  │  ASP.NET Core 1.0   │  .NET 6 / 8 LTS      │
│  ASP.NET MVC 1–5    │  Open source        │  Minimal APIs        │
│  Web API, WCF       │                     │  Blazor              │
```

### .NET Framework (2002 – 2019)

| Versión | Año | Hito |
|---|---|---|
| 1.0 | 2002 | CLR, C#, **ASP.NET Web Forms** |
| 2.0 | 2005 | Genéricos, Master Pages |
| 3.0 / 3.5 | 2006–2007 | WPF, WCF, WF · LINQ · sobre 3.5 SP1 se publica **ASP.NET MVC 1.0** (2009) |
| 4.0 | 2010 | TPL (`Task`) · base de **ASP.NET MVC 3** (2011) |
| 4.5 | 2012 | `async`/`await`, **ASP.NET Web API** |
| 4.8 / 4.8.1 | 2019 / 2022 | **Última versión.** Solo correcciones de seguridad |

Características:

- **Solo Windows** e instalado *en el sistema operativo* (una única versión por máquina, actualizada con Windows Update).
- Las aplicaciones web dependen de **IIS** y de `System.Web.dll`, un ensamblado monolítico.
- Sigue soportado mientras lo esté la versión de Windows en la que corre, **pero no evoluciona**: no recibirá nuevas versiones de C#, ni mejoras de rendimiento, ni nuevas APIs.

### .NET Core (2016 – 2019)

Reescritura desde cero con nuevos objetivos:

- **Multiplataforma**: Windows, Linux, macOS (y contenedores).
- **Open source** (github.com/dotnet) y desarrollado en público.
- **Modular**: todo se distribuye como paquetes NuGet; solo cargas lo que usas.
- **Instalación lado a lado**: varias versiones conviven en la misma máquina; incluso se puede publicar la aplicación con su propio runtime (*self-contained*).
- **Alto rendimiento**: ASP.NET Core y Kestrel obtienen resultados muy competitivos en los benchmarks independientes de TechEmpower.

| Versión | Año | Hito |
|---|---|---|
| 1.0 | 2016 | ASP.NET Core 1.0, Kestrel |
| 2.0 / 2.1 | 2017–2018 | .NET Standard 2.0, Razor Pages, SignalR |
| 3.0 / 3.1 LTS | 2019 | Endpoint routing, gRPC, Blazor Server, WinForms/WPF (solo Windows) |

### .NET 5 en adelante: "One .NET"

En 2020 desaparece el apellido "Core" y la plataforma pasa a llamarse simplemente **.NET**. Se salta el número 4 para no confundirlo con .NET Framework 4.x.

| Versión | Año | Tipo | Novedades relevantes para web |
|---|---|---|---|
| .NET 5 | 2020 | STS | Unificación, C# 9 (records) |
| .NET 6 | 2021 | LTS | **Minimal hosting** (`Program.cs` sin `Startup`), **Minimal APIs**, Hot Reload |
| .NET 7 | 2022 | STS | Rate limiting, output caching, `MapGroup` |
| .NET 8 | 2023 | LTS | Blazor unificado, Native AOT para APIs, Identity API endpoints, `TimeProvider` |
| .NET 9 | 2024 | STS | OpenAPI integrado (`AddOpenApi`), `MapStaticAssets`, HybridCache |
| **.NET 10** | **nov. 2025** | **LTS** | C# 14, validación integrada en Minimal APIs, OpenAPI 3.1, *passkeys* en Identity, ficheros `.slnx`, aplicaciones de un solo fichero (`dotnet run app.cs`) |

## 1.2 Política de soporte

- **LTS** (*Long Term Support*): versiones pares, publicadas en noviembre, **3 años** de soporte.
- **STS** (*Standard Term Support*): versiones impares, **2 años** de soporte (antes 18 meses).
- Cada noviembre sale una versión nueva; las actualizaciones de servicio son mensuales ("Patch Tuesday").

> **Recomendación para migraciones:** apuntar siempre a la **LTS** vigente. Hoy es **.NET 10**, soportada hasta noviembre de 2028.

## 1.3 Piezas de la plataforma

```
┌───────────────────────────────────────────────────────────┐
│  Tu aplicación (C#)                                       │
├───────────────────────────────────────────────────────────┤
│  ASP.NET Core  │  EF Core  │  Bibliotecas Microsoft.Ext.* │  ← paquetes / frameworks
├───────────────────────────────────────────────────────────┤
│  BCL (Base Class Library): System.*, colecciones, IO...   │
├───────────────────────────────────────────────────────────┤
│  Runtime (CoreCLR): JIT, GC, tipos                        │
├───────────────────────────────────────────────────────────┤
│  Windows  │  Linux  │  macOS  │  Contenedores             │
└───────────────────────────────────────────────────────────┘
```

- **SDK**: compilador, CLI `dotnet`, plantillas. Se instala en la máquina de desarrollo.
- **Runtime**: lo necesario para ejecutar. En servidores se instala solo el runtime (o el *ASP.NET Core Hosting Bundle* en IIS).
- **Target Framework Moniker (TFM)**: identifica la plataforma destino en el `.csproj`:
  - `net48` → .NET Framework 4.8
  - `netstandard2.0` → biblioteca compatible con Framework y .NET moderno
  - `net10.0` → .NET 10

> **.NET Standard 2.0** es el puente clásico durante una migración: una biblioteca de lógica de negocio compilada para `netstandard2.0` puede usarse a la vez desde la aplicación Web Forms (`net48`) y desde la nueva aplicación ASP.NET Core (`net10.0`). Lo usaremos en el Módulo 3.

## 1.4 ¿Por qué migrar?

| Motivo | Detalle |
|---|---|
| **Fin de la evolución** | .NET Framework no recibe nuevas funcionalidades. Las bibliotecas de terceros abandonan progresivamente `net4x`. |
| **Rendimiento** | Mejoras importantes en cada versión (GC, JIT, `Span<T>`, Kestrel). Menos servidores para la misma carga. |
| **Despliegue** | Linux, contenedores, Kubernetes, nube. Sin dependencia de IIS ni de Windows Server. |
| **Productividad** | C# moderno, Hot Reload, DI, configuración y logging integrados, OpenAPI. |
| **Seguridad** | Identity moderno, OIDC, protección de datos, cabeceras HTTPS por defecto. |
| **Talento** | Los nuevos desarrolladores aprenden .NET moderno, no Web Forms. |

**Pero:** migrar tiene coste. **Web Forms no existe en ASP.NET Core** (ni lo existirá), así que la interfaz de usuario hay que **reescribirla**. Por eso el Módulo 3 dedica tiempo a decidir **qué migrar, qué refactorizar y qué dejar conviviendo**.

## 1.5 Herramientas de la CLI

```bash
dotnet --info                 # versión del SDK, runtimes instalados, SO
dotnet --list-sdks
dotnet new list               # plantillas disponibles
dotnet new web -n MiApp       # crear proyecto
dotnet build                  # compilar
dotnet run                    # compilar y ejecutar
dotnet watch                  # ejecutar con recarga en caliente
dotnet add package <Paquete>  # añadir paquete NuGet
dotnet test                   # ejecutar pruebas
dotnet publish -c Release     # generar artefactos de despliegue
```

## Preguntas de repaso

1. ¿Qué diferencia hay entre una versión LTS y una STS? ¿Cuál elegirías para migrar una aplicación corporativa?
2. ¿Por qué no se puede "recompilar" una aplicación Web Forms para .NET 10?
3. ¿Para qué sirve `netstandard2.0` durante una migración?

## Referencias

> Enlaces comprobados el 4 de octubre de 2026. Las fechas de soporte proceden de la directiva oficial y pueden actualizarse.

**Documentación oficial**

- [Introducción a .NET](https://learn.microsoft.com/es-es/dotnet/core/introduction) — Qué es .NET, componentes (runtime, SDK, bibliotecas) y escenarios.
- [Directiva de soporte técnico oficial de .NET y .NET Core](https://dotnet.microsoft.com/es-es/platform/support/policy/dotnet-core) — Diferencia LTS/STS (3 y 2 años) y tabla de versiones: .NET 10 publicado el 11/11/2025, soporte hasta el 14/11/2028.
- [Preguntas frecuentes sobre el ciclo de vida de .NET Framework](https://learn.microsoft.com/es-es/lifecycle/faq/dotnet-framework) — .NET Framework 4.5.2 y posteriores es un componente de Windows y sigue el ciclo de vida del sistema operativo.
- [Versiones de .NET Framework y dependencias](https://learn.microsoft.com/es-es/dotnet/framework/install/versions-and-dependencies) — Lista completa de versiones de .NET Framework (1.0 a 4.8.1) y versión de CLR de cada una.
- [Novedades de .NET 10](https://learn.microsoft.com/es-es/dotnet/core/whats-new/dotnet-10/overview) y [Novedades de ASP.NET Core en .NET 10](https://learn.microsoft.com/es-es/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0) — Validación integrada en Minimal APIs, OpenAPI 3.1, *passkeys* en Identity.
- [Novedades del SDK y las herramientas para .NET 10](https://learn.microsoft.com/es-es/dotnet/core/whats-new/dotnet-10/sdk) — Aplicaciones basadas en archivos (`dotnet run app.cs`) y otras mejoras de la CLI.
- [Novedades de C# 14](https://learn.microsoft.com/es-es/dotnet/csharp/whats-new/csharp-14)
- [.NET Standard](https://learn.microsoft.com/es-es/dotnet/standard/net-standard) — Qué es y cuándo usar `netstandard2.0` para compartir bibliotecas entre .NET Framework y .NET.
- [Marcos de destino (TFM) en proyectos de estilo SDK](https://learn.microsoft.com/es-es/dotnet/standard/frameworks) — `net48`, `netstandard2.0`, `net10.0`...
- [CLI de .NET](https://learn.microsoft.com/es-es/dotnet/core/tools/) — Referencia de los comandos `dotnet`.
- [Introducción a la actualización de aplicaciones .NET](https://learn.microsoft.com/es-es/dotnet/core/porting/) — Punto de partida oficial para migrar de .NET Framework a .NET.

**Notas de versión de ASP.NET Core (hitos citados en las tablas)**

- [ASP.NET Core 2.0](https://learn.microsoft.com/es-es/aspnet/core/release-notes/aspnetcore-2.0?view=aspnetcore-2.0) (Razor Pages) · [2.1](https://learn.microsoft.com/es-es/aspnet/core/release-notes/aspnetcore-2.1?view=aspnetcore-2.1) (SignalR) · [3.0](https://learn.microsoft.com/es-es/aspnet/core/release-notes/aspnetcore-3.0?view=aspnetcore-3.0) (gRPC, Blazor Server, enrutamiento de endpoints)
- [Migración a .NET 6](https://learn.microsoft.com/es-es/aspnet/core/migration/50-to-60?view=aspnetcore-10.0) (modelo de hosting mínimo) · [ASP.NET Core 7.0](https://learn.microsoft.com/es-es/aspnet/core/release-notes/aspnetcore-7.0?view=aspnetcore-10.0) (rate limiting, output caching, `MapGroup`) · [8.0](https://learn.microsoft.com/es-es/aspnet/core/release-notes/aspnetcore-8.0?view=aspnetcore-10.0) (Native AOT, `MapIdentityApi`) · [9.0](https://learn.microsoft.com/es-es/aspnet/core/release-notes/aspnetcore-9.0?view=aspnetcore-10.0) (`MapStaticAssets`, OpenAPI integrado, HybridCache)

**Blogs oficiales del equipo de .NET**

- [Introducing .NET 5](https://devblogs.microsoft.com/dotnet/introducing-net-5/) (inglés) — Unificación de la plataforma y motivo para saltarse el número 4.
- [.NET STS releases supported for 24 months](https://devblogs.microsoft.com/dotnet/dotnet-sts-releases-supported-for-24-months/) (inglés, 16/09/2025) — Las versiones STS pasan de 18 a 24 meses de soporte a partir de .NET 9.
- [Announcing .NET 10](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/) (inglés) — Anuncio oficial de la versión LTS.

**Otras fuentes**

- [dotnet/core — releases.md](https://github.com/dotnet/core/blob/main/releases.md) — Histórico oficial de versiones y fechas de .NET Core / .NET.
- [.NET Framework version history](https://en.wikipedia.org/wiki/.NET_Framework_version_history) y [ASP.NET MVC](https://en.wikipedia.org/wiki/ASP.NET_MVC) (Wikipedia, inglés) — Fechas de publicación de .NET Framework y de ASP.NET MVC, con enlaces a las fuentes primarias.
- [TechEmpower Framework Benchmarks](https://www.techempower.com/benchmarks/) — Comparativa independiente de rendimiento de frameworks web.
