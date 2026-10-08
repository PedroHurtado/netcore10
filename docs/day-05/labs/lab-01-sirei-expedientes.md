# Lab 1 — SIREI: de Web Forms a MVC

**Duración:** 70 min (en parejas) · **Teoría relacionada:** [01 — Método](../01-metodo-migracion.md), [02 — Caso SIREI](../02-caso-sirei.md) · **Legacy:** [legacy/sirei](../legacy/sirei/)

## Objetivo

Migrar las dos pantallas centrales de SIREI (`BuscarExpedientes.aspx` y `ExpedienteDetalle.aspx`) a un **área** `Sirei` de ASP.NET Core MVC, siguiendo la receta del capítulo 1:

1. **Leer** el legacy y fijar el contrato de URLs.
2. **Copiar la base** ya preparada (dominio e infraestructura) y entender sus reglas.
3. **Crear el servicio**, el **controlador** y las **vistas** (el foco del ejercicio).
4. Resolver tú dos operaciones más: **completar trámites** y **conservar las URLs antiguas**.

Trabaja sobre una **copia de `src/day-04`** (la tuya, con los laboratorios de ayer, o una nueva). La solución completa está en `src/day-05`.

> Para no perder el tiempo tecleando lo mecánico, el paso 2 copia ficheros de `src/day-05`. El resto del código está en este documento: **cópialo, pero lee los comentarios**.

## Paso 0 — Preparar la copia (3 min)

```bash
cp -r src/day-04 mi-copia-day-05      # o copia la carpeta con el Explorador (sin bin/ ni obj/)
cd mi-copia-day-05
dotnet build
```

✅ **Comprueba:** compila sin errores.

## Paso 1 — Leer el legacy y fijar el contrato (8 min)

Abre [BuscarExpedientes.aspx.cs](../legacy/sirei/BuscarExpedientes.aspx.cs) y [ExpedienteDetalle.aspx.cs](../legacy/sirei/ExpedienteDetalle.aspx.cs). Con tu compañero, completa la tabla: cada **evento** del legacy, ¿qué **petición HTTP** y qué **acción** será?

| Evento legacy | Verbo + URL nueva | Acción del controlador |
|---|---|---|
| `BuscarExpedientes.aspx` → `Page_Load`, `btnBuscar_Click`, `ddlEstado_SelectedIndexChanged`, `PageIndexChanging` | | |
| `ExpedienteDetalle.aspx?id=5` → `Page_Load` (`!IsPostBack`) | | |
| `btnGuardar_Click` | | |
| `gvTramites_RowCommand` (`Completar`) | | |
| Un marcador antiguo a `ExpedienteDetalle.aspx?id=5` | | |

Y marca en el código **tres reglas de negocio**, al menos una que solo esté en la interfaz.

<details>
<summary>Respuestas</summary>

| Evento legacy | Verbo + URL nueva | Acción |
|---|---|---|
| Búsqueda (carga, botón, desplegable, paginación) | `GET /Sirei/Expedientes?texto=&estado=&pagina=` | `Index(texto, estado, pagina)` |
| `Page_Load` de la ficha | `GET /Sirei/Expedientes/Detalle/5` | `Detalle(id)` |
| `btnGuardar_Click` | `POST /Sirei/Expedientes/Detalle/5` | `Detalle(id, formulario)` |
| `gvTramites_RowCommand` | `POST /Sirei/Expedientes/CompletarTramite/5?tramiteId=12` | `CompletarTramite(id, tramiteId)` |
| Marcador antiguo | `GET /ExpedienteDetalle.aspx?id=5` → **301** | `DetalleLegacy(id)` |

Reglas: (1) no cerrar con trámites pendientes (`btnGuardar_Click`); (2) un expediente cerrado no admite cambios (**solo** `btnGuardar.Visible = !cerrado`); (3) un trámite se completa una vez y solo los del propio expediente (el legacy **no** lo comprueba).

</details>

## Paso 2 — Copiar la base: dominio e infraestructura (8 min)

### 2a. Copiar ficheros

Copia de `src/day-05` a tu copia, **en las mismas rutas**:

