# 4. Controladores MVC

Ayer vimos MVC de pasada. Hoy lo usamos de verdad para la interfaz web del Gestor de Incidencias. Todo el código está en [IncidenciasController.cs](../../src/day-02/GestorIncidencias.Web/Controllers/IncidenciasController.cs).

## 4.1 El ciclo de una petición MVC

```
 GET /Incidencias/Detalle/3
          │
          ▼
 ┌─────────────────┐   1. El enrutamiento decide controlador y acción
 │   Enrutamiento  │      {controller=Incidencias}/{action=Detalle}/{id=3}
 └────────┬────────┘
          ▼
 ┌─────────────────┐   2. Model binding: rellena los parámetros (id = 3)
 │  Model binding  │   3. Validación: rellena ModelState
 └────────┬────────┘
          ▼
 ┌─────────────────┐   4. La acción llama al caso de uso y decide el resultado
 │  Acción         │      return View(incidencia);
 └────────┬────────┘
          ▼
 ┌─────────────────┐   5. El resultado se ejecuta: busca Views/Incidencias/Detalle.cshtml,
 │  ViewResult     │      la combina con el modelo y genera HTML
 └────────┬────────┘
          ▼
       HTML al navegador
```

## 4.2 Qué es un controlador

Una clase que agrupa **acciones** relacionadas. Por convención:

```csharp
public class IncidenciasController(IIncidenciaService servicio) : Controller
//           ^^^^^^^^^^^ nombre del controlador = "Incidencias" (se quita el sufijo Controller)
{
    public async Task<IActionResult> Index(EstadoIncidencia? estado, CancellationToken ct)
    //                               ^^^^^ nombre de la acción
    {
        var incidencias = await servicio.ListarAsync(estado, ct);
        return View(new ListadoIncidenciasViewModel(incidencias, estado));
        //     ^^^^ busca Views/Incidencias/Index.cshtml
    }
}
```

| Clase base | Para qué | Métodos de ayuda |
|---|---|---|
| `Controller` | **MVC con vistas** | `View()`, `PartialView()`, `RedirectToAction()`, `TempData`, `ViewData`... y todo lo de `ControllerBase` |
| `ControllerBase` | **API** (sin vistas) | `Ok()`, `NotFound()`, `BadRequest()`, `Problem()`, `CreatedAtAction()`... |

Las dependencias llegan **por el constructor** (inyección de dependencias del día 1). El contenedor crea un controlador **nuevo por cada petición**.

## 4.3 Enrutamiento

### Enrutamiento convencional (el que usamos para MVC)

Una plantilla global en `Program.cs`:

```csharp
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
```

| URL | Controlador | Acción | `id` |
|---|---|---|---|
| `/` | `Home` (por defecto) | `Index` (por defecto) | — |
| `/Incidencias` | `Incidencias` | `Index` | — |
| `/Incidencias/Crear` | `Incidencias` | `Crear` | — |
| `/Incidencias/Detalle/3` | `Incidencias` | `Detalle` | `3` |
| `/Incidencias?estado=Abierta` | `Incidencias` | `Index` | — (`estado` viene de la *query string*) |

Ventaja: no hay que decorar cada acción. Es el estilo de MVC 5, así que la migración desde Noticom será directa.

### Enrutamiento por atributos (el que usamos para la API)

```csharp
[Route("api/incidencias")]
public class IncidenciasApiController : ControllerBase
{
    [HttpGet("{id:int}")]          // GET /api/incidencias/3
    public ... Obtener(int id) { }

    [HttpPost("{id:int}/resolver")] // POST /api/incidencias/3/resolver
    public ... Resolver(int id) { }
}
```

Más explícito y flexible. Es **obligatorio** en controladores con `[ApiController]`.

> Las restricciones como `{id:int}` hacen que la ruta solo coincida si el valor es un entero: `/api/incidencias/abc` da 404 en lugar de llegar a la acción con un valor incorrecto.

## 4.4 Tipos de resultado (`IActionResult`)

La acción no escribe la respuesta: **devuelve un objeto que describe qué hacer**.

