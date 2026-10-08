# Lab 2 — Noticom: de ASP.NET MVC 5 a ASP.NET Core MVC

**Duración:** 60 min (en parejas) · **Teoría relacionada:** [03 — Caso Noticom](../03-caso-noticom.md), [día 4, cap. 3](../../day-04/03-migracion-mvc5-herramientas.md) · **Legacy:** [legacy/noticom](../legacy/noticom/)

## Objetivo

Migrar la pantalla de **lotes** de Noticom, que en MVC 5 son cuatro pantallas en una (`Lote/Index?t=c|v|cr|b`) con mucho JavaScript, a un área `Noticom`:

1. Sustituir el string mágico `TipoEjecucion` por un **enum** y un **ViewModel** que decide título y columnas.
2. Crear el **controlador** con una acción para la página y otra para el **fragmento AJAX** (misma búsqueda, misma vista parcial).
3. Crear las **vistas** sin JavaScript inline y conectar un `.js` **sin Razor**.
4. Proteger las operaciones con **políticas y roles** (lo que el legacy dejaba a la pantalla).

Trabaja sobre tu copia del [Lab 1](lab-01-sirei-expedientes.md). La solución completa está en `src/day-05`.

## Paso 0 — Leer el legacy (6 min)

Abre [Index.cshtml](../legacy/noticom/Index.cshtml), [_GridLotes.cshtml](../legacy/noticom/_GridLotes.cshtml) y [LoteController.cs](../legacy/noticom/LoteController.cs). Responde con tu compañero:

1. ¿En cuántos sitios distintos se compara `TipoEjecucion` con `"v"`?
2. ¿Qué pasa con la pantalla de validación si alguien añade una columna "Prioridad" en la posición 3 de `_GridLotes`?
3. ¿Cómo sabe el JavaScript que la sesión ha caducado? ¿Qué pasaría si el texto `_Logon_` apareciera en la descripción de un lote?
4. Un usuario sin permiso para borrar, ¿puede borrar un lote validado? ¿Cómo?

<details>
<summary>Respuestas</summary>

1. En **tres capas**: la vista (`<h1>`), el JavaScript (`ajaxStop` y el `data` del AJAX) y el servicio (`if (tipoEjecucion == "v" || ...)`).
2. Las columnas se ocultan **por número** (`ocultarColumnaGridview(11, ...)`): se ocultarían las equivocadas en las cuatro pantallas, y `crearRemesa()` (que lee el Id de `td:nth-child(2)`) seguiría funcionando solo por casualidad.
3. Busca el texto `"_Logon_"` en una respuesta **200**. Si ese texto apareciera en los datos, el usuario sería expulsado al login.
4. Sí: `POST /Lote/Borrar` con `idLote=12` (por ejemplo desde F12 → Consola). La acción no tiene autorización, ni antiforgery, y `LoteService.Borrar` no comprueba el estado.

</details>

## Paso 1 — Copiar la base (6 min)

Copia de `src/day-05` a tu copia, **en las mismas rutas**:

| Origen (`src/day-05/...`) | Qué es |
|---|---|
| `GestorIncidencias.Domain/Lotes/` (carpeta) | `Lote`, `Remesa`, `EstadoLote`, `TipoNotificacion` |
| `GestorIncidencias.Application/Abstracciones/ILoteRepository.cs` e `ILoteConsultas.cs` | Puertos |
| `GestorIncidencias.Application/Lotes/` (carpeta) | DTOs, `ILoteService`, `LoteService`, `LoteLog` |
| `GestorIncidencias.Infrastructure/Noticom/` (carpeta) | `NoticomDbContext`, configuraciones, repositorio, consultas, datos de demostración |
| `GestorIncidencias.Web/wwwroot/js/noticom/lotes.js` | El JavaScript (lo leeremos en el paso 6) |

Lee `Remesa.Crear` y `LoteService.CrearRemesaAsync`: ¿qué comprobaba el legacy antes de remesar un lote? ¿Y ahora?

Registra los servicios:

- En `GestorIncidencias.Application/DependencyInjection.cs` (con `using GestorIncidencias.Application.Lotes;`):

```csharp
        services.AddScoped<ILoteService, LoteService>();   // Día 5: Noticom
```

- En `GestorIncidencias.Infrastructure/DependencyInjection.cs` (con `using GestorIncidencias.Infrastructure.Noticom;`), junto a lo de SIREI:

```csharp
        // Día 5 — Noticom: su propia base de datos (el EDMX de EF6, ahora en API fluida)
        services.AddDbContext<NoticomDbContext>(opt => opt.UseInMemoryDatabase("Noticom"));
        services.AddScoped<ILoteRepository, EfLoteRepository>();
        services.AddScoped<ILoteConsultas, EfLoteConsultas>();
        services.AddHostedService<InicializadorNoticom>();
        services.AddHealthChecks().AddDbContextCheck<NoticomDbContext>("noticom");
```

- Al final de `wwwroot/css/site.css`, copia el bloque `/* Día 5 ... */` de la solución (clases `.lote-*`, `.pestanas`, `.filtro-lotes`, `.corto`, `.cargando`).

✅ **Comprueba:** `dotnet build` compila; al arrancar, `Datos de demostración de Noticom cargados (56 lotes, 3 remesas)`.

## Paso 2 — Quién puede hacer qué (4 min)

El legacy no comprobaba nada. La regla nueva:

| Operación | Quién |
|---|---|
| Consultar | Cualquier usuario autenticado (`FallbackPolicy`) |
| Validar lotes y crear remesas | `Tecnico` o `Administrador` → política `GestionarLotes` |
| Borrar lotes | Solo `Administrador` |

En `GestorIncidencias.Web/Seguridad/Politicas.cs` (si no lo tienes del Lab 2 de ayer, créalo con la clase `Politicas`), añade:

```csharp
    /// <summary>Día 5 (Noticom): validar lotes y crear remesas.</summary>
    public const string GestionarLotes = "GestionarLotes";
```

Y en `Program.cs`, encadenada a `AddAuthorizationBuilder()` (necesita `using GestorIncidencias.Application.Seguridad;` y `using GestorIncidencias.Web.Seguridad;`):

```csharp
    .AddPolicy(Politicas.GestionarLotes, politica =>
        politica.RequireRole(Roles.Tecnico, Roles.Administrador))
```

¿Por qué otra política si la regla es la misma que `GestionarIncidencias`? Porque son **decisiones distintas**: si mañana valida lotes otro departamento, se cambia una sola línea sin tocar las incidencias.

## Paso 3 — `TipoEjecucion` → `ModoLotes` + ViewModel (10 min)

Crea `GestorIncidencias.Web/Areas/Noticom/Models/LotesViewModels.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Lotes;
using GestorIncidencias.Domain.Lotes;

namespace GestorIncidencias.Web.Areas.Noticom.Models;

/// <summary>Antes: TipoEjecucion = "c" | "v" | "cr" | "b". Un enum: si se escribe mal, no compila.</summary>
public enum ModoLotes
{
    Consulta,       // "c"
    Validacion,     // "v"
    CrearRemesa,    // "cr"
    Borrado         // "b"
}

/// <summary>Todo lo que la vista necesita, ya decidido en el servidor: título, columnas y acciones.</summary>
public record LotesViewModel(
    ModoLotes Modo,
    FiltroLotes Filtro,
    Pagina<LoteDto> Pagina,
    bool PuedeGestionar,
    bool PuedeBorrar,
    string UrlVolver)
{
    public string Titulo => Modo switch
    {
        ModoLotes.Validacion => "Lotes pendientes de validación",
        ModoLotes.CrearRemesa => "Crear remesas",
        ModoLotes.Borrado => "Borrar lotes",
        _ => "Consulta de lotes"
    };

    // Antes: ocultarColumnaGridview(N, 'gridLotes') por número. Ahora cada pantalla pinta SUS columnas.
    public bool ColumnaSeleccion => Modo == ModoLotes.CrearRemesa && PuedeGestionar;
    public bool ColumnaEstado => Modo == ModoLotes.Consulta;
    public bool ColumnaRemesa => Modo == ModoLotes.Consulta;
    public bool ColumnaValidacion => Modo is ModoLotes.Consulta or ModoLotes.CrearRemesa;
    public bool ColumnaValidar => Modo == ModoLotes.Validacion && PuedeGestionar;
    public bool ColumnaBorrar => Modo == ModoLotes.Borrado && PuedeBorrar;

    public Dictionary<string, string?> RutaPagina(int pagina) => Rutas.De(Modo, Filtro, pagina);
}

/// <summary>Formulario "Crear remesa": nombre + casillas name="LoteIds" (antes: un JSON montado recorriendo la tabla).</summary>
public class CrearRemesaFormulario
{
    [Display(Name = "Nombre remesa")]
    [Required(ErrorMessage = "El nombre de la remesa es obligatorio.")]
    [StringLength(Remesa.NombreLongitudMaxima, ErrorMessage = "El nombre no puede superar {1} caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public List<int> LoteIds { get; set; } = [];

    public CrearRemesaComando AComando() => new(Nombre, LoteIds);
}

/// <summary>Valores de ruta de una búsqueda (paginador y vuelta tras un POST). Antes: sessionStorage["FiltroLotes"].</summary>
public static class Rutas
{
    public static Dictionary<string, string?> De(ModoLotes modo, FiltroLotes filtro, int pagina)
    {
        var valores = new Dictionary<string, string?> { ["modo"] = modo.ToString() };
        if (filtro.Proceso is { } proceso) valores["proceso"] = proceso.ToString();
        if (filtro.Ejercicio is { } ejercicio) valores["ejercicio"] = ejercicio.ToString();
        if (filtro.Estado is { } estado && modo == ModoLotes.Consulta) valores["estado"] = estado.ToString();
        if (filtro.Tipo is { } tipo) valores["tipo"] = tipo.ToString();
        if (!string.IsNullOrWhiteSpace(filtro.Remesa)) valores["remesa"] = filtro.Remesa;
        if (pagina > 1) valores["pagina"] = pagina.ToString();
        return valores;
    }
}
```