| Origen (`src/day-05/...`) | Qué es |
|---|---|
| `GestorIncidencias.Domain/Expedientes/` (carpeta) | `Expediente`, `Tramite`, `EstadoExpediente` |
| `GestorIncidencias.Application/Abstracciones/IExpedienteRepository.cs` | Puerto para escribir |
| `GestorIncidencias.Application/Abstracciones/IExpedienteConsultas.cs` | Puerto para leer |
| `GestorIncidencias.Application/Expedientes/ExpedienteDto.cs` | DTOs y comando |
| `GestorIncidencias.Application/Expedientes/ExpedienteLog.cs` | Logs (EventId 2001-2004) |
| `GestorIncidencias.Infrastructure/Sirei/` (carpeta) | `SireiDbContext`, configuraciones, repositorio, consultas, datos de demostración |

Mientras copias, **lee** `Expediente.Actualizar` y `ExpedienteConfiguracion`: ¿dónde está la regla de `btnGuardar_Click`? ¿Cómo se llama la columna del estado en la base de datos?

### 2b. Que el contexto de incidencias no se "trague" los expedientes

En `GestorIncidencias.Infrastructure/Persistencia/IncidenciasDbContext.cs`, **sustituye** `OnModelCreating` por:

```csharp
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        // Solo las configuraciones de Persistencia/Configuraciones: el ensamblado tiene ahora las de OTROS contextos (Sirei).
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(IncidenciasDbContext).Assembly,
            tipo => tipo.Namespace == typeof(Configuraciones.IncidenciaConfiguracion).Namespace);
```

Sin el filtro, `ApplyConfigurationsFromAssembly` aplicaría **todas** las configuraciones del ensamblado y el contexto de incidencias mapearía también `Expediente` y `Tramite`: dos contextos con las mismas tablas.

### 2c. Registrar la infraestructura de SIREI

En `GestorIncidencias.Infrastructure/DependencyInjection.cs`, añade el `using` y, **antes** de `services.AddIdentidad();`, el registro:

```csharp
using GestorIncidencias.Infrastructure.Sirei;
```

```csharp
        // Día 5 — SIREI: OTRA base de datos (la que ya existe). Con SQL Server: UseSqlServer(...GetConnectionString("Sirei"))
        services.AddDbContext<SireiDbContext>(opt => opt.UseInMemoryDatabase("Sirei"));
        services.AddScoped<IExpedienteRepository, EfExpedienteRepository>();
        services.AddScoped<IExpedienteConsultas, EfExpedienteConsultas>();
        services.AddHostedService<InicializadorSirei>();
        services.AddHealthChecks().AddDbContextCheck<SireiDbContext>("sirei");
```

### 2d. Estilos

Al final de `GestorIncidencias.Web/wwwroot/css/site.css`:

```css
/* Día 5: SIREI */
.expediente-abierto { background: #dbeafe; color: #1e40af; }
.expediente-entramite { background: #fef3c7; color: #92400e; }
.expediente-suspendido { background: #fee2e2; color: #991b1b; }
.expediente-cerrado { background: #e5e7eb; color: #374151; }
.texto-largo { white-space: pre-line; }
```

✅ **Comprueba:** `dotnet build` compila. Arranca la aplicación: en la consola aparece `Datos de demostración de SIREI cargados (45 expedientes, ...)`. Para con `Ctrl+C`.

## Paso 3 — El servicio (casos de uso) (10 min)

### 3a. La interfaz

Crea `GestorIncidencias.Application/Expedientes/IExpedienteService.cs`:

```csharp
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Expedientes;

namespace GestorIncidencias.Application.Expedientes;

/// <summary>Casos de uso de los expedientes: sustituyen a Page_Load, btnBuscar_Click, btnGuardar_Click y gvTramites_RowCommand.</summary>
public interface IExpedienteService
{
    Task<Pagina<ExpedienteDto>> ListarAsync(string? texto = null, EstadoExpediente? estado = null, int pagina = 1, int tamanoPagina = Pagina<ExpedienteDto>.TamanoPorDefecto, CancellationToken ct = default);
    Task<ExpedienteDetalleDto?> ObtenerAsync(int id, CancellationToken ct = default);

    Task<Resultado> ActualizarAsync(int id, ActualizarExpedienteComando comando, CancellationToken ct = default);
    Task<Resultado> AgregarTramiteAsync(int id, string descripcion, CancellationToken ct = default);
    Task<Resultado> CompletarTramiteAsync(int id, int tramiteId, CancellationToken ct = default);
}
```

