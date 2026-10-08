---
name: razor-js-config
description: Extrae las expresiones Razor (@Model, @ViewBag, @Url, @Html...) incrustadas en bloques <script> de vistas .cshtml y las sustituye por una configuración JSON o atributos data-*, dejando el JavaScript libre de Razor y movible a un .js externo compatible con CSP. Usar al migrar o revisar vistas legacy (MVC 5 o ASP.NET Core) que mezclan Razor y JavaScript, cuando se pida "sacar el JS de la vista", "quitar Razor del script", "localizar variables Razor en el script" o preparar una vista para una CSP sin scripts inline.
---

# Razor fuera del JavaScript

Convención acordada para el traspaso: **el JavaScript nunca contiene Razor**. Los valores que vienen del servidor se publican en el HTML como datos y el JS los lee desde ahí.

## Regla de decisión

| Qué necesita el JS | Mecanismo |
|---|---|
| Uno o dos valores **primitivos** (string, número, bool) ligados a un elemento concreto | Atributos `data-*` en ese elemento |
| Cualquier objeto **compuesto** (listas, objetos anidados, fechas, nulos con significado) o más de dos valores | Bloque `<script type="application/json">` serializado con `Json.Serialize` |

Ante la duda, usar el bloque JSON: sirve para todos los casos.

## Procedimiento

1. **Inventariar.** En cada `<script>` de la vista, localizar todas las expresiones Razor: `@Model.*`, `@ViewBag.*`, `@ViewData[...]`, `@TempData[...]`, `@Url.*`, `@Html.*`, `@(...)`, `@{ ... }` y `@if`/`@foreach` que generen JS. Ignorar las que estén fuera del `<script>`. Distinguir las variables JS globales (p. ej. `urlpagina`), que no son Razor.
2. **Reportar** al usuario una tabla con: línea del script, expresión Razor, propiedad del modelo y tipo (primitivo/compuesto). Señalar además los defectos de cada uso (ver "Defectos típicos").
3. **Publicar los datos** en la vista según la regla de decisión. Exponer solo lo que el JS usa, mediante un objeto anónimo o un ViewModel específico; nunca el modelo entero.
4. **Reescribir el JS** para leer de la configuración y quitar todo Razor. Si la vista lo permite, mover el script a `wwwroot/js/<area>/<vista>.js` y referenciarlo con `<script src="~/js/..." asp-append-version="true"></script>`.
5. **Corregir** los defectos detectados en el paso 2 (comillas, número de argumentos, etc.) y avisar de cada cambio de comportamiento.
6. **Verificar** con la checklist final.

## Patrón: bloque JSON (objetos compuestos)

Vista (ASP.NET Core):

```cshtml
<script type="application/json" id="lotes-config">
    @Json.Serialize(new
    {
        Model.TipoEjecucion,
        Model.Proceso,
        Model.Ejercicio,
        Model.Estados,
        Filtro = new { Model.Pago, Model.Tipo }
    })
</script>
```

JavaScript:

```js
var config = JSON.parse(document.getElementById('lotes-config').textContent);
var tipoEjecucion = config.tipoEjecucion;
```

Por qué es la opción por defecto:
- `type="application/json"` no se ejecuta, así que la CSP no lo bloquea.
- `Json.Serialize` (System.Text.Json) escapa `<`, `>`, `&` y `'` (`<`...). Un valor con `</script>` no puede romper el bloque.
- `Json.Serialize` devuelve `IHtmlContent`: no usar `Html.Raw` encima ni volver a codificarlo.
- Los tipos se conservan: números, bool, `null`, arrays y objetos anidados.

Id del bloque: `<vista>-config` en kebab-case. Si hay varios por página, un id por bloque.

## Patrón: atributos data-* (primitivos)

```cshtml
<div id="GridviewLotes"
     data-tipo-ejecucion="@Model.TipoEjecucion"
     data-proceso="@Model.Proceso"></div>
```

```js
var $grid = $('#GridviewLotes');
var tipoEjecucion = $grid.data('tipo-ejecucion');
```

Aquí Razor codifica para atributo HTML, que es el contexto correcto. Hay que tener en cuenta que jQuery `.data()` convierte tipos (`"007"` → `7`, `"true"` → `true`). Si el valor debe seguir siendo string, usar `.attr('data-proceso')`.

## Defectos típicos que hay que señalar y corregir

- **Valor sin comillas** (`consultaLotes(@Model.Proceso, ...)`): con null o vacío genera `consultaLotes(, ...)`, un error de sintaxis que rompe todo el script. Con un string no numérico produce un `ReferenceError`.
- **Codificación HTML dentro de JS** (`'@Model.X'`): un `'` llega como `&#39;` literal, y un `\` o un salto de línea rompen el string.
- **Número de argumentos** distinto al de la firma de la función que recibe los valores (parámetros que quedan `undefined`).
- **Razor que genera código** (`@if` dentro del script): sustituirlo por un valor de configuración y un `if` en JS.
- **Datos sensibles** expuestos al serializar el modelo completo.

## Detalles de serialización

- **camelCase**: `Json.Serialize` usa las `JsonOptions` de MVC, con camelCase por defecto (`TipoEjecucion` → `tipoEjecucion`). Comprobar la configuración en `Program.cs` (`AddJsonOptions`) antes de escribir los nombres en el JS.
- **Fechas**: llegan como string ISO 8601. Si el JS necesita un objeto fecha, convertirlo con `new Date(config.fecha)`.
- **Enums**: llegan como número salvo que esté registrado `JsonStringEnumConverter`.
- **Proyecto ASP.NET MVC 5 (legacy)**: no existe `Json.Serialize`. Usar `@Html.Raw(JsonConvert.SerializeObject(obj, new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeHtml }))`. Sin `EscapeHtml` se pierde la protección frente a `</script>`.

## Helper opcional (si se repite en muchas vistas)

```csharp
public static class JsonConfigHtmlExtensions
{
    public static IHtmlContent JsonConfig(this IHtmlHelper html, string id, object value) =>
        new HtmlString(
            $"<script type=\"application/json\" id=\"{HtmlEncoder.Default.Encode(id)}\">" +
            JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)) +
            "</script>");
}
```

Uso: `@Html.JsonConfig("lotes-config", new { Model.TipoEjecucion, Model.Estados })`. Proponerlo; no crearlo sin confirmación del usuario.

## Checklist final

- [ ] Ningún `<script>` ejecutable de la vista contiene `@`.
- [ ] Todo valor compuesto viaja por bloque JSON; los `data-*` solo llevan primitivos.
- [ ] Solo se exponen las propiedades que el JS usa.
- [ ] Los nombres en el JS coinciden con la política de nombres JSON del proyecto.
- [ ] Las llamadas reescritas pasan el número correcto de argumentos.
- [ ] Si el JS se movió a un `.js` externo, la vista lo referencia y la página carga sin errores de consola ni violaciones de CSP.
- [ ] El usuario ha recibido el resumen de cambios, incluidos los de comportamiento.
