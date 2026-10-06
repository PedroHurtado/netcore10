# 5. Vistas Razor

La vista es la "V" de MVC: una plantilla `.cshtml` que mezcla **HTML** y **C#** para generar la página. Las vistas del proyecto están en [Views/](../../src/day-02/GestorIncidencias.Web/Views/).

## 5.1 Dónde busca MVC las vistas

Cuando una acción hace `return View(modelo)`, MVC busca, por este orden:

```
Views/{Controlador}/{Acción}.cshtml      → Views/Incidencias/Detalle.cshtml
Views/Shared/{Acción}.cshtml             → Views/Shared/Detalle.cshtml
```

Estructura del proyecto:

```
Views/
├── _ViewImports.cshtml     ← usings y Tag Helpers para TODAS las vistas
├── _ViewStart.cshtml       ← código que se ejecuta antes de cada vista (fija el layout)
├── Shared/
│   ├── _Layout.cshtml      ← plantilla común (cabecera, menú, pie)
│   ├── _Mensajes.cshtml    ← parcial: mensajes de TempData
│   ├── _Estado.cshtml      ← parcial: etiqueta de color del estado
│   └── Error.cshtml
├── Home/
│   └── Index.cshtml
└── Incidencias/
    ├── Index.cshtml        ← listado con filtro
    ├── Detalle.cshtml      ← ficha + botones de acción
    └── Crear.cshtml        ← formulario
```

Los ficheros que empiezan por `_` son, por convención, **no navegables**: layouts, parciales y ficheros de configuración.

## 5.2 Sintaxis Razor en 5 minutos

El carácter `@` pasa de HTML a C#. Razor detecta solo dónde termina el código.

```cshtml
@* Esto es un comentario de Razor: no llega al navegador *@

<h1>@Model.Titulo</h1>                          @* expresión: escribe el valor *@
<p>Alta: @Model.FechaAlta.ToString("dd/MM/yyyy")</p>
<p>Total: @(Model.Precio * 1.21m) €</p>        @* expresión compleja: entre paréntesis *@

@{
    var esCritica = Model.Prioridad == Prioridad.Critica;   // bloque de código
}

@if (esCritica)
{
    <strong class="prioridad-critica">¡Crítica!</strong>
}

@foreach (var incidencia in Model.Incidencias)
{
    <tr><td>@incidencia.Id</td><td>@incidencia.Titulo</td></tr>
}
```

| Sintaxis | Uso |
|---|---|
| `@expresion` | Escribe un valor |
| `@(expresión compleja)` | Cuando hay operadores o espacios |
| `@{ ... }` | Bloque de código C# |
| `@if`, `@foreach`, `@switch` | Control de flujo con HTML dentro |
| `@* ... *@` | Comentario |
| `@model Tipo` | Tipo del modelo de la vista |
| `@using`, `@inject` | Importar espacios de nombres, inyectar servicios |

### Seguridad: Razor codifica todo lo que escribe

```cshtml
<td>@incidencia.Titulo</td>
```

Si alguien crea una incidencia con título `<script>alert('hack')</script>`, Razor escribe `&lt;script&gt;...`, que el navegador muestra como texto. **Protección frente a XSS por defecto.** La única forma de escribir HTML sin codificar es `@Html.Raw(...)`: no la uséis con datos que vengan del usuario.

> En Web Forms, `<%= %>` **no** codificaba (había que usar `<%: %>` o `Server.HtmlEncode`). En Razor es al revés: seguro por defecto.

## 5.3 Vistas fuertemente tipadas y ViewModels

```cshtml
@model ListadoIncidenciasViewModel
```

Con `@model`, la propiedad `Model` tiene tipo: IntelliSense en el editor y **errores de compilación** si escribimos mal una propiedad.

Un **ViewModel** es una clase pensada para **una vista concreta**, con exactamente lo que necesita:

```csharp
// Models/ViewModels.cs
public record ListadoIncidenciasViewModel(
    IReadOnlyList<IncidenciaDto> Incidencias,   // los datos
    EstadoIncidencia? FiltroEstado);            // el filtro seleccionado (para marcarlo en el <select>)

public record PanelViewModel(string NombreAplicacion, int Total, int Abiertas, int EnCurso, int CriticasPendientes);
```