### 3b. La implementación

Crea `GestorIncidencias.Application/Expedientes/ExpedienteService.cs`. Fíjate en `ActualizarAsync`: es `btnGuardar_Click` sin la interfaz, con la **concurrencia optimista** que el legacy no tenía.

```csharp
using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Expedientes;
using Microsoft.Extensions.Logging;

namespace GestorIncidencias.Application.Expedientes;

public class ExpedienteService(
    IExpedienteRepository repositorio,
    IExpedienteConsultas consultas,
    IUsuarioActual usuario,
    TimeProvider reloj,
    ILogger<ExpedienteService> logger) : IExpedienteService
{
    // ------------------------------------------------------------------ Lecturas

    public Task<Pagina<ExpedienteDto>> ListarAsync(
        string? texto = null, EstadoExpediente? estado = null, int pagina = 1, int tamanoPagina = Pagina<ExpedienteDto>.TamanoPorDefecto,
        CancellationToken ct = default)
    {
        (pagina, tamanoPagina) = Pagina<ExpedienteDto>.Normalizar(pagina, tamanoPagina);
        texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
        return consultas.ListarAsync(texto, estado, pagina, tamanoPagina, ct);
    }

    public Task<ExpedienteDetalleDto?> ObtenerAsync(int id, CancellationToken ct = default) =>
        consultas.ObtenerDetalleAsync(id, ct);

    // ------------------------------------------------------------------ Escrituras

    public async Task<Resultado> ActualizarAsync(int id, ActualizarExpedienteComando comando, CancellationToken ct = default)
    {
        var expediente = await repositorio.ObtenerPorIdAsync(id, ct);   // con sus trámites (Include)
        if (expediente is null)
            return Resultado.Fallo($"No existe el expediente {id}.", TipoError.NoEncontrado);

        // CONCURRENCIA OPTIMISTA: el formulario trae la versión que el usuario tenía delante.
        if (expediente.Version != comando.Version)
        {
            logger.ConflictoConcurrencia(expediente.Numero, comando.Version, expediente.Version);
            return Resultado.Fallo(
                "Otra persona ha modificado el expediente mientras lo editabas. Se muestran los datos actuales: revisa y vuelve a guardar.",
                TipoError.Conflicto);
        }

        // La regla de btnGuardar_Click, ahora en el dominio.
        var resultado = expediente.Actualizar(comando.Observaciones, comando.Estado, reloj.GetUtcNow());
        if (!resultado.Exito)
        {
            logger.OperacionRechazada(id, resultado.Error!);
            return resultado;
        }

        await repositorio.GuardarCambiosAsync(ct);   // UPDATE parametrizado ... WHERE Id = @id AND Version = @original
        logger.ExpedienteActualizado(expediente.Numero, expediente.Estado, expediente.Version, usuario.Nombre);
        return resultado;
    }

    public Task<Resultado> AgregarTramiteAsync(int id, string descripcion, CancellationToken ct = default) =>
        ModificarAsync(id, expediente =>
        {
            var tramite = expediente.AgregarTramite(descripcion, reloj.GetUtcNow());
            return tramite.Exito ? Resultado.Ok() : Resultado.Fallo(tramite.Error!, tramite.Tipo!.Value);
        }, "trámite añadido", ct);

    public Task<Resultado> CompletarTramiteAsync(int id, int tramiteId, CancellationToken ct = default) =>
        ModificarAsync(id, expediente => expediente.CompletarTramite(tramiteId, reloj.GetUtcNow()), $"trámite {tramiteId} completado", ct);

    /// <summary>Patrón común: cargar el agregado → pedirle el cambio → guardar.</summary>
    private async Task<Resultado> ModificarAsync(int id, Func<Expediente, Resultado> cambio, string operacion, CancellationToken ct)
    {
        var expediente = await repositorio.ObtenerPorIdAsync(id, ct);
        if (expediente is null)
            return Resultado.Fallo($"No existe el expediente {id}.", TipoError.NoEncontrado);

        var resultado = cambio(expediente);
        if (!resultado.Exito)
        {
            logger.OperacionRechazada(id, resultado.Error!);
            return resultado;
        }

        await repositorio.GuardarCambiosAsync(ct);
        logger.TramitesModificados(expediente.Numero, operacion, usuario.Nombre);
        return resultado;
    }
}
```