| Método | Resultado | Cuándo |
|---|---|---|
| `View()` / `View(modelo)` | HTML de la vista con el mismo nombre que la acción | Mostrar una página |
| `View("OtraVista", modelo)` | HTML de otra vista | Reutilizar una vista |
| `RedirectToAction(nameof(Detalle), new { id })` | `302` a otra acción | Después de un POST correcto |
| `NotFound()` | `404` | El recurso no existe |
| `BadRequest()` | `400` | Petición incorrecta |
| `PartialView("_Estado", modelo)` | Fragmento HTML sin layout | Peticiones AJAX |
| `Json(objeto)` | JSON | Pequeñas llamadas AJAX desde las vistas |
| `File(bytes, "application/pdf", "informe.pdf")` | Descarga | Exportaciones |

> Usad `nameof(Detalle)` en lugar de `"Detalle"`: si alguien renombra la acción, el compilador avisa.

## 4.5 Model binding: de la petición a los parámetros

ASP.NET Core rellena los parámetros de la acción buscando **por nombre** en varias fuentes:

```csharp
public async Task<IActionResult> Index(EstadoIncidencia? estado, CancellationToken ct)
// /Incidencias?estado=Abierta    → estado = EstadoIncidencia.Abierta (convierte el texto al enum)
// /Incidencias                   → estado = null

public async Task<IActionResult> Detalle(int id, CancellationToken ct)
// /Incidencias/Detalle/3         → id = 3 (de la ruta)

public async Task<IActionResult> Crear(IncidenciaFormulario formulario, CancellationToken ct)
// POST con Titulo=...&Descripcion=...&Prioridad=2
//                                → formulario.Titulo, formulario.Descripcion, formulario.Prioridad
```

| Fuente | Ejemplo | Atributo para forzarla |
|---|---|---|
| Campos de formulario (POST) | `Titulo=Hola` | `[FromForm]` |
| Valores de ruta | `/Detalle/3` | `[FromRoute]` |
| *Query string* | `?estado=Abierta` | `[FromQuery]` |
| Cuerpo JSON (APIs) | `{ "titulo": "Hola" }` | `[FromBody]` |
| Cabeceras | `X-Correlation-Id` | `[FromHeader]` |
| Servicios de DI | — | `[FromServices]` |

`CancellationToken` es especial: ASP.NET Core lo rellena con un token que se cancela si el usuario cierra el navegador. Pasarlo hasta EF Core evita trabajo inútil.

> **Comparación con Web Forms:** no hay `Request.Form["txtTitulo"]` ni `int.Parse(hfId.Value)`. El model binding hace la conversión de tipos y, si falla (`id=abc`), lo anota en `ModelState`.

## 4.6 Validación con DataAnnotations y ModelState

El ViewModel declara las reglas de la interfaz con atributos ([IncidenciaFormulario.cs](../../src/day-02/GestorIncidencias.Web/Models/IncidenciaFormulario.cs)):

```csharp
public class IncidenciaFormulario
{
    [Display(Name = "Título")]
    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(120, MinimumLength = 5, ErrorMessage = "El título debe tener entre {2} y {1} caracteres.")]
    public string Titulo { get; set; } = string.Empty;
    // ...
}
```

Tras el model binding, MVC **valida automáticamente** y deja el resultado en `ModelState`. En los controladores MVC **somos nosotros** quienes lo comprobamos:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Crear(IncidenciaFormulario formulario, CancellationToken ct)
{
    if (!ModelState.IsValid)
        return View(formulario);          // 1. Errores de formato → se repinta el formulario con los mensajes

    var resultado = await servicio.CrearAsync(formulario.AComando(), ct);
    if (!resultado.Exito)
    {
        ModelState.AddModelError(string.Empty, resultado.Error!);  // 2. Error de negocio → mensaje general
        return View(formulario);
    }

    TempData["Mensaje"] = $"Incidencia {resultado.Valor!.Id} creada correctamente.";
    return RedirectToAction(nameof(Detalle), new { id = resultado.Valor.Id });   // 3. Éxito → redirigir
}
```

Dos niveles de validación, cada uno en su sitio:

| Nivel | Dónde | Ejemplo | Cómo llega al usuario |
|---|---|---|---|
| Formato / obligatoriedad | ViewModel (Web) | Título vacío | Mensaje junto al campo (`asp-validation-for`) |
| Reglas de negocio | Dominio / Aplicación | "No más de 10 abiertas" | Mensaje general (`asp-validation-summary`) |

> ⚠️ **Trampa con `record`:** en Minimal APIs (día 1) poníamos los atributos con `[property: Required]`. En **MVC** los atributos de un `record` posicional van **en el parámetro**: `record CrearIncidenciaRequest([Required] string Titulo, ...)`. Con `property:` MVC lanza `InvalidOperationException` al validar. Para formularios, lo más sencillo es usar una **clase** con propiedades `{ get; set; }`, como `IncidenciaFormulario`.

## 4.7 GET + POST y el patrón Post-Redirect-Get

Una pantalla de formulario necesita **dos acciones con el mismo nombre**:

```csharp
[HttpGet]  public IActionResult Crear() => View(new IncidenciaFormulario());     // muestra el formulario
[HttpPost] public async Task<IActionResult> Crear(IncidenciaFormulario f, ...)  // lo procesa
```

Y tras un POST correcto, **siempre** se redirige (patrón **PRG**):

```
 Navegador                          Servidor
    │  POST /Incidencias/Crear         │
    │ ───────────────────────────────▶ │  crea la incidencia 7
    │  302 Location: /Incidencias/Detalle/7
    │ ◀─────────────────────────────── │
    │  GET /Incidencias/Detalle/7      │
    │ ───────────────────────────────▶ │
    │  200 HTML                        │
    │ ◀─────────────────────────────── │
    │                                  │
    │  (el usuario pulsa F5 → repite el GET, NO el POST: no se duplica la incidencia)