| Qué pasar a la vista | ¿Recomendado? | Motivo |
|---|---|---|
| La entidad del dominio (`Incidencia`) | ❌ | La vista podría usar métodos de negocio; acopla la interfaz al dominio |
| El DTO del caso de uso (`IncidenciaDto`) | ✅ para vistas de solo lectura sencillas | Ya es una copia "plana" de los datos (lo hacemos en `Detalle`) |
| Un ViewModel propio | ✅ cuando la vista necesita más cosas (filtros, listas para desplegables, totales) | Una vista = un modelo a medida |
| `ViewData["X"]` / `ViewBag.X` | ⚠️ solo para datos menores (el título de la página) | Sin tipos: los errores se ven en ejecución |

## 5.4 Layout, _ViewStart y _ViewImports

### Layout: la página maestra

[_Layout.cshtml](../../src/day-02/GestorIncidencias.Web/Views/Shared/_Layout.cshtml) define la estructura común. Cada vista se inserta en `@RenderBody()`:

```cshtml
<!DOCTYPE html>
<html lang="es">
<head>
    <title>@ViewData["Title"] · Gestor de Incidencias</title>
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
</head>
<body>
    <header><nav> ... menú ... </nav></header>
    <main>
        <partial name="_Mensajes" />
        @RenderBody()                                           @* ← aquí va cada vista *@
    </main>
    @await RenderSectionAsync("Scripts", required: false)      @* ← sección opcional *@
</body>
</html>
```

Cada vista fija el título:

```cshtml
@{
    ViewData["Title"] = "Incidencias";
}
```

### _ViewStart y _ViewImports

```cshtml
@* Views/_ViewStart.cshtml — se ejecuta antes de cada vista *@
@{ Layout = "_Layout"; }
```

```cshtml
@* Views/_ViewImports.cshtml — se aplica a todas las vistas de la carpeta y subcarpetas *@
@using GestorIncidencias.Web.Models
@using GestorIncidencias.Application.Incidencias
@using GestorIncidencias.Domain.Incidencias
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

| Web Forms | Razor |
|---|---|
| `Site.Master` | `_Layout.cshtml` |
| `<asp:ContentPlaceHolder ID="MainContent">` | `@RenderBody()` |
| Otros `ContentPlaceHolder` | `@RenderSection("Nombre")` / `@section Nombre { }` |
| `MasterPageFile="~/Site.Master"` en cada página | `_ViewStart.cshtml` (una vez para todas) |
| `<%@ Import Namespace="..." %>` / `web.config` `<namespaces>` | `_ViewImports.cshtml` |

## 5.5 Vistas parciales: reutilizar fragmentos

Una parcial es un trozo de vista reutilizable. La etiqueta de estado aparece en el listado, en el detalle y en las Razor Pages:

```cshtml
@* Views/Shared/_Estado.cshtml *@
@model EstadoIncidencia
<span class="etiqueta estado-@Model.ToString().ToLowerInvariant()">@Model</span>
```

```cshtml
@* Uso desde cualquier vista *@
<td><partial name="_Estado" model="incidencia.Estado" /></td>
```

> Equivale a un **User Control** (`.ascx`) de Web Forms, pero sin ciclo de vida ni eventos: solo genera HTML. Para fragmentos con lógica propia (consultar datos, por ejemplo) existen los **View Components**, que veremos si hay tiempo el día 5.

## 5.6 Tag Helpers: HTML que entiende de rutas y modelos

Los Tag Helpers son atributos `asp-*` que el servidor procesa al generar el HTML. Parecen HTML normal (los diseñadores pueden trabajar con ellos), pero conocen las rutas y el modelo.

### Enlaces

```cshtml
<a asp-controller="Incidencias" asp-action="Detalle" asp-route-id="3">Ver</a>
```

Genera:

```html
<a href="/Incidencias/Detalle/3">Ver</a>
```

Si mañana cambia la plantilla de rutas, **todos los enlaces se actualizan solos**. Dentro del mismo controlador basta con `asp-action`. Para Razor Pages se usa `asp-page="/Paginas/Incidencias/Index"`.

### Formularios

```cshtml
<form asp-action="Crear" method="post">
    <label asp-for="Titulo"></label>
    <input asp-for="Titulo" />
    <span asp-validation-for="Titulo"></span>