### 3c. Registrarlo

En `GestorIncidencias.Application/DependencyInjection.cs`, añade `using GestorIncidencias.Application.Expedientes;` y, debajo del registro de `IIncidenciaService`:

```csharp
        services.AddScoped<IExpedienteService, ExpedienteService>();   // Día 5: SIREI
```

✅ **Comprueba:** `dotnet build` compila.

## Paso 4 — El controlador en un área (12 min)

### 4a. Los ViewModels

Crea `GestorIncidencias.Web/Areas/Sirei/Models/ExpedientesViewModels.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Expedientes;
using GestorIncidencias.Domain.Expedientes;

namespace GestorIncidencias.Web.Areas.Sirei.Models;

/// <summary>Lo que BuscarExpedientes.aspx guardaba en el ViewState (texto, estado, página) viaja en la query string.</summary>
public record ListadoExpedientesViewModel(Pagina<ExpedienteDto> Pagina, string? FiltroTexto, EstadoExpediente? FiltroEstado);

/// <summary>El formulario de la ficha: ddlEstado, txtObservaciones... y la versión que se está editando (campo oculto).</summary>
public class ExpedienteFormulario
{
    [Display(Name = "Estado")]
    [Required(ErrorMessage = "El estado es obligatorio.")]
    public EstadoExpediente? Estado { get; set; }

    [Display(Name = "Observaciones")]
    [StringLength(Expediente.ObservacionesLongitudMaxima, ErrorMessage = "Las observaciones no pueden superar {1} caracteres.")]
    [DataType(DataType.MultilineText)]
    public string? Observaciones { get; set; }

    public int Version { get; set; }

    public static ExpedienteFormulario Desde(ExpedienteDetalleDto expediente) => new()
    {
        Estado = expediente.Estado,
        Observaciones = expediente.Observaciones,
        Version = expediente.Version
    };

    public ActualizarExpedienteComando AComando() => new(Observaciones, Estado!.Value, Version);
}

/// <summary>La ficha: los datos actuales (solo lectura) y el formulario (lo que el usuario edita).</summary>
public record FichaExpedienteViewModel(ExpedienteDetalleDto Expediente, ExpedienteFormulario Formulario);
```

### 4b. El controlador

Crea `GestorIncidencias.Web/Areas/Sirei/Controllers/ExpedientesController.cs`:

```csharp
using GestorIncidencias.Application.Expedientes;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Expedientes;
using GestorIncidencias.Web.Areas.Sirei.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorIncidencias.Web.Areas.Sirei.Controllers;

/// <summary>
/// BuscarExpedientes.aspx y ExpedienteDetalle.aspx convertidas en UN controlador del área "Sirei".
/// Sin [Authorize]: la FallbackPolicy hace lo que hacía "if (Session["Usuario"] == null) Response.Redirect(...)".
/// </summary>
[Area("Sirei")]
public class ExpedientesController(IExpedienteService servicio) : Controller
{
    // GET /Sirei/Expedientes?texto=obra&estado=EnTramite&pagina=2
    public async Task<IActionResult> Index(string? texto, EstadoExpediente? estado, int pagina = 1, CancellationToken ct = default)
    {
        var resultado = await servicio.ListarAsync(texto, estado, pagina, ct: ct);
        return View(new ListadoExpedientesViewModel(resultado, texto, estado));
    }

    // GET /Sirei/Expedientes/Detalle/5   ← antes: ExpedienteDetalle.aspx?id=5 + Page_Load(!IsPostBack)
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        var expediente = await servicio.ObtenerAsync(id, ct);
        if (expediente is null)
            return NotFound();

        return View(new FichaExpedienteViewModel(expediente, ExpedienteFormulario.Desde(expediente)));
    }

    // POST /Sirei/Expedientes/Detalle/5   ← antes: btnGuardar_Click
    // [Bind(Prefix)]: los campos de la vista se llaman "Formulario.Estado", "Formulario.Version"...
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Detalle(
        int id, [Bind(Prefix = nameof(FichaExpedienteViewModel.Formulario))] ExpedienteFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await servicio.ActualizarAsync(id, formulario.AComando(), ct);
            if (resultado.Tipo == TipoError.NoEncontrado)
                return NotFound();

            if (resultado.Exito)
            {
                TempData["Mensaje"] = "Expediente guardado.";              // antes: lblMensaje.Text = "Guardado"
                return RedirectToAction(nameof(Detalle), new { id });      // Post-Redirect-Get
            }

            ModelState.AddModelError(string.Empty, resultado.Error!);     // antes: lblError.Text = "..."
        }

        // Volver a pintar la ficha: datos ACTUALES de la BD + lo que escribió el usuario (lo que hacía el ViewState).
        var actual = await servicio.ObtenerAsync(id, ct);
        if (actual is null)
            return NotFound();

        // Si otra persona guardó entretanto, se recarga con sus datos y la versión nueva.
        if (actual.Version != formulario.Version)
        {
            TempData["Error"] = ModelState[string.Empty]?.Errors.FirstOrDefault()?.ErrorMessage;
            return RedirectToAction(nameof(Detalle), new { id });
        }

        return View(new FichaExpedienteViewModel(actual, formulario));
    }

    private IActionResult TrasCambio(int id, Resultado resultado, string mensajeExito)
    {
        if (resultado.Exito)
            TempData["Mensaje"] = mensajeExito;
        else
            TempData["Error"] = resultado.Error;

        return RedirectToAction(nameof(Detalle), new { id });
    }
}
```

(`TrasCambio` todavía no se usa: lo necesitarás en el paso 6.)

### 4c. La ruta del área

En `GestorIncidencias.Web/Program.cs`, **antes** de `app.MapControllerRoute(name: "default", ...)`:

```csharp
// Día 5 — ÁREA Sirei: /Sirei → ExpedientesController.Index. Antes que la ruta "default" (lo específico primero).
app.MapAreaControllerRoute(
        name: "sirei",
        areaName: "Sirei",
        pattern: "Sirei/{controller=Expedientes}/{action=Index}/{id?}")
    .CacheOutput(CacheHtmlPolicy.Nombre);
```

✅ **Comprueba:** `dotnet build` compila. (Todavía no hay vistas: si abres `/Sirei` verás `The view 'Index' was not found`.)

## Paso 5 — Las vistas (12 min)

### 5a. Lo propio del área

Las vistas de un área **no** heredan `Views/_ViewImports.cshtml` ni `Views/_ViewStart.cshtml`. Crea los dos en `GestorIncidencias.Web/Areas/Sirei/Views/`:

`_ViewImports.cshtml`:

```cshtml
@using GestorIncidencias.Web
@using GestorIncidencias.Web.Areas.Sirei.Models
@using GestorIncidencias.Application.Comun
@using GestorIncidencias.Application.Expedientes
@using GestorIncidencias.Domain.Expedientes
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

`_ViewStart.cshtml`:

```cshtml
@{
    Layout = "_Layout";
}
```

Y la etiqueta de estado, `Areas/Sirei/Views/Shared/_EstadoExpediente.cshtml` (antes: el `switch` de `gvExpedientes_RowDataBound`):

```cshtml
@model EstadoExpediente
<span class="etiqueta expediente-@Model.ToString().ToLowerInvariant()">@Model</span>
```

### 5b. La búsqueda

Copia de la solución `src/day-05/GestorIncidencias.Web/Areas/Sirei/Views/Expedientes/Index.cshtml` (es el mismo patrón que el listado de incidencias y el Lab 1 de ayer). Léela comparándola con [BuscarExpedientes.aspx](../legacy/sirei/BuscarExpedientes.aspx): formulario GET, tabla con `@foreach`, paginador que conserva `texto` y `estado`.

### 5c. La ficha

Crea `Areas/Sirei/Views/Expedientes/Detalle.cshtml`:

```cshtml
@model FichaExpedienteViewModel
@{
    var expediente = Model.Expediente;
    var cerrado = expediente.Estado == EstadoExpediente.Cerrado;
    ViewData["Title"] = $"Expediente {expediente.Numero}";
}