**Piensa:** ¿por qué `ModoLotes` está en la capa **Web** y no en Application?

<details>
<summary>Respuesta</summary>

Porque es un concepto de **pantalla** ("qué ve el usuario"), no de negocio. El caso de uso solo sabe filtrar lotes (`FiltroLotes`); qué filtro corresponde a cada pantalla lo decide el controlador. En el legacy, `LoteService.Consultar` recibía `tipoEjecucion`: el servicio dependía de la interfaz.

</details>

## Paso 4 — El controlador (10 min)

Crea `GestorIncidencias.Web/Areas/Noticom/Controllers/LotesController.cs`:

```csharp
using GestorIncidencias.Application.Lotes;
using GestorIncidencias.Application.Seguridad;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Lotes;
using GestorIncidencias.Web.Areas.Noticom.Models;
using GestorIncidencias.Web.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorIncidencias.Web.Areas.Noticom.Controllers;

[Area("Noticom")]
public class LotesController(ILoteService servicio, IAuthorizationService autorizacion) : Controller
{
    // GET /Noticom/Lotes?modo=Validacion&proceso=120&ejercicio=2026&pagina=2   ← antes: Lote/Index?t=v
    // FiltroLotes se rellena desde la query string (proceso, ejercicio, estado, tipo, remesa).
    public async Task<IActionResult> Index(ModoLotes modo, FiltroLotes filtro, int pagina = 1, CancellationToken ct = default) =>
        View(await CrearModeloAsync(modo, filtro, pagina, ct));

    // GET /Noticom/Lotes/Tabla?...   ← antes: [HttpPost] ConsultaLotes. Leer = GET. Misma búsqueda, solo la tabla.
    public async Task<IActionResult> Tabla(ModoLotes modo, FiltroLotes filtro, int pagina = 1, CancellationToken ct = default) =>
        PartialView("_TablaLotes", await CrearModeloAsync(modo, filtro, pagina, ct));

    // POST /Noticom/Lotes/Validar/12
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.GestionarLotes)]
    public async Task<IActionResult> Validar(int id, string? volver, CancellationToken ct) =>
        Volver(await servicio.ValidarAsync(id, ct), $"Lote {id} validado.", volver);

    private async Task<LotesViewModel> CrearModeloAsync(ModoLotes modo, FiltroLotes filtro, int pagina, CancellationToken ct)
    {
        // El MODO fija el estado (antes lo hacía el servicio según tipoEjecucion).
        var filtroEfectivo = modo switch
        {
            ModoLotes.Validacion or ModoLotes.Borrado => filtro with { Estado = EstadoLote.PendienteValidacion },
            ModoLotes.CrearRemesa => filtro with { Estado = EstadoLote.Validado },
            _ => filtro
        };

        var resultado = await servicio.ListarAsync(filtroEfectivo, pagina, ct: ct);
        var puedeGestionar = (await autorizacion.AuthorizeAsync(User, Politicas.GestionarLotes)).Succeeded;
        var urlVolver = Url.Action(nameof(Index), Rutas.De(modo, filtro, resultado.NumeroPagina))!;

        return new LotesViewModel(modo, filtroEfectivo, resultado, puedeGestionar, User.IsInRole(Roles.Administrador), urlVolver);
    }

    /// <summary>PRG volviendo a la MISMA búsqueda (campo oculto "volver"). Solo URLs locales: nada de open redirect.</summary>
    private IActionResult Volver(Resultado resultado, string mensajeExito, string? volver)
    {
        if (resultado.Exito)
            TempData["Mensaje"] = mensajeExito;
        else
            TempData["Error"] = resultado.Error;

        return Url.IsLocalUrl(volver) ? LocalRedirect(volver) : RedirectToAction(nameof(Index));
    }
}
```

