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
| 3.0 / 3.5 | 2006–2007 | WPF, WCF, WF · LINQ |
| 4.0 | 2010 | TPL (`Task`), **ASP.NET MVC 2/3** |
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
- **Alto rendimiento**: Kestrel está entre los servidores web más rápidos de los benchmarks TechEmpower.

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