<p><a asp-action="Index">← Volver a la búsqueda</a></p>

<h1>Expediente @expediente.Numero</h1>

<dl class="ficha">
    <dt>Titular</dt>
    <dd>@expediente.Titular</dd>
    <dt>Asunto</dt>
    <dd>@expediente.Asunto</dd>
    <dt>Estado actual</dt>
    <dd><partial name="_EstadoExpediente" model="expediente.Estado" /></dd>
    <dt>Última modificación</dt>
    <dd>@expediente.FechaModificacion.ToLocalTime().ToString("dd/MM/yyyy HH:mm") <span class="nota">(versión @expediente.Version)</span></dd>
</dl>

@if (cerrado)
{
    <p class="nota">El expediente está cerrado: no admite cambios.</p>
}
else
{
    @* POST a /Sirei/Expedientes/Detalle/5: el id va en la ruta (antes: ViewState["IdExpediente"]) *@
    <h2>Datos del expediente</h2>
    <form asp-action="Detalle" asp-route-id="@expediente.Id" method="post" class="formulario">
        <div asp-validation-summary="ModelOnly" class="errores"></div>

        <input type="hidden" asp-for="Formulario.Version" />

        <div class="campo">
            <label asp-for="Formulario.Estado"></label>
            <select asp-for="Formulario.Estado" asp-items="Html.GetEnumSelectList<EstadoExpediente>()"></select>
            <span asp-validation-for="Formulario.Estado" class="error-campo"></span>
        </div>

        <div class="campo">
            <label asp-for="Formulario.Observaciones"></label>
            <textarea asp-for="Formulario.Observaciones" rows="4"></textarea>
            <span asp-validation-for="Formulario.Observaciones" class="error-campo"></span>
        </div>

        <button type="submit" class="boton">Guardar</button>
    </form>
}

<h2>Trámites (@expediente.Tramites.Count)</h2>
@* Paso 6: la tabla de trámites *@
```

### 5d. El menú

En `Views/Shared/_Layout.cshtml`, añade el enlace a SIREI después del de Razor Pages:

```cshtml
            <a asp-area="Sirei" asp-controller="Expedientes" asp-action="Index">SIREI</a>
```

Arranca, entra como **luis** y abre `/Sirei`. Pulsa cualquier enlace del menú (por ejemplo **Incidencias (MVC)**): **no hace nada**. Inspecciona el enlace con F12: `href=""`.

**Piensa:** ¿por qué? (Pista: desde `/Sirei`, ¿en qué área busca el Tag Helper el controlador `Incidencias`?)

<details>
<summary>Respuesta y arreglo</summary>

Los Tag Helpers de enlaces **heredan el área de la página actual** (valor de ruta "ambiental"). Desde `/Sirei`, `asp-controller="Incidencias"` busca `/Sirei/Incidencias`, que no existe, y el `href` queda vacío. Arreglo: añade `asp-area=""` a **todos** los enlaces y formularios del layout que no son del área (marca, Incidencias, Nueva, Razor Pages, Usuarios, Salir, Iniciar sesión). Por ejemplo:

```cshtml
<a asp-area="" asp-controller="Incidencias" asp-action="Index">Incidencias (MVC)</a>
<a asp-area="" asp-page="/Paginas/Incidencias/Index">Razor Pages</a>
```

</details>

✅ **Comprueba** (como luis):

| # | Acción | Resultado esperado |
|---|---|---|
| 1 | `/Sirei` | Listado: "Página 1 de 3 · 45 expedientes" |
| 2 | Buscar `obra`; después estado `Cerrado` | Filtra; la URL lleva `?texto=obra&estado=Cerrado` |
| 3 | Abrir **2026/000001**, estado `Cerrado`, Guardar | "No se puede cerrar un expediente con trámites pendientes." y lo escrito en Observaciones **se conserva** |
| 4 | Mismo expediente, estado `EnTramite`, Guardar | "Expediente guardado." y la versión sube |
| 5 | Abre el **mismo** expediente en dos pestañas. Guarda en la primera; después guarda en la segunda | En la segunda: "Otra persona ha modificado el expediente..." y los datos de la primera |

## Paso 6 — Escribe tú: completar trámites (8 min)

En `ExpedienteDetalle.aspx`, el `GridView` de trámites tenía un `ButtonField CommandName="Completar"` y el evento `gvTramites_RowCommand`. Hazlo en ASP.NET Core:

1. Una acción **POST** `CompletarTramite(int id, int tramiteId, CancellationToken ct)` en el controlador que llame a `servicio.CompletarTramiteAsync` y vuelva a la ficha con `TrasCambio`.
2. En `Detalle.cshtml`, debajo de `<h2>Trámites...`, una tabla con: descripción, fecha de alta, "Pendiente" o "Completado el ..." y, **solo** si el trámite está pendiente y el expediente no está cerrado, un formulario POST con un botón **Completar**.

<details>
<summary>Solución</summary>

```csharp
    // POST /Sirei/Expedientes/CompletarTramite/5?tramiteId=12   ← antes: gvTramites_RowCommand (CommandName="Completar")
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompletarTramite(int id, int tramiteId, CancellationToken ct) =>
        TrasCambio(id, await servicio.CompletarTramiteAsync(id, tramiteId, ct), "Trámite completado.");