Y la ruta del área en `Program.cs`, junto a la de SIREI:

```csharp
app.MapAreaControllerRoute(
        name: "noticom",
        areaName: "Noticom",
        pattern: "Noticom/{controller=Lotes}/{action=Index}/{id?}")
    .CacheOutput(CacheHtmlPolicy.Nombre);
```

✅ **Comprueba:** `dotnet build` compila.

## Paso 5 — Las vistas (12 min)

### 5a. Lo propio del área

Crea `Areas/Noticom/Views/_ViewImports.cshtml`:

```cshtml
@using GestorIncidencias.Web
@using GestorIncidencias.Web.Areas.Noticom.Models
@using GestorIncidencias.Application.Comun
@using GestorIncidencias.Application.Lotes
@using GestorIncidencias.Domain.Lotes
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

y `Areas/Noticom/Views/_ViewStart.cshtml` con `@{ Layout = "_Layout"; }`.

### 5b. La tabla (vista parcial)

Copia de la solución `Areas/Noticom/Views/Lotes/_TablaLotes.cshtml` y **léela** comparándola con [_GridLotes.cshtml](../legacy/noticom/_GridLotes.cshtml). Localiza:

- Cada `@if (Model.ColumnaXxx)`: lo que antes era `ocultarColumnaGridview`.
- El formulario **Validar** de cada fila: POST con antiforgery (automático) y el campo oculto `volver`.
- Los enlaces del paginador con `asp-all-route-data` y el atributo `data-pagina` (lo usa el JavaScript).
- Las casillas `name="LoteIds" ... form="form-remesa"` y el formulario de la remesa (los usarás en la ampliación).

### 5c. La página

Crea `Areas/Noticom/Views/Lotes/Index.cshtml`:

```cshtml
@model LotesViewModel
@{
    ViewData["Title"] = $"{Model.Titulo} · Noticom";
    var filtro = Model.Filtro;
}

<h1>@Model.Titulo <small>· Noticom migrado</small></h1>

<nav class="pestanas" aria-label="Pantallas de lotes">
    @foreach (var modo in Enum.GetValues<ModoLotes>())
    {
        <a asp-action="Index" asp-route-modo="@modo" class="@(modo == Model.Modo ? "activa" : null)">@modo</a>
    }
</nav>

@* Antes: _BusquedaLotes + _BusquedaAvanzadaLotes y JavaScript para mostrar una u otra.
   Ahora: UN formulario GET; lo avanzado en un <details> (sin JavaScript). *@
<form asp-action="Index" method="get" id="filtro-lotes" class="filtro-lotes">
    <input type="hidden" name="modo" value="@Model.Modo" />

    <div class="filtro">
        <label for="proceso">Proceso:</label>
        <input id="proceso" name="proceso" type="number" min="1" value="@filtro.Proceso" class="corto" />
        <label for="ejercicio">Ejercicio:</label>
        <input id="ejercicio" name="ejercicio" type="number" min="2000" max="2100" value="@filtro.Ejercicio" class="corto" />
        <button type="submit" class="boton-secundario">Buscar</button>
    </div>

    <details open="@filtro.EsAvanzado">
        <summary>Búsqueda avanzada</summary>
        <div class="filtro">
            <label for="tipo">Tipo:</label>
            <select id="tipo" name="tipo">
                <option value="">(todos)</option>
                @foreach (var tipo in Enum.GetValues<TipoNotificacion>())
                {
                    <option value="@tipo" selected="@(filtro.Tipo == tipo)">@tipo</option>
                }
            </select>
        </div>
    </details>