</form>
```

Genera (simplificado):

```html
<form action="/Incidencias/Crear" method="post">
    <label for="Titulo">Título</label>                                 ← texto de [Display(Name)]
    <input type="text" id="Titulo" name="Titulo" value=""
           data-val="true"
           data-val-required="El título es obligatorio."
           data-val-length="El título debe tener entre 5 y 120 caracteres."
           data-val-length-min="5" data-val-length-max="120" />        ← reglas de [Required], [StringLength]
    <span class="field-validation-valid" data-valmsg-for="Titulo"></span>
    <input name="__RequestVerificationToken" type="hidden" value="CfDJ8..." />   ← antiforgery automático
</form>
```

Un solo `asp-for` genera: `id`, `name` (para que el model binding lo encuentre), `value` (para repintar lo que escribió el usuario si hay errores), `type` según el tipo de la propiedad y los atributos de validación.

### Tabla de Tag Helpers más usados

| Tag Helper | Para qué |
|---|---|
| `<a asp-controller asp-action asp-route-*>` | Enlaces a acciones MVC |
| `<a asp-page asp-page-handler>` | Enlaces a Razor Pages |
| `<form asp-action>` / `<form asp-page>` | Formularios (+ token antiforgery) |
| `<input asp-for>` | Campo enlazado a una propiedad |
| `<textarea asp-for>` | Texto largo |
| `<select asp-for asp-items>` | Desplegable |
| `<label asp-for>` | Etiqueta con el `[Display(Name)]` |
| `<span asp-validation-for>` | Error de un campo |
| `<div asp-validation-summary="ModelOnly">` | Errores generales (los de `AddModelError(string.Empty, ...)`) |
| `<partial name model>` | Vista parcial |
| `<link asp-append-version>` / `<script asp-append-version>` | Añade una huella del fichero para evitar cachés antiguas |
| `<environment include="Development">` | Contenido solo para un entorno |

### Desplegables a partir de un enum

```cshtml
<select asp-for="Prioridad" asp-items="Html.GetEnumSelectList<Prioridad>()">
    <option value="">(la de la configuración)</option>
</select>
```

`Html.GetEnumSelectList<T>()` crea una opción por valor del enum. La opción vacía permite dejar la prioridad sin elegir (`Prioridad?` → `null`).

## 5.7 Recorrido completo: el formulario "Nueva incidencia"

[Crear.cshtml](../../src/day-02/GestorIncidencias.Web/Views/Incidencias/Crear.cshtml):

```cshtml
@model IncidenciaFormulario
@{
    ViewData["Title"] = "Nueva incidencia";
}

<h1>Nueva incidencia</h1>

<form asp-action="Crear" method="post" class="formulario">
    <div asp-validation-summary="ModelOnly" class="errores"></div>

    <div class="campo">
        <label asp-for="Titulo"></label>
        <input asp-for="Titulo" autofocus />
        <span asp-validation-for="Titulo" class="error-campo"></span>
    </div>

    <div class="campo">
        <label asp-for="Descripcion"></label>
        <textarea asp-for="Descripcion" rows="4"></textarea>
        <span asp-validation-for="Descripcion" class="error-campo"></span>
    </div>

    <div class="campo">
        <label asp-for="Prioridad"></label>
        <select asp-for="Prioridad" asp-items="Html.GetEnumSelectList<Prioridad>()">
            <option value="">(la de la configuración)</option>
        </select>
    </div>

    <div class="acciones">
        <button type="submit" class="boton">Crear</button>
        <a asp-action="Index">Cancelar</a>
    </div>