```

```cshtml
@if (expediente.Tramites.Count == 0)
{
    <p class="vacio">El expediente no tiene trámites.</p>
}
else
{
    <table>
        <thead>
            <tr><th>Trámite</th><th>Alta</th><th>Situación</th><th></th></tr>
        </thead>
        <tbody>
            @foreach (var tramite in expediente.Tramites)
            {
                <tr>
                    <td>@tramite.Descripcion</td>
                    <td>@tramite.FechaAlta.ToLocalTime().ToString("dd/MM/yyyy")</td>
                    <td>@(tramite.Pendiente ? "Pendiente" : $"Completado el {tramite.FechaCompletado?.ToLocalTime():dd/MM/yyyy}")</td>
                    <td>
                        @if (tramite.Pendiente && !cerrado)
                        {
                            <form asp-action="CompletarTramite" asp-route-id="@expediente.Id" asp-route-tramiteId="@tramite.Id" method="post">
                                <button type="submit" class="boton-pequeno">Completar</button>
                            </form>
                        }
                    </td>
                </tr>
            }
        </tbody>
    </table>
}
```

Fíjate en que la acción recibe **el expediente y el trámite**: `Expediente.CompletarTramite` solo busca entre **sus** trámites. En el legacy, `UPDATE Tramites ... WHERE Id = @idTramite` permitía completar el trámite de **cualquier** expediente.

</details>

✅ **Comprueba:** completa los tres trámites del expediente **2026/000001** y ciérralo: ahora sí se puede. Al cerrarlo, desaparecen el formulario y los botones.

## Paso 7 — Escribe tú: las URLs antiguas (4 min)

Hay correos y marcadores con `ExpedienteDetalle.aspx?id=5` y `BuscarExpedientes.aspx`. Añade al controlador dos acciones que respondan a esas rutas **exactas** con una redirección **permanente** (301) a las nuevas. Pista: una ruta por atributo que empieza por `/` ignora el área y el controlador; `RedirectToActionPermanent`.

¿Deben exigir haber iniciado sesión?

<details>
<summary>Solución</summary>

```csharp
    // GET /ExpedienteDetalle.aspx?id=5  →  301 /Sirei/Expedientes/Detalle/5
    [HttpGet("/ExpedienteDetalle.aspx")]
    [AllowAnonymous]
    public IActionResult DetalleLegacy(int id) => RedirectToActionPermanent(nameof(Detalle), new { id });

    // GET /BuscarExpedientes.aspx  →  301 /Sirei
    [HttpGet("/BuscarExpedientes.aspx")]
    [AllowAnonymous]
    public IActionResult IndexLegacy() => RedirectToActionPermanent(nameof(Index));
```

`[AllowAnonymous]`: la redirección no revela nada. Si no hay sesión, la página de destino ya pedirá el login (y volverá a la URL **nueva**).

</details>

✅ **Comprueba:** abre `http://localhost:5196/ExpedienteDetalle.aspx?id=3` → acabas en `/Sirei/Expedientes/Detalle/3`. En F12 → Red, la primera respuesta es un **301**.

## Errores típicos