</form>

@* Datos para el JavaScript en data-*: el .js no contiene Razor (antes: '@Model.TipoEjecucion' dentro del <script>) *@
<div id="lotes" aria-live="polite"
     data-url-tabla="@Url.Action("Tabla")"
     data-url-index="@Url.Action("Index")">
    <partial name="_TablaLotes" model="Model" />
</div>
```

(La solución añade además los filtros de estado y remesa en el modo Consulta, y la pestaña **Remesas**.)

Añade al menú del layout: `<a asp-area="Noticom" asp-controller="Lotes" asp-action="Index">Noticom</a>`.

✅ **Comprueba**, todavía **sin** JavaScript:

| # | Usuario | Acción | Resultado esperado |
|---|---|---|---|
| 1 | luis | `/Noticom` | "Consulta de lotes", 56 lotes en 3 páginas, columnas Estado y Remesa |
| 2 | luis | Pestaña **Validacion** | Solo pendientes; **sin** columna de botones (Luis no tiene rol) |
| 3 | ana | Pestaña **Validacion**, ejercicio `2026`, **Validar** un lote | "Lote N validado." y vuelves a la **misma** búsqueda (mira la URL) |
| 4 | ana | Pestaña **CrearRemesa** | Solo validados, con casillas, sin columna de estado |
| 5 | ana | Página 2 y Validar | Vuelves a la página 2 (el campo `volver`) |

## Paso 6 — El JavaScript, sin Razor (6 min)

Al final de `Index.cshtml`:

```cshtml
@section Scripts {
    <script src="~/js/noticom/lotes.js" asp-append-version="true"></script>
}
```

Lee `wwwroot/js/noticom/lotes.js` (lo copiaste en el paso 1) y localiza: de dónde saca las URLs, qué cabecera envía, qué hace con un 401 y qué hace si algo falla.

✅ **Comprueba:**

1. Busca un ejercicio y pasa de página: la tabla cambia **sin recargar** la página (F12 → Red: peticiones `Tabla?...` de tipo *fetch*) y la URL del navegador se actualiza.
2. En F12 → Red, abre una petición `Tabla`: lleva la cabecera `X-Requested-With: XMLHttpRequest`.
3. F12 → Consola: ningún error de CSP.
4. **Sesión caducada:** deja la página de lotes abierta, cierra sesión en **otra pestaña** y vuelve a buscar en la primera. Te lleva al login y, al entrar, vuelves a la página de lotes.

**Experimento:** comenta la línea `headers: { "X-Requested-With": "XMLHttpRequest" }` y repite la prueba 4 (recarga con `Ctrl+F5`). ¿Qué aparece dentro de la tabla? ¿Por qué? Deshaz el cambio.

<details>
<summary>Lo que verás</summary>

La **página de login** pintada dentro del contenedor de la tabla. Sin la cabecera, la cookie responde **302** a `/Cuenta/Login`; `fetch` sigue la redirección y recibe el HTML del login con **200**, que el código inserta como si fuera la tabla. Es exactamente el problema que el legacy "resolvía" buscando `_Logon_`. jQuery añadía la cabecera sola; `fetch`, no.

</details>

## Paso 7 — Escribe tú: borrar lotes (6 min)

Añade la acción **Borrar** al controlador:

- `POST /Noticom/Lotes/Borrar/12`, con antiforgery, que **solo** pueda ejecutar el rol `Administrador`.
- Llama a `servicio.BorrarAsync(id, ct)` y vuelve a la búsqueda con `Volver(...)`.

La columna y el formulario (con `data-confirmar`, que pide confirmación en `lotes.js`) ya están en `_TablaLotes.cshtml`.

<details>
<summary>Solución</summary>

```csharp
    // POST /Noticom/Lotes/Borrar/12   (en el legacy bastaba con ver la pantalla "b")
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Administrador)]
    public async Task<IActionResult> Borrar(int id, string? volver, CancellationToken ct) =>
        Volver(await servicio.BorrarAsync(id, ct), $"Lote {id} borrado.", volver);