</form>
```

Qué ocurre en cada escenario:

| Escenario | Qué hace el controlador | Qué ve el usuario |
|---|---|---|
| Primera visita (`GET`) | `View(new IncidenciaFormulario())` | Formulario vacío |
| Título de 2 letras (`POST`) | `ModelState.IsValid == false` → `View(formulario)` | El formulario **con lo que había escrito** y el error bajo el título |
| Límite de abiertas alcanzado (`POST`) | `AddModelError(string.Empty, ...)` → `View(formulario)` | Error general arriba del formulario |
| Todo correcto (`POST`) | `TempData` + `RedirectToAction(Detalle)` | La ficha de la nueva incidencia con un aviso verde |

### Validación en el navegador (opcional)

Los atributos `data-val-*` permiten validar **antes** de enviar el formulario, añadiendo las librerías `jquery.validate` y `jquery.validate.unobtrusive` (la plantilla `dotnet new mvc` las incluye en `_ValidationScriptsPartial.cshtml`). En el proyecto del curso **no** las usamos para no depender de JavaScript: la validación del servidor es la que manda siempre, porque la del navegador se puede saltar.

## 5.8 De controles Web Forms a Razor

| Web Forms | Razor / MVC |
|---|---|
| `<asp:Label ID="lblTitulo" runat="server" />` + `lblTitulo.Text = ...` | `@Model.Titulo` |
| `<asp:TextBox ID="txtTitulo" runat="server" />` | `<input asp-for="Titulo" />` |
| `<asp:TextBox TextMode="MultiLine" />` | `<textarea asp-for="Descripcion"></textarea>` |
| `<asp:DropDownList DataSource=... />` | `<select asp-for="Prioridad" asp-items="..."></select>` |
| `<asp:GridView>` / `<asp:Repeater>` | `<table>` + `@foreach` |
| `<asp:HyperLink NavigateUrl="~/Detalle.aspx?id=3">` | `<a asp-action="Detalle" asp-route-id="3">` |
| `<asp:Button OnClick="btnGuardar_Click">` | `<button type="submit">` dentro de `<form asp-action="...">` |
| `<asp:RequiredFieldValidator>` | `[Required]` en el ViewModel + `<span asp-validation-for>` |
| `<asp:ValidationSummary>` | `<div asp-validation-summary="...">` |
| `<asp:Panel Visible="false">` | `@if (condicion) { ... }` |
| User Control (`.ascx`) | Vista parcial o View Component |
| Master Page | Layout |

La diferencia de fondo: en Web Forms los controles **guardan estado** (ViewState) y **disparan eventos**. En Razor la vista **solo genera HTML**; el estado viaja en el formulario o la URL, y los "eventos" son peticiones HTTP a acciones.

## 5.9 Errores frecuentes

| Error | Causa | Solución |
|---|---|---|
| `InvalidOperationException: The view 'Detalle' was not found` | Nombre de fichero o carpeta incorrectos | Comprobar `Views/{Controlador}/{Acción}.cshtml` |
| Los `asp-*` aparecen tal cual en el HTML | Falta `@addTagHelper` | Revisar `_ViewImports.cshtml` |
| `NullReferenceException` en `@Model.X` | La acción hace `return View()` sin modelo | `return View(modelo)` |
| `The model item passed into the ViewDataDictionary is of type A, but this ViewDataDictionary instance requires B` | El `@model` de la vista no coincide con lo que pasa la acción | Alinear tipos |
| El formulario no se enlaza (propiedades vacías en el POST) | `name` del campo distinto de la propiedad (por escribir el `<input>` a mano) | Usar `asp-for` |
| 400 Bad Request al enviar el formulario | Falta el token antiforgery (formulario escrito sin Tag Helper, o `<form>` sin `method="post"`) | Usar `<form asp-action ... method="post">` |

## Preguntas de repaso

1. ¿Qué genera `<a asp-action="Detalle" asp-route-id="5">` y por qué es mejor que escribir `href="/Incidencias/Detalle/5"`?
2. Si el usuario escribe `<b>hola</b>` como título, ¿cómo se ve en el listado? ¿Por qué?
3. ¿Qué diferencia hay entre `asp-validation-summary="ModelOnly"` y `"All"`?
4. ¿Por qué el formulario vuelve a mostrar lo que el usuario había escrito cuando hay un error?
5. ¿Dónde pondrías un fragmento de HTML que se repite en tres vistas?

## Referencias

> Enlaces comprobados el 6 de octubre de 2026.

- [Vistas en ASP.NET Core MVC](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/overview?view=aspnetcore-10.0) — Ubicación de vistas, ViewModels, `ViewData` y `ViewBag`.
- [Referencia de sintaxis de Razor](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/razor?view=aspnetcore-10.0) — Toda la sintaxis, incluida la codificación HTML.
- [Diseño (layout) en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/layout?view=aspnetcore-10.0) — `_Layout`, secciones, `_ViewStart` y `_ViewImports`.
- [Vistas parciales](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/partial?view=aspnetcore-10.0)
- [Tag Helpers en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/tag-helpers/intro?view=aspnetcore-10.0)
- [Tag Helpers en formularios](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/working-with-forms?view=aspnetcore-10.0) — `asp-for`, `select`, validación y `GetEnumSelectList`.
- [Tag Helper de delimitador (`<a>`)](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/tag-helpers/built-in/anchor-tag-helper?view=aspnetcore-10.0)
- [Prevención de scripts de sitios (XSS)](https://learn.microsoft.com/es-es/aspnet/core/security/cross-site-scripting?view=aspnetcore-10.0) — Por qué Razor codifica por defecto.