```

> En Web Forms todo era un POST a la misma página (*PostBack*) y pulsar F5 mostraba el famoso "¿Desea volver a enviar el formulario?". PRG elimina ese problema.

### TempData: un mensaje que sobrevive a la redirección

Tras redirigir, la acción `Detalle` es **otra petición**: las variables de la anterior se han perdido. `TempData` guarda un valor (en una cookie, por defecto) que se lee **una sola vez** en la siguiente petición:

```csharp
TempData["Mensaje"] = "Incidencia resuelta.";       // en el POST
return RedirectToAction(nameof(Detalle), new { id });
```

```cshtml
@* Views/Shared/_Mensajes.cshtml, incluida en el layout *@
@if (TempData["Mensaje"] is string mensaje)
{
    <div class="aviso aviso-ok">@mensaje</div>
}
```

## 4.8 Protección CSRF (antiforgery)

Un sitio malicioso podría tener un formulario oculto que haga `POST /Incidencias/Cerrar/3` en nuestra aplicación usando la sesión del usuario. Para evitarlo:

1. El Tag Helper `<form method="post">` añade **automáticamente** un campo oculto `__RequestVerificationToken`.
2. `[ValidateAntiForgeryToken]` en la acción rechaza (400) cualquier POST sin un token válido.

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Cerrar(int id, CancellationToken ct) => ...
```

> Las Razor Pages validan el token **automáticamente** en todos los POST. En MVC hay que poner el atributo (o registrar el filtro global `AutoValidateAntiforgeryTokenAttribute`). Lo ampliaremos el día 4 (seguridad).

## 4.9 Acciones que cambian estado: siempre POST

Iniciar, Resolver y Cerrar **modifican datos**, así que son POST, nunca un enlace GET:

```csharp
[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> Resolver(int id, CancellationToken ct) =>
    TrasCambioDeEstado(id, await servicio.ResolverAsync(id, ct), "Incidencia resuelta.");

private IActionResult TrasCambioDeEstado(int id, Resultado<IncidenciaDto> resultado, string mensajeExito)
{
    if (resultado.Tipo == TipoError.NoEncontrado)
        return NotFound();

    if (resultado.Exito) TempData["Mensaje"] = mensajeExito;
    else                 TempData["Error"] = resultado.Error;

    return RedirectToAction(nameof(Detalle), new { id });
}
```

¿Por qué no GET? Los navegadores, los antivirus y los buscadores **siguen enlaces** para precargarlos. Un `<a href="/Incidencias/Cerrar/3">` podría cerrar incidencias sin que nadie haga clic.

## 4.10 Controladores delgados

El controlador **traduce** entre HTTP y los casos de uso. Nada más.

