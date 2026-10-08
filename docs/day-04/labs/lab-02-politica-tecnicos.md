# Lab 2 — Solo los técnicos gestionan incidencias

**Duración:** 30 min · **Teoría relacionada:** [04 — Autenticación y autorización](../04-autenticacion-autorizacion.md), [06 — CSRF y XSS](../06-csrf-xss.md)

## Objetivo

Hoy cualquier usuario autenticado puede iniciar, resolver, cerrar o reabrir incidencias y cambiar su categoría. La nueva regla:

> **Solo los usuarios con rol `Tecnico` o `Administrador` gestionan incidencias.** Cualquier usuario autenticado puede **ver**, **crear** y **comentar**.

Lo haremos con una **política de autorización** y la aplicaremos en los **tres** puntos de entrada (MVC, Razor Pages y API), además de ocultar los botones que el usuario no puede usar.

| Usuario | Rol | Debe poder gestionar |
|---|---|---|
| `ana@demo.local` | Tecnico | ✅ |
| `luis@demo.local` | — | ❌ |
| `admin@demo.local` | Administrador | ✅ |

Trabaja sobre tu copia del [Lab 1](lab-01-busqueda-webforms.md) (o sobre una copia nueva de `src/day-04`).

## Paso 0 — Ver el problema (2 min)

Ejecuta la aplicación, entra como **luis@demo.local** y abre `/Incidencias/Detalle/1`.

✅ **Comprueba:** Luis ve los botones **Iniciar** y **Resolver** y el desplegable de categoría, y puede usarlos. Eso es lo que vamos a cambiar.

## Paso 1 — El nombre de la política (2 min)

Crea el fichero `GestorIncidencias.Web/Seguridad/Politicas.cs`:

```csharp
namespace GestorIncidencias.Web.Seguridad;

/// <summary>Nombres de las políticas de autorización (se registran en Program.cs).</summary>
public static class Politicas
{
    /// <summary>Iniciar, resolver, cerrar, reabrir y cambiar la categoría: técnicos y administradores.</summary>
    public const string GestionarIncidencias = "GestionarIncidencias";
}
```

Una constante, y no el texto `"GestionarIncidencias"` repetido: si te equivocas al escribirla, no compila.

## Paso 2 — Registrar la política (3 min)

En `GestorIncidencias.Web/Program.cs`:

1. Añade arriba el `using`:

```csharp
using GestorIncidencias.Application.Seguridad;
```

2. Busca `AddAuthorizationBuilder()` y añade la política **a continuación** de la *FallbackPolicy*:

```csharp
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy(Politicas.GestionarIncidencias, politica =>
        politica.RequireRole(Roles.Tecnico, Roles.Administrador));   // uno de los dos roles basta
```

✅ **Comprueba:** `dotnet build` compila. (Todavía no cambia nada: la política existe, pero nadie la usa.)

## Paso 3 — Proteger el controlador MVC (4 min)

En `GestorIncidencias.Web/Controllers/IncidenciasController.cs`:

1. Añade los `using`:

```csharp
using GestorIncidencias.Web.Seguridad;
using Microsoft.AspNetCore.Authorization;
```

2. Añade `[Authorize(Policy = Politicas.GestionarIncidencias)]` a las **cinco** acciones de gestión: `Iniciar`, `Resolver`, `Cerrar`, `Reabrir` y `CambiarCategoria`. Por ejemplo:

```csharp
    // POST /Incidencias/Resolver/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Politicas.GestionarIncidencias)]
    public async Task<IActionResult> Resolver(int id, CancellationToken ct) =>
        TrasCambio(id, await servicio.ResolverAsync(id, ct), "Incidencia resuelta.");
```

**No** lo pongas en `Index`, `Detalle`, `Crear` ni `Comentar`: esas siguen protegidas por la *FallbackPolicy* (cualquier usuario autenticado).

**Piensa:** ¿por qué no ponemos el atributo en la clase y `[AllowAnonymous]` en las demás?

<details>
<summary>Respuesta</summary>

Porque `[AllowAnonymous]` **anula toda** la autorización: las acciones de lectura quedarían abiertas a usuarios **sin** sesión. Lo que queremos es "autenticado para todo, técnico para gestionar": la *FallbackPolicy* cubre lo primero y el atributo en cada acción, lo segundo. (Otra opción válida: separar las acciones de gestión en otro controlador con el atributo en la clase.)

</details>

## Paso 4 — Proteger la API (3 min)

En `GestorIncidencias.Web/Controllers/Api/IncidenciasApiController.cs` (ya tiene los `using` necesarios), añade `[Authorize(Policy = Politicas.GestionarIncidencias)]` a las acciones `Iniciar`, `Resolver`, `Cerrar`, `Reabrir` y `CambiarCategoria`:

```csharp
    // POST /api/incidencias/5/resolver
    [HttpPost("{id:int}/resolver")]
    [Authorize(Policy = Politicas.GestionarIncidencias)]
    public async Task<ActionResult<IncidenciaDto>> Resolver(int id, CancellationToken ct) =>
        Responder(await servicio.ResolverAsync(id, ct));
```

Fíjate: la clase ya tiene `[Authorize(AuthenticationSchemes = Esquemas.Token)]`. Los dos atributos **se combinan**: el usuario se identifica con el **token** (clase) y tiene que cumplir la **política** (acción).

## Paso 5 — Proteger la Razor Page (4 min)

En Razor Pages, `[Authorize]` en un método *handler* **no tiene efecto** (capítulo 4.4). Se comprueba a mano con `IAuthorizationService`.

En `GestorIncidencias.Web/Pages/Paginas/Incidencias/Index.cshtml.cs`:

1. Añade los `using`:

```csharp
using GestorIncidencias.Web.Seguridad;
using Microsoft.AspNetCore.Authorization;
```

2. Pide `IAuthorizationService` en el constructor:

```csharp
public class IndexModel(IIncidenciaService servicio, IAuthorizationService autorizacion) : PageModel
```

3. Al principio de `OnPostResolverAsync`, añade la comprobación:

```csharp
    public async Task<IActionResult> OnPostResolverAsync(int id, CancellationToken ct)
    {
        // En Razor Pages, [Authorize] en un MÉTODO handler no tiene efecto: se comprueba a mano.
        if (!(await autorizacion.AuthorizeAsync(User, Politicas.GestionarIncidencias)).Succeeded)
            return Forbid();   // 403 → la cookie lo convierte en una redirección a /Cuenta/AccesoDenegado

        var resultado = await servicio.ResolverAsync(id, ct);
        // ... (lo demás no cambia)
```

✅ **Comprueba:** `dotnet build` compila sin errores.

## Paso 6 — Ocultar lo que no se puede usar (6 min)

### 6a. Los `using` de las vistas

En `GestorIncidencias.Web/Views/_ViewImports.cshtml`, añade al final de los `@using`:

```cshtml
@using GestorIncidencias.Web.Seguridad
@using Microsoft.AspNetCore.Authorization
```

Y lo mismo en `GestorIncidencias.Web/Pages/_ViewImports.cshtml`.

### 6b. La ficha (`Views/Incidencias/Detalle.cshtml`)

1. Al principio, inyecta el servicio y calcula el permiso **una vez**:

```cshtml
@model IncidenciaDetalleDto
@inject IAuthorizationService Autorizacion
@{
    var incidencia = Model.Incidencia;
    var puedeGestionar = (await Autorizacion.AuthorizeAsync(User, Politicas.GestionarIncidencias)).Succeeded;
    ViewData["Title"] = $"Incidencia {incidencia.Id}";
}
```

2. En la categoría, muestra solo el nombre si la incidencia está cerrada **o** el usuario no puede gestionar:

```cshtml
        @if (incidencia.Estado == EstadoIncidencia.Cerrada || !puedeGestionar)
```

3. Envuelve **todo** el `<div class="acciones">...</div>` de los botones en un `@if`:

```cshtml
@if (puedeGestionar)
{
<div class="acciones">
    ... (los formularios Iniciar, Resolver, Cerrar y Reabrir, sin cambios)
</div>
}
```

### 6c. El listado de Razor Pages (`Pages/Paginas/Incidencias/Index.cshtml`)

1. Al principio (debajo de `@page`, que debe seguir siendo la primera línea):

```cshtml
@page
@model IndexModel
@inject IAuthorizationService Autorizacion
@{
    var puedeGestionar = (await Autorizacion.AuthorizeAsync(User, Politicas.GestionarIncidencias)).Succeeded;
    ViewData["Title"] = "Incidencias (Razor Pages)";
    var pagina = Model.Resultado;
}
```

2. En la condición del botón **Resolver** de cada fila:

```cshtml
                    @if (puedeGestionar && incidencia.Estado is (EstadoIncidencia.Abierta or EstadoIncidencia.EnCurso))
```

¿Por qué `IAuthorizationService` y no `User.IsInRole(Roles.Tecnico)`? Porque así la vista pregunta lo mismo que el controlador (**la política**). Si mañana la regla cambia (por ejemplo, "y además tener el claim `departamento=TIC`"), solo se toca Program.cs.

## Paso 7 — Probar (6 min)

Ejecuta la aplicación y prueba estos casos:

| # | Usuario | Acción | Resultado esperado |
|---|---|---|---|
| 1 | luis | Abre `/Incidencias/Detalle/1` | Sin botones; la categoría como texto. **Sí** puede comentar |
| 2 | luis | Abre `/Paginas/Incidencias` | Sin botones **Resolver** |
| 3 | luis | Crea una incidencia nueva | ✅ Se crea (crear no requiere ser técnico) |
| 4 | ana | Abre `/Incidencias/Detalle/1` y pulsa **Iniciar** | ✅ "Incidencia iniciada." |
| 5 | admin | Abre una incidencia abierta | ✅ Ve los botones (rol Administrador) |
| 6 | luis | En el `.http`, pide el token **de Luis** y ejecuta `POST /api/incidencias/3/resolver` | **403 Forbidden** |
| 7 | ana | Lo mismo con el token de Ana | **200 OK** |

> **¿Y si Luis "fuerza" el POST?** Ocultar el botón no lo impide: cualquiera puede enviar `POST /Incidencias/Resolver/2` desde las herramientas del navegador o con `curl` (con su cookie y un token antiforgery válido, que obtiene de cualquier formulario de la aplicación). Lo que lo impide es el `[Authorize(Policy = ...)]` de la acción: acabaría en `/Cuenta/AccesoDenegado`. **Ocultar es comodidad; proteger es el atributo.** La prueba de integración del [Lab 3](lab-03-pruebas.md) lo comprueba por la API.

## Errores típicos

| Síntoma | Causa probable | Solución |
|---|---|---|
| `CS0103 The name 'Politicas' does not exist` | Falta `using GestorIncidencias.Web.Seguridad;` (o en `_ViewImports.cshtml` para las vistas) | Pasos 3, 5 y 6a |
| `CS0103 The name 'Roles' does not exist` (en Program.cs) | Falta `using GestorIncidencias.Application.Seguridad;` | Paso 2 |
| `CS0246 'IAuthorizationService' could not be found` | Falta `using Microsoft.AspNetCore.Authorization;` | Pasos 5 y 6a |
| Al arrancar o al pulsar un botón: `InvalidOperationException: The AuthorizationPolicy named: 'GestionarIncidencias' was not found` | El atributo se usa, pero la política no está registrada | Paso 2 |
| Luis sigue pudiendo resolver desde `/Paginas/Incidencias` | Se puso `[Authorize]` en `OnPostResolverAsync` (no tiene efecto) | Paso 5: comprobación con `IAuthorizationService` |
| Ana tampoco puede gestionar (va a AccesoDenegado) | `RequireRole("Técnico")` con tilde, o un nombre distinto del rol creado | Usar las constantes `Roles.Tecnico` y `Roles.Administrador` |
| Luis no puede ver el listado (va a AccesoDenegado) | El atributo se puso en la **clase** del controlador | Paso 3: solo en las cinco acciones |
| La API devuelve 401 en lugar de 403 con el token de Luis | El token ha caducado o no se ha enviado la cabecera | Pedir un token nuevo |

## Ampliación opcional

1. **Política con claims:** cambia la política para que, además del rol, exija `RequireClaim(TiposClaim.NombreCompleto)`. ¿Qué tienes que tocar para que siga funcionando? (Solo Program.cs.)
2. **Autorización por recurso:** "un usuario sin rol puede **cerrar** las incidencias que ha creado él". ¿Qué falta en el modelo para poder implementarlo? (Pista: quién la creó. Ver *Autorización basada en recursos* en las referencias del capítulo 4.)

## Resumen: qué has tocado

| Capa | Fichero | Cambio |
|---|---|---|
| Domain / Application / Infrastructure | — | **Nada**: quién puede hacer qué es una decisión de la capa de entrada |
| Web | `Seguridad/Politicas.cs` | Nombre de la política |
| Web | `Program.cs` | `AddPolicy(...RequireRole(Tecnico, Administrador))` |
| Web | `IncidenciasController.cs`, `IncidenciasApiController.cs` | `[Authorize(Policy = ...)]` en las acciones de gestión |
| Web | `Pages/.../Index.cshtml.cs` | `IAuthorizationService` + `Forbid()` en el *handler* |
| Web | `_ViewImports.cshtml`, `Detalle.cshtml`, `Pages/.../Index.cshtml` | Ocultar botones con la misma política |

## Referencias

> Enlaces comprobados el 8 de octubre de 2026.

- [Autorización basada en directivas](https://learn.microsoft.com/es-es/aspnet/core/security/authorization/policies?view=aspnetcore-10.0)
- [Autorización basada en roles](https://learn.microsoft.com/es-es/aspnet/core/security/authorization/roles?view=aspnetcore-10.0)
- [Convenciones de autorización de Razor Pages](https://learn.microsoft.com/es-es/aspnet/core/razor-pages/security/authorization/conventions?view=aspnetcore-10.0)
