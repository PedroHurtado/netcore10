# 4. Configuración base de un proyecto ASP.NET Core

## 4.1 Crear el proyecto

```bash
dotnet new sln -n GestorIncidencias                     # crea GestorIncidencias.slnx
dotnet new web -n GestorIncidencias.Web                  # plantilla vacía
dotnet sln add GestorIncidencias.Web
```

Plantillas web más usadas:

| Plantilla | Comando | Contenido |
|---|---|---|
| Vacía | `dotnet new web` | Solo `Program.cs` con un endpoint |
| API | `dotnet new webapi` | Minimal API + OpenAPI (`--use-controllers` para controladores) |
| MVC | `dotnet new mvc` | Controladores, vistas, layout, Bootstrap |
| Razor Pages | `dotnet new webapp` | Páginas Razor, layout, Bootstrap |
| Blazor | `dotnet new blazor` | Blazor Web App |

> Usamos la plantilla **vacía** para entender qué añade cada pieza en lugar de partir de código "mágico".

> **.slnx** es el nuevo formato de solución en XML, por defecto desde .NET 10. Es mucho más legible que el `.sln` clásico y lo soportan Visual Studio, VS Code y Rider.

## 4.2 Ficheros del proyecto

```
GestorIncidencias.Web/
├── GestorIncidencias.Web.csproj
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── Properties/launchSettings.json
└── wwwroot/          (lo creamos nosotros)
```

### El `.csproj` (estilo SDK)

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.*" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.*" />
  </ItemGroup>
</Project>
```

Comparado con un `.csproj` de .NET Framework:

- **No lista los ficheros `.cs`**: se incluyen todos los de la carpeta automáticamente.
- `Sdk="Microsoft.NET.Sdk.Web"` trae todo ASP.NET Core (el *shared framework* `Microsoft.AspNetCore.App`), sin paquetes NuGet adicionales.
- Paquetes con `<PackageReference>` (adiós a `packages.config`).
- `Nullable`: el compilador avisa de posibles `NullReferenceException`.
- `ImplicitUsings`: `using System;`, `using System.Linq;`, `using Microsoft.AspNetCore.Builder;`... ya incluidos.

### `Program.cs`: el punto de entrada

Desde .NET 6 se usa el **modelo de hosting mínimo**: sin clase `Startup`, sin `Main` explícito (*top-level statements*).

```csharp
var builder = WebApplication.CreateBuilder(args);   // 1. Configurar

// builder.Configuration → fuentes de configuración
// builder.Services      → registro de servicios (DI)
// builder.Logging       → proveedores de log
// builder.Environment   → entorno actual

var app = builder.Build();                           // 2. Construir

// app.UseXxx()   → middleware (pipeline)
// app.MapXxx()   → endpoints

app.Run();                                           // 3. Ejecutar
```

`CreateBuilder` ya deja configurado por defecto:

- **Kestrel** como servidor web.
- **Configuración**: `appsettings.json`, `appsettings.{Entorno}.json`, *User Secrets* (en Development), variables de entorno, argumentos de línea de comandos.
- **Logging**: consola, depuración, EventSource (y EventLog en Windows).
- **DI**: contenedor con los servicios del framework.
- Integración con **IIS** si se aloja ahí.

> En proyectos antiguos de ASP.NET Core (2.x–5) verás `Program.cs` + `Startup.cs` con `ConfigureServices` y `Configure`. Es exactamente lo mismo: `ConfigureServices` ≡ `builder.Services...`; `Configure` ≡ `app.Use...`.

### `Properties/launchSettings.json`

Solo para **desarrollo local** (no se publica). Define perfiles de arranque:

```json
"http": {
  "commandName": "Project",
  "applicationUrl": "http://localhost:5196",
  "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
}
```

```bash
dotnet run --launch-profile https
```

### `wwwroot/`

Raíz de **ficheros estáticos** (HTML, CSS, JS, imágenes). Solo se sirve lo que está aquí y solo si se añade el middleware correspondiente. El código fuente nunca es accesible desde la web (a diferencia de IIS clásico, donde había que proteger carpetas con `web.config`).

## 4.3 El host y Kestrel

```
Internet ──▶ [Proxy inverso: IIS / Nginx / YARP] ──▶ Kestrel ──▶ Pipeline de middleware ──▶ Tu código
                    (opcional)                    (proceso .NET)
