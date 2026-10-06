# Lab 2 — "Reabrir incidencia" de punta a punta

**Duración:** 35 min · **Teoría relacionada:** [02 — Clean Architecture](../02-clean-architecture.md), [04 — Controladores MVC](../04-controladores-mvc.md), [05 — Vistas Razor](../05-vistas-razor.md)

## Objetivo

Añadir una funcionalidad nueva atravesando **todas las capas**, en el orden correcto: de dentro (dominio) hacia fuera (vista).

> **Regla de negocio:** una incidencia **resuelta** se puede **reabrir** (el usuario dice que el problema ha vuelto). Pasa a estado `Abierta` y se borra la fecha de resolución. Una incidencia **cerrada** ya no se puede reabrir.

```
Abierta ──Iniciar──▶ EnCurso ──Resolver──▶ Resuelta ──Cerrar──▶ Cerrada
   ▲                                          │
   └──────────────── Reabrir (NUEVO) ─────────┘
```

Todo el código está en este documento: **cópialo**, pero lee los comentarios para entender qué hace cada pieza. Trabaja sobre tu copia de `src/day-02` del [Lab 1](lab-01-recorrido-capas.md).

## Paso 1 — Domain: la regla (5 min)

Abre `GestorIncidencias.Domain/Incidencias/Incidencia.cs` y añade este método **debajo de `Cerrar()`**:

```csharp
    /// <summary>Resuelta → Abierta. Se borra la fecha de resolución.</summary>
    public Resultado Reabrir()
    {
        if (Estado != EstadoIncidencia.Resuelta)
            return Resultado.Fallo($"Solo se puede reabrir una incidencia resuelta (estado actual: {Estado}).", TipoError.Conflicto);

        Estado = EstadoIncidencia.Abierta;
        FechaResolucion = null;
        return Resultado.Ok();
    }
```

✅ **Comprueba:** `dotnet build GestorIncidencias.Domain` compila sin errores.

**Piensa:** ¿por qué este método puede escribir `Estado = ...` y el servicio no?

## Paso 2 — Application: el caso de uso (7 min)

### 2a. La interfaz

En `GestorIncidencias.Application/Incidencias/IIncidenciaService.cs` añade al final de la interfaz:

```csharp
    Task<Resultado<IncidenciaDto>> ReabrirAsync(int id, CancellationToken ct = default);
```

Compila: `dotnet build GestorIncidencias.Application`.

❌ **Error esperado:**

```
error CS0535: 'IncidenciaService' does not implement interface member 'IIncidenciaService.ReabrirAsync(int, CancellationToken)'
```

Es normal: hemos prometido algo en la interfaz que la clase todavía no hace.

### 2b. La implementación

En `GestorIncidencias.Application/Incidencias/IncidenciaService.cs` añade **debajo de `CerrarAsync`**:

```csharp
    public Task<Resultado<IncidenciaDto>> ReabrirAsync(int id, CancellationToken ct = default) =>
        CambiarEstadoAsync(id, incidencia => incidencia.Reabrir(), ct);
```

Una sola línea: el patrón *cargar → pedir a la entidad → guardar* ya está en `CambiarEstadoAsync`.

✅ **Comprueba:** `dotnet build GestorIncidencias.Application` compila sin errores.

**Piensa:** ¿has tenido que tocar algo en `GestorIncidencias.Infrastructure`? ¿Por qué no?

## Paso 3 — Web: la acción del controlador (5 min)

En `GestorIncidencias.Web/Controllers/IncidenciasController.cs` añade **debajo de la acción `Cerrar`**:

```csharp
    // POST /Incidencias/Reabrir/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reabrir(int id, CancellationToken ct) =>
        TrasCambioDeEstado(id, await servicio.ReabrirAsync(id, ct), "Incidencia reabierta.");
```

✅ **Comprueba:** `dotnet build` (toda la solución) compila sin errores.

## Paso 4 — Web: el botón en la vista (5 min)

En `GestorIncidencias.Web/Views/Incidencias/Detalle.cshtml`, busca el bloque del botón **Cerrar**:

```cshtml
    @if (Model.Estado == EstadoIncidencia.Resuelta)
    {
        <form asp-action="Cerrar" asp-route-id="@Model.Id" method="post">
            <button type="submit" class="boton-secundario">Cerrar</button>
        </form>
    }
```

y añade un segundo formulario **dentro del mismo `@if`**, debajo del de Cerrar:

```cshtml
    @if (Model.Estado == EstadoIncidencia.Resuelta)
    {
        <form asp-action="Cerrar" asp-route-id="@Model.Id" method="post">
            <button type="submit" class="boton-secundario">Cerrar</button>
        </form>
        <form asp-action="Reabrir" asp-route-id="@Model.Id" method="post">
            <button type="submit" class="boton-secundario">Reabrir</button>
        </form>
    }
```

## Paso 5 — Probar (8 min)

