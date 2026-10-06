# Lab 1 — Recorrido guiado por la arquitectura en capas

**Duración:** 20 min · **Teoría relacionada:** [02 — Clean Architecture](../02-clean-architecture.md)

## Objetivo

No vamos a escribir funcionalidad nueva. Vamos a **entender** la solución: quién referencia a quién, por dónde pasa una petición y qué impide el compilador. Al terminar, deberías poder explicar a un compañero por qué hay cuatro proyectos.

> Si te atascas en cualquier paso, levanta la mano: este laboratorio es para **mirar**, no para pelearse con errores.

## Paso 0 — Preparar tu copia (2 min)

Copia la carpeta `src/day-02` a tu carpeta de trabajo (así puedes romper cosas sin miedo) y ejecútala:

```bash
cd mi-copia-day-02
dotnet run --project GestorIncidencias.Web
```

Abre http://localhost:5196 y navega por: **Incidencias (MVC)**, una ficha de detalle, **Nueva (MVC)**, **Razor Pages** y **API JSON**.

✅ **Comprueba:** ves 4 incidencias de ejemplo y el pie dice `Entorno: Development`.

Para la aplicación con `Ctrl+C`.

## Paso 1 — Dibuja el mapa de referencias (4 min)

Abre los cuatro ficheros `.csproj` y completa la tabla (busca `<ProjectReference>` y `<PackageReference>`):

| Proyecto | ¿A qué proyectos referencia? | ¿Qué paquetes NuGet usa? |
|---|---|---|
| `GestorIncidencias.Domain` | | |
| `GestorIncidencias.Application` | | |
| `GestorIncidencias.Infrastructure` | | |
| `GestorIncidencias.Web` | | |

<details>
<summary>Solución</summary>

| Proyecto | Proyectos | Paquetes |
|---|---|---|
| Domain | ninguno | ninguno |
| Application | Domain | `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options` |
| Infrastructure | Application | `Microsoft.EntityFrameworkCore.InMemory`, `Microsoft.Extensions.Hosting.Abstractions` |
| Web | Application, Infrastructure | `Microsoft.AspNetCore.OpenApi` |

</details>

✅ **Comprueba:** ¿hay alguna flecha que vaya de dentro hacia fuera (por ejemplo, de Domain a Application)? No debería.

## Paso 2 — Sigue una petición con el depurador (8 min)

Vamos a seguir "Resolver la incidencia 1" de principio a fin.

1. Pon un **punto de interrupción** (F9 en Visual Studio / clic en el margen en VS Code) en estas cuatro líneas:

   | Fichero | Línea que contiene... |
   |---|---|
   | `GestorIncidencias.Web/Controllers/IncidenciasController.cs` | `TrasCambioDeEstado(id, await servicio.ResolverAsync(id, ct)` |
   | `GestorIncidencias.Application/Incidencias/IncidenciaService.cs` | `var incidencia = await repositorio.ObtenerPorIdAsync(id, ct);` |
   | `GestorIncidencias.Domain/Incidencias/Incidencia.cs` | `if (!EstaAbierta)` (dentro de `Resolver`) |
   | `GestorIncidencias.Infrastructure/Persistencia/EfIncidenciaRepository.cs` | `public Task GuardarCambiosAsync` |

2. Arranca **con depuración** (F5).
3. Ve a http://localhost:5196/Incidencias/Detalle/1 y pulsa **Resolver**.
4. Cada vez que se pare, mira la ventana de **Pila de llamadas** (*Call Stack*) y continúa con F5.

Rellena el orden en que se detiene:

| Orden | Proyecto (capa) | Clase |
|---|---|---|
| 1 | | |
| 2 | | |
| 3 | | |
| 4 | | |

<details>
<summary>Solución</summary>

1. Web — `IncidenciasController`
2. Application — `IncidenciaService`
3. Domain — `Incidencia`
4. Infrastructure — `EfIncidenciaRepository`

Fíjate: en **ejecución** Application llama a Infrastructure, pero en **compilación** Application no conoce Infrastructure. Lo conecta el contenedor de DI (`AddInfrastructure()` en `Program.cs`).

</details>

✅ **Comprueba:** al terminar, la ficha muestra el aviso verde "Incidencia resuelta." y el estado `Resuelta`.

Quita los puntos de interrupción y para la aplicación.

## Paso 3 — Intenta romper las reglas (6 min)

Ahora vamos a comprobar que **el compilador protege la arquitectura**. Haz cada cambio, compila con `dotnet build` y **deshazlo** antes del siguiente.

### 3a. El dominio no puede usar EF Core

En la primera línea de `GestorIncidencias.Domain/Incidencias/Incidencia.cs` añade:

```csharp
using Microsoft.EntityFrameworkCore;
```

Resultado esperado:

```
error CS0234: The type or namespace name 'EntityFrameworkCore' does not exist in the namespace 'Microsoft'
```

**¿Por qué?** Domain no tiene el paquete de EF Core. No es un error que haya que "arreglar" añadiendo el paquete: es la arquitectura funcionando.

### 3b. Application no puede usar Infrastructure

En la primera línea de `GestorIncidencias.Application/Incidencias/IncidenciaService.cs` añade:

```csharp
using GestorIncidencias.Infrastructure;
```

Resultado esperado:

```
error CS0234: The type or namespace name 'Infrastructure' does not exist in the namespace 'GestorIncidencias'
```

**¿Por qué?** Application no referencia a Infrastructure (la flecha va al revés).

### 3c. Nadie puede saltarse las reglas de la entidad

En `IncidenciaService.cs`, dentro de `CambiarEstadoAsync`, justo antes de `var resultado = cambio(incidencia);`, añade:

```csharp
incidencia.Estado = EstadoIncidencia.Cerrada;
```

Resultado esperado:

```
error CS0200: Property or indexer 'Incidencia.Estado' cannot be assigned to -- it is read only
```

**¿Por qué?** El setter es privado. La única forma de cambiar el estado es con `Iniciar()`, `Resolver()` o `Cerrar()`, que comprueban las reglas.

✅ **Comprueba:** después de deshacer los tres cambios, `dotnet build` vuelve a compilar sin errores.

## Preguntas para comentar en grupo

1. En el proyecto del día 1, ¿habría dado error alguno de los tres cambios del paso 3?
2. Si mañana hay que guardar en SQL Server en lugar de en memoria, ¿en qué proyecto se cambia el código?
3. ¿Qué parte de este proyecto podríais reutilizar tal cual en una aplicación de consola que cierre incidencias antiguas cada noche?

## Referencias

> Enlaces comprobados el 6 de octubre de 2026.

- [Depuración en Visual Studio: puntos de interrupción](https://learn.microsoft.com/es-es/visualstudio/debugger/using-breakpoints) — Cómo usar puntos de interrupción y la pila de llamadas.
- [Depuración de C# en Visual Studio Code](https://code.visualstudio.com/docs/csharp/debugging) (inglés)
- [Errores de referencias de ensamblado (CS0234 y otros)](https://learn.microsoft.com/es-es/dotnet/csharp/language-reference/compiler-messages/assembly-references)