```

- **Kestrel** es el servidor HTTP multiplataforma incluido. Puede exponerse directamente o detrás de un proxy.
- En **IIS**, el *ASP.NET Core Module* (ANCM) aloja la aplicación *in-process* (dentro de `w3wp.exe`) u *out-of-process* (como proxy hacia Kestrel).
- El `web.config` en ASP.NET Core **solo** configura ANCM para IIS; ya no contiene configuración de la aplicación.

## 4.4 Ciclo de desarrollo

```bash
dotnet build                       # compila
dotnet run                         # compila y ejecuta
dotnet watch                       # ejecuta y recarga al guardar (Hot Reload)
dotnet run --environment Production
```

Ficheros `.http`: peticiones HTTP ejecutables desde Visual Studio o VS Code (extensión REST Client). En el proyecto: `GestorIncidencias.Web.http`.

## 4.5 Organización del código en el proyecto del día 1

Por ahora todo vive en un único proyecto, organizado por carpetas. El día 2 lo separaremos en capas (Clean Architecture).

| Carpeta | Contenido |
|---|---|
| `Models/` | Entidad `Incidencia`, enums y DTOs (`CrearIncidenciaRequest`, `IncidenciaResponse`) |
| `Data/` | `IncidenciasDbContext` (EF Core InMemory) |
| `Services/` | Repositorio, servicio de negocio, carga de datos inicial |
| `Endpoints/` | Definición de rutas con Minimal APIs |
| `Middleware/` | Middleware propio |
| `Options/` | Clases de configuración tipada |

## Preguntas de repaso

1. ¿Dónde se configura el entorno con el que arranca la aplicación en desarrollo?
2. ¿Qué diferencia hay entre `builder.Services` y `app.Use...`?
3. ¿Se publica `launchSettings.json` al desplegar? ¿Cómo se fija el entorno en un servidor?

## Referencias

> Enlaces comprobados el 4 de octubre de 2026.

**Documentación oficial**

- [Información general de los conceptos básicos de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/?view=aspnetcore-10.0) — Mapa de todos los fundamentos (host, DI, middleware, configuración...).
- [WebApplication y WebApplicationBuilder](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/minimal-apis/webapplication?view=aspnetcore-10.0) — Qué configura `CreateBuilder` por defecto y qué middleware añade automáticamente.
- [Host genérico de .NET en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/host/generic-host?view=aspnetcore-10.0)
- [Migración de .NET 5 a .NET 6](https://learn.microsoft.com/es-es/aspnet/core/migration/50-to-60?view=aspnetcore-10.0) — Paso de `Startup.cs` al modelo de hosting mínimo.
- [Información general sobre el SDK de proyectos de .NET](https://learn.microsoft.com/es-es/dotnet/core/project-sdk/overview) — `Microsoft.NET.Sdk.Web` y [directivas `using` implícitas](https://learn.microsoft.com/es-es/dotnet/core/project-sdk/overview#implicit-using-directives).
- [Tipos de referencia anulables](https://learn.microsoft.com/es-es/dotnet/csharp/fundamentals/null-safety/nullable-reference-types) — Qué activa `<Nullable>enable</Nullable>`.
- [Plantillas predeterminadas de `dotnet new`](https://learn.microsoft.com/es-es/dotnet/core/tools/dotnet-new-sdk-templates) — Indica que a partir de .NET 10 el formato de solución predeterminado es `.slnx`.
- [Servidor web Kestrel](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-10.0) e [Implementaciones de servidores web](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/servers/?view=aspnetcore-10.0)
- [Hospedaje de ASP.NET Core en Windows con IIS](https://learn.microsoft.com/es-es/aspnet/core/host-and-deploy/iis/?view=aspnetcore-10.0) y [Módulo ASP.NET Core (ANCM)](https://learn.microsoft.com/es-es/aspnet/core/host-and-deploy/aspnet-core-module?view=aspnetcore-10.0) — Hospedaje en proceso (predeterminado) y fuera de proceso.
- [Entornos y `launchSettings.json`](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/environments?view=aspnetcore-10.0)
- [Archivos estáticos](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0)
- [Recarga activa (Hot Reload)](https://learn.microsoft.com/es-es/aspnet/core/test/hot-reload?view=aspnetcore-10.0) y [`dotnet watch`](https://learn.microsoft.com/es-es/dotnet/core/tools/dotnet-watch)
- [Uso de archivos .http](https://learn.microsoft.com/es-es/aspnet/core/test/http-files?view=aspnetcore-10.0)
- [Introducción a la publicación de aplicaciones .NET](https://learn.microsoft.com/es-es/dotnet/core/deploying/) — Despliegue dependiente del framework frente a autocontenido.

**Blog oficial**

- [Introducing support for SLNX](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/) (inglés) — El nuevo formato de solución `.slnx`.