| Síntoma | Causa probable | Solución |
|---|---|---|
| `CS0246 'IExpedienteRepository' could not be found` | Faltan ficheros del paso 2a o el `using` | Paso 2a / `using GestorIncidencias.Application.Abstracciones;` |
| Al abrir `/Sirei`: `Unable to resolve service for type 'IExpedienteService'` | Falta el registro | Paso 3c |
| Al abrir `/Sirei`: `Unable to resolve service for type 'IExpedienteConsultas'` | Falta el registro de infraestructura | Paso 2c |
| 404 en `/Sirei` | Falta la ruta del área o `[Area("Sirei")]` | Paso 4c / 4b |
| `The view 'Index' was not found. ... /Views/Expedientes/Index.cshtml` | Las vistas no están en `Areas/Sirei/Views/Expedientes/` o falta `[Area]` | Revisar carpetas |
| La página sale sin menú ni estilos | Falta `_ViewStart.cshtml` en el área | Paso 5a |
| En el HTML aparece `asp-for="..."` literal y los formularios no funcionan | Falta `_ViewImports.cshtml` en el área (o su `@addTagHelper`) | Paso 5a |
| Los enlaces del menú no hacen nada desde `/Sirei` | Heredan el área | Paso 5d: `asp-area=""` |
| Guardar siempre da "Otra persona ha modificado..." | El campo oculto `Formulario.Version` no está en el formulario (llega 0) | Paso 5c |
| Al guardar: "El estado es obligatorio." aunque se haya elegido uno | Los `name` no llevan el prefijo `Formulario.` o falta `[Bind(Prefix = ...)]`: el formulario llega vacío | Pasos 4b y 5c: usar `asp-for`, no `name` a mano |

## Ampliación opcional

1. **Añadir trámite**: la acción `AgregarTramite(int id, string? descripcion)` y un formulario al pie de la ficha (solución en `src/day-05`). ¿Qué pasa con una descripción con apóstrofo (`Informe de D'Ors`)? ¿Y en el legacy?
2. **Pruebas**: copia `tests/GestorIncidencias.UnitTests/Dominio/ExpedienteTests.cs` y `Aplicacion/ExpedienteServiceTests.cs` de la solución y ejecútalas. Rompe la regla de cierre y mira cuál falla.
3. **Autorización**: solo técnicos y administradores pueden **cerrar** un expediente; cualquiera puede cambiar las observaciones. ¿Política en la acción, o regla en el caso de uso con `IUsuarioActual`? Discútelo: depende del **dato** enviado, no de la acción.

## Resumen: qué has hecho

| Capa | Fichero | Origen en el legacy |
|---|---|---|
| Domain | `Expedientes/*` (copiado) | Reglas de `btnGuardar_Click`, `btnGuardar.Visible`, `RowCommand` |
| Application | `IExpedienteService`, `ExpedienteService` | Cuerpo de los eventos, sin interfaz; + concurrencia optimista |
| Infrastructure | `Sirei/*` (copiado) | `SqlDataAdapter`, `SqlCommandBuilder`, SQL concatenado |
| Web | `Areas/Sirei/Controllers/ExpedientesController.cs` | `Page_Load`, `btnGuardar_Click`, `gvTramites_RowCommand` |
| Web | `Areas/Sirei/Views/...` | `.aspx` con `GridView`, `DropDownList`, `TextBox`, `Label` |
| Web | `Program.cs`, `_Layout.cshtml` | Ruta del área; `asp-area=""` en el menú |

## Referencias

> Enlaces comprobados el 9 de octubre de 2026.

- [Áreas en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/mvc/controllers/areas?view=aspnetcore-10.0) — Incluye `asp-area=""` en los enlaces.
- [Enlace de modelos](https://learn.microsoft.com/es-es/aspnet/core/mvc/models/model-binding?view=aspnetcore-10.0) — Prefijos y `[Bind]`.
- [Control de conflictos de simultaneidad (EF Core)](https://learn.microsoft.com/es-es/ef/core/saving/concurrency)
- [Enrutamiento a acciones del controlador](https://learn.microsoft.com/es-es/aspnet/core/mvc/controllers/routing?view=aspnetcore-10.0) — Rutas por atributo que empiezan por `/`.