```

</details>

✅ **Comprueba:**

| # | Usuario | Acción | Resultado esperado |
|---|---|---|---|
| 1 | ana | Pestaña **Borrado** | Sin columna Borrar (no es administradora) |
| 2 | admin | Pestaña **Borrado**, **Borrar** un lote | Pide confirmación; "Lote N borrado." |
| 3 | admin | Pestaña **Consulta**, estado `Validado`: copia el Id de un lote. En F12 → Consola de la pestaña **Borrado**: cambia el `action` de un formulario de borrado a ese Id y envíalo | "Solo se puede borrar un lote pendiente de validación..." — la regla está en el **dominio**, no en la columna |

## Errores típicos

| Síntoma | Causa probable | Solución |
|---|---|---|
| `InvalidOperationException: The AuthorizationPolicy named: 'GestionarLotes' was not found` | Política no registrada | Paso 2 |
| Los enlaces del paginador no conservan el modo | Falta `asp-all-route-data="Model.RutaPagina(...)"` | Paso 5b |
| Al validar, vuelves a la portada de lotes y no a tu búsqueda | Falta el campo oculto `volver` en el formulario | Paso 5b |
| La página se recarga entera al buscar (sin fetch) | Falta la sección `Scripts`; o el contenedor no es `id="lotes"` o el formulario no es `id="filtro-lotes"` (el script no los encuentra y no hace nada) | Pasos 5c y 6 |
| Consola: *Refused to execute inline script* | Se ha dejado algún `<script>` con código en la vista | Todo al `.js` |
| La tabla muestra el login dentro | Falta la cabecera `X-Requested-With` | Paso 6 |
| 400 al pulsar Validar | El formulario se escribió con `action="..."` a mano: sin token antiforgery | `asp-action` |

## Ampliación opcional

1. **Crear remesa**: añade la acción `CrearRemesa(CrearRemesaFormulario formulario, string? volver, ...)` con la política `GestionarLotes` y, si va bien, redirige a una acción `Remesas` que liste `servicio.ListarRemesasAsync()` (solución completa en `src/day-05`). Prueba a remesar un lote **pendiente** forzando el POST: ¿qué responde?
2. **Pruebas**: copia `tests/GestorIncidencias.UnitTests/Dominio/LoteTests.cs` y ejecútalas. Escribe una prueba de integración que compruebe que Luis recibe **302 a AccesoDenegado** al validar (mira `CasoPracticoTests` en la solución: `ClienteWebAsync` inicia sesión por el formulario).
3. **JSON en lugar de HTML**: si una pantalla de Noticom consume JSON (`return Json(...)`), compara la respuesta de MVC 5 (`{"Ok":true}`) con la de ASP.NET Core (`{"ok":true}`). ¿Qué cambiarías: el JavaScript o la configuración del serializador?

## Resumen: qué has hecho

| Legacy (MVC 5) | ASP.NET Core |
|---|---|
| `TipoEjecucion` string en vista, JS y servicio | `ModoLotes` (Web) + `FiltroLotes` (Application) |
| Cuatro `if` para el título; columnas ocultas por número | `LotesViewModel.Titulo` y `ColumnaXxx` |
| `[HttpPost] ConsultaLotes` para leer | `GET Tabla` → `PartialView` (la misma que usa `Index`) |
| `sessionStorage["FiltroLotes"]` | Query string + campo `volver` + `history.replaceState` |
| `<script>` inline con Razor; `onclick` | `lotes.js` con `data-*` y delegación de eventos |
| `_Logon_` en una respuesta 200 | 401 gracias a `X-Requested-With` |
| `Validar`/`Borrar` sin autorización ni antiforgery | `[Authorize(Policy/Roles)]` + antiforgery global + reglas en el dominio |

## Referencias

> Enlaces comprobados el 9 de octubre de 2026.

- [Vistas parciales](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/partial?view=aspnetcore-10.0)
- [Tag Helper de anclaje: `asp-all-route-data`](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/tag-helpers/built-in/anchor-tag-helper?view=aspnetcore-10.0)
- [Autorización basada en roles](https://learn.microsoft.com/es-es/aspnet/core/security/authorization/roles?view=aspnetcore-10.0)
- [Uso de Fetch (MDN)](https://developer.mozilla.org/es/docs/Web/API/Fetch_API/Using_Fetch)