Ejecuta la aplicación (`dotnet run --project GestorIncidencias.Web`) y prueba estos casos:

| # | Acción | Resultado esperado |
|---|---|---|
| 1 | Abre la incidencia **4** ("No funciona la VPN", está `Resuelta`) | Aparecen los botones **Cerrar** y **Reabrir** |
| 2 | Pulsa **Reabrir** | Aviso verde "Incidencia reabierta.", estado `Abierta`, fecha de resolución "—" |
| 3 | Pulsa **Resolver** y después **Cerrar** | Estado `Cerrada`; ya no hay botones |
| 4 | Abre una incidencia `Abierta` (por ejemplo, la 1) | No aparece el botón **Reabrir** |

> **Para pensar:** ¿qué pasaría si alguien envía a mano un `POST /Incidencias/Reabrir/4` con la incidencia ya **cerrada**? La vista no muestra el botón, pero el dominio devuelve `Resultado.Fallo(...)`, y el controlador mostraría el aviso rojo "Solo se puede reabrir una incidencia resuelta (estado actual: Cerrada)". **La vista oculta; el dominio protege.**

## Errores típicos

| Síntoma | Causa probable | Solución |
|---|---|---|
| `CS0535 ... does not implement interface member` | Falta el paso 2b | Añadir `ReabrirAsync` en `IncidenciaService` |
| `CS1061 'Incidencia' does not contain a definition for 'Reabrir'` | Falta el paso 1, o el método está fuera de la clase | Revisar llaves `{ }` en `Incidencia.cs` |
| `CS0103 The name 'TipoError' does not exist` | Falta un `using` | Debe estar `using GestorIncidencias.Domain.Comun;` al principio del fichero |
| El botón no aparece | El `<form>` está fuera del `@if` correcto, o la incidencia no está `Resuelta` | Revisar el paso 4 |
| Al pulsar el botón: **404** | La acción no se llama `Reabrir` o falta `[HttpPost]` | Revisar el paso 3 |
| Al pulsar el botón: **400 Bad Request** | El formulario no tiene `method="post"` (sin token antiforgery) | Revisar el paso 4 |
| Los cambios no se ven | La aplicación sigue con el código anterior | Parar (`Ctrl+C`) y volver a ejecutar `dotnet run` |

## Ampliaciones opcionales (si terminas antes)

### A. Reabrir desde la API

En `GestorIncidencias.Web/Controllers/Api/IncidenciasApiController.cs`, debajo de `Cerrar`:

```csharp
    // POST /api/incidencias/5/reabrir
    [HttpPost("{id:int}/reabrir")]
    public async Task<ActionResult<IncidenciaDto>> Reabrir(int id, CancellationToken ct) =>
        Responder(await servicio.ReabrirAsync(id, ct));
```

Pruébalo añadiendo al fichero `GestorIncidencias.Web.http`:

```http
### Reabrir (Resuelta → Abierta)
POST {{host}}/api/incidencias/4/reabrir

### Reabrir otra vez → 409 Conflict
POST {{host}}/api/incidencias/4/reabrir
```

### B. Reabrir desde Razor Pages

En `Pages/Paginas/Incidencias/Index.cshtml.cs` añade un *handler* (copia `OnPostResolverAsync` y cambia lo necesario) y, en `Index.cshtml`, un botón con `asp-page-handler="Reabrir"` para las incidencias `Resuelta`.

**Observa:** las ampliaciones A y B no necesitan **ningún cambio** en Domain ni en Application. Esa es la recompensa de separar en capas.

## Resumen: qué has tocado

| Capa | Fichero | Cambio |
|---|---|---|
| Domain | `Incidencia.cs` | Método `Reabrir()` con la regla |
| Application | `IIncidenciaService.cs` | Nueva operación en el contrato |
| Application | `IncidenciaService.cs` | Una línea que reutiliza `CambiarEstadoAsync` |
| Infrastructure | — | **Nada** |
| Web | `IncidenciasController.cs` | Acción POST |
| Web | `Detalle.cshtml` | Botón |

Compara esta tabla con la del [capítulo 3, apartado 3.5](../03-hexagonal-clean-vertical-slice.md#35-el-mismo-cambio-en-las-tres-añadir-reabrir-incidencia): en Vertical Slice, casi todo esto estaría en un único fichero `ReabrirIncidencia.cs`.

## Referencias

> Enlaces comprobados el 6 de octubre de 2026.

- [Control de solicitudes con controladores en ASP.NET Core MVC](https://learn.microsoft.com/es-es/aspnet/core/mvc/controllers/actions?view=aspnetcore-10.0)
- [Tag Helpers en formularios](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/working-with-forms?view=aspnetcore-10.0)
- [Prevención de ataques CSRF](https://learn.microsoft.com/es-es/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [Error del compilador CS0535](https://learn.microsoft.com/es-es/dotnet/csharp/language-reference/compiler-messages/interface-implementation-errors) — Errores al implementar interfaces.