```csharp
// ❌ Controlador "gordo": reglas de negocio y acceso a datos en la acción
[HttpPost]
public async Task<IActionResult> Cerrar(int id, [FromServices] IncidenciasDbContext db)
{
    var i = await db.Incidencias.FindAsync(id);
    if (i.Estado != EstadoIncidencia.Resuelta) { ... }   // regla de negocio
    i.Estado = EstadoIncidencia.Cerrada;                   // (no compila: el setter es privado ✔)
    await db.SaveChangesAsync();                           // acceso a datos
    return RedirectToAction("Index");
}

// ✅ Controlador delgado: delega en el caso de uso y decide la navegación
[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> Cerrar(int id, CancellationToken ct) =>
    TrasCambioDeEstado(id, await servicio.CerrarAsync(id, ct), "Incidencia cerrada.");
```

Responsabilidades del controlador:

| Sí | No |
|---|---|
| Recibir y validar la entrada (ModelState) | Reglas de negocio |
| Convertir ViewModel ↔ comando/DTO | Consultas LINQ / SQL |
| Llamar a **un** caso de uso | Orquestar varios casos de uso con lógica entre ellos |
| Elegir vista, redirección o código HTTP | Enviar correos, escribir ficheros |
| Mensajes para el usuario (TempData) | Calcular fechas, importes, estados |

## 4.11 De Web Forms a MVC: equivalencias

| Web Forms | ASP.NET Core MVC |
|---|---|
| `Incidencias.aspx` + `Incidencias.aspx.cs` | `IncidenciasController` + `Views/Incidencias/*.cshtml` |
| `Page_Load` con `if (!IsPostBack)` | Acción `[HttpGet]` |
| `btnGuardar_Click` | Acción `[HttpPost]` |
| `txtTitulo.Text` | Propiedad del ViewModel rellenada por model binding |
| `Request.QueryString["id"]` | Parámetro de la acción (`int id`) |
| `Page.IsValid` + validadores | `ModelState.IsValid` + DataAnnotations |
| `Response.Redirect("Detalle.aspx?id=3")` | `RedirectToAction(nameof(Detalle), new { id = 3 })` |
| `Session["Mensaje"]` para avisar tras redirigir | `TempData["Mensaje"]` |
| `ViewState` | **No existe**: el estado viaja en el formulario, la URL o se vuelve a cargar |
| `Server.Transfer` | `return View("OtraVista", modelo)` |
| `Response.StatusCode = 404` | `return NotFound()` |

## Preguntas de repaso

1. ¿Qué URL ejecuta la acción `Detalle(int id)` de `IncidenciasController` con `id = 5`?
2. ¿Por qué hay dos métodos `Crear` en el controlador? ¿Cómo sabe ASP.NET Core cuál ejecutar?
3. ¿Qué ocurre si quitamos `return RedirectToAction(...)` y devolvemos `View()` después de crear la incidencia? Prueba a pulsar F5.
4. ¿Qué diferencia hay entre un error en `ModelState` y un `Resultado` con `Exito = false`?
5. ¿Por qué "Cerrar" no puede ser un enlace `<a href>`?

## Referencias

> Enlaces comprobados el 6 de octubre de 2026.

- [Control de solicitudes con controladores en ASP.NET Core MVC](https://learn.microsoft.com/es-es/aspnet/core/mvc/controllers/actions?view=aspnetcore-10.0) — Controladores, acciones y tipos de resultado.
- [Enrutamiento a acciones de controlador](https://learn.microsoft.com/es-es/aspnet/core/mvc/controllers/routing?view=aspnetcore-10.0) — Rutas convencionales y por atributos.
- [Enlace de modelos](https://learn.microsoft.com/es-es/aspnet/core/mvc/models/model-binding?view=aspnetcore-10.0) — Fuentes, conversión de tipos y atributos `[From*]`.
- [Validación de modelos](https://learn.microsoft.com/es-es/aspnet/core/mvc/models/validation?view=aspnetcore-10.0) — DataAnnotations y `ModelState`.
- [Inserción de dependencias en controladores](https://learn.microsoft.com/es-es/aspnet/core/mvc/controllers/dependency-injection?view=aspnetcore-10.0)
- [Administración de sesiones y estado — TempData](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/app-state?view=aspnetcore-10.0#tempdata)
- [Prevención de ataques CSRF](https://learn.microsoft.com/es-es/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [Tutorial: Introducción a ASP.NET Core MVC](https://learn.microsoft.com/es-es/aspnet/core/tutorials/first-mvc-app/start-mvc?view=aspnetcore-10.0) — Tutorial oficial paso a paso para repasar en casa.
