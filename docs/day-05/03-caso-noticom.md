# 3. Caso práctico Noticom: de ASP.NET MVC 5 a ASP.NET Core MVC

**Legacy:** [legacy/noticom](legacy/noticom/) (y la vista real en [examples/prueba.txt](../../examples/prueba.txt)) · **Solución:** [src/day-05 → Areas/Noticom](../../src/day-05/GestorIncidencias.Web/Areas/Noticom/) · **Laboratorio:** [Lab 2](labs/lab-02-noticom-lotes.md)

Noticom gestiona **lotes de notificaciones**: se generan por proceso y ejercicio, se **validan** y se agrupan en **remesas** para enviarlas. Migramos la pantalla de lotes, que en MVC 5 son **cuatro pantallas en una** según el parámetro `t` (`TipoEjecucion`): consulta (`c`), validación (`v`), crear remesa (`cr`) y borrado (`b`).

Al ser MVC 5, buena parte es **mecánica** (capítulo 3 del día 4: `using`, tipos de retorno, `Html.Partial`...). Lo interesante está en otra parte: en una vista con **mucho JavaScript** que hace lo que debería hacer el servidor.

## 3.1 Lo que encontramos al leer el legacy

| Pieza | 🚩 | Decisión |
|---|---|---|
| `TipoEjecucion` = `"c"`, `"v"`, `"cr"`, `"b"` comparado en vista, JS y servicio | String mágico en tres capas | `enum ModoLotes` en la capa Web (es un concepto de **pantalla**) |
| Cuatro `if` para el `<h1>` | Lógica de presentación repetida | `LotesViewModel.Titulo` |
| `_BusquedaLotes` + `_BusquedaAvanzadaLotes` con campos duplicados | El JS copia valores de uno a otro | **Un** formulario GET; lo avanzado en un `<details>` |
| `$.ajax({ type: 'POST', url: '/Lote/ConsultaLotes' })` para **leer** | No enlazable, no cacheable | `GET /Noticom/Lotes/Tabla?...` → vista parcial |
| `sessionStorage["FiltroLotes"]` | La búsqueda no está en la URL | Query string + `history.replaceState` |
| `ocultarColumnaGridview(12, 'gridLotes')` tras cada AJAX | Columnas **por número**: añadir una descoloca todo | El ViewModel decide qué columnas se pintan |
| `crearRemesa()` lee los Id de `td:nth-child(2)` y monta un JSON | Depende del orden de las columnas | Casillas `name="LoteIds"` + `List<int>` por *model binding* |
| `result.indexOf("_Logon_")` en una respuesta 200 | Detección frágil de sesión caducada | **401** con `X-Requested-With` |
| `var tipoEjecucion = '@Model.TipoEjecucion'` en el `<script>` | Razor en el JS; CSP | `data-*` + `wwwroot/js/noticom/lotes.js` |
| `onclick="borrarLote(12)"`, `style="display:none"` | Inline: la CSP los bloquea | Delegación de eventos; clases CSS |
| `Validar`, `Borrar` sin `[ValidateAntiForgeryToken]` ni rol | CSRF; cualquiera valida | Antiforgery global (día 4) + `[Authorize(Policy/Roles)]` |
| `Borrar` no comprueba el estado | La regla la "garantiza" la columna oculta | `Lote.ComprobarBorrado()` |
| `CrearRemesa`: un `SaveChanges` por lote, sin comprobar estados | Remesas a medias; lotes remesados dos veces | `Remesa.Crear(...)` + un único `SaveChanges` |
| `db.Lotes.Include("Remesa").ToList()` y después `Where` | Toda la tabla a memoria | Filtros en el `IQueryable` + paginación |
| `new LoteService()` y `new NoticomEntities()` | Sin inyección; imposible de probar | `ILoteService` inyectado; `NoticomDbContext` Scoped |
| `return Json(new { Ok = true })` | PascalCase con Newtonsoft | Ya no hace falta (formularios + PRG). Si se mantuviera: **camelCase** en ASP.NET Core |

## 3.2 Recorrido por la solución

```
src/day-05/
├── GestorIncidencias.Domain/Lotes/
│   ├── Lote.cs                 ← Validar, ComprobarBorrado, PuedeRemesarse
│   ├── Remesa.cs               ← Remesa.Crear(nombre, lotes, ahora): todas las reglas de la remesa
│   └── EstadoLote.cs           ← PendienteValidacion, Validado, Remesado (+ TipoNotificacion)
├── GestorIncidencias.Application/Lotes/
│   ├── LoteService.cs          ← ValidarAsync, BorrarAsync, CrearRemesaAsync, ListarAsync
│   └── LoteDto.cs              ← LoteDto, RemesaDto, FiltroLotes, CrearRemesaComando
├── GestorIncidencias.Infrastructure/Noticom/   ← NoticomDbContext (el EDMX de EF6, ahora en API fluida)
└── GestorIncidencias.Web/
    ├── Areas/Noticom/
    │   ├── Controllers/LotesController.cs      ← Index, Tabla, Validar, Borrar, CrearRemesa, Remesas
    │   ├── Models/LotesViewModels.cs           ← ModoLotes, LotesViewModel (título y columnas)
    │   └── Views/Lotes/Index.cshtml, _TablaLotes.cshtml, Remesas.cshtml
    └── wwwroot/js/noticom/lotes.js             ← sin Razor, sin jQuery, mejora progresiva
```

### `TipoEjecucion` → `ModoLotes` + un ViewModel que ya lo sabe todo

```csharp
public record LotesViewModel(ModoLotes Modo, FiltroLotes Filtro, Pagina<LoteDto> Pagina, bool PuedeGestionar, bool PuedeBorrar, string UrlVolver)
{
    public string Titulo => Modo switch { ModoLotes.Validacion => "Lotes pendientes de validación", ... };

    public bool ColumnaSeleccion => Modo == ModoLotes.CrearRemesa && PuedeGestionar;
    public bool ColumnaValidar  => Modo == ModoLotes.Validacion && PuedeGestionar;
    public bool ColumnaBorrar   => Modo == ModoLotes.Borrado && PuedeBorrar;
    ...
}
```

La vista solo pregunta: `@if (Model.ColumnaValidar) { <th></th> }`. Cada pantalla pinta **sus** columnas; no hay nada que ocultar después.

¿Y el filtro por estado que el legacy aplicaba según la pantalla? Lo fija el controlador al construir el modelo (`Validacion` → solo pendientes, `CrearRemesa` → solo validados). El servicio no sabe nada de pantallas.

### AJAX bien hecho: la misma acción con y sin JavaScript

```
 Sin JavaScript                                 Con JavaScript (lotes.js)
 ───────────────────────────────────────        ──────────────────────────────────────────────
 <form method="get"> → GET /Noticom/Lotes?...   submit interceptado → fetch GET /Noticom/Lotes/Tabla?...
   Index → View (página completa)                 Tabla → PartialView("_TablaLotes") (solo la tabla)
                                                  history.replaceState → la URL refleja la búsqueda
```

Las dos acciones usan el **mismo** `CrearModeloAsync` y la **misma** vista parcial. Si el JavaScript falla, la aplicación sigue funcionando: es **mejora progresiva**.

### Sesión caducada en una petición AJAX

| | MVC 5 + jQuery | ASP.NET Core + `fetch` |
|---|---|---|
| Petición sin sesión | El servidor devuelve la vista `_Logon_` con **200** | La cookie de autenticación responde **401** si la petición lleva `X-Requested-With: XMLHttpRequest` |
| Cómo lo detecta el JS | Busca el texto `"_Logon_"` en el HTML | `if (respuesta.status === 401)` |
| Trampa | — | `fetch` **no** envía `X-Requested-With` (jQuery sí). Sin la cabecera, el servidor responde 302 → `fetch` sigue la redirección → recibe el HTML del **login** con 200 y lo pinta dentro de la tabla |

### Datos del servidor para el JavaScript, sin Razor en el script

```cshtml
<div id="lotes" data-url-tabla="@Url.Action("Tabla")" data-url-index="@Url.Action("Index")">
```

```javascript
const urlTabla = contenedor.dataset.urlTabla;   // antes: urlpagina + '/Lote/ConsultaLotes' y '@Model.TipoEjecucion'
```

Es la convención del proyecto (skill [razor-js-config](../../.claude/skills/razor-js-config/SKILL.md)): uno o dos valores simples → `data-*`; objetos compuestos → `<script type="application/json">` con `Json.Serialize`. El `.js` se sirve con huella en el nombre (`lotes.{hash}.js`, gracias a `MapStaticAssets` y `asp-append-version`) y la CSP no se queja.

### Crear remesa: un formulario normal

```cshtml
<td><input type="checkbox" name="LoteIds" value="@lote.Id" form="form-remesa" /></td>
...
<form asp-action="CrearRemesa" method="post" id="form-remesa" data-confirmar-remesa>
    <input name="Nombre" required /> <button type="submit">Crear remesa</button>
</form>
```

El atributo HTML `form="form-remesa"` asocia la casilla al formulario aunque esté en otra parte de la página. El navegador envía `LoteIds=3&LoteIds=7&Nombre=...` y el *model binding* lo convierte en `CrearRemesaFormulario`. El JavaScript solo pide confirmación; las reglas (nombre, al menos un lote, solo validados) están en `Remesa.Crear`.

## 3.3 Qué hemos dejado fuera

- **Pago** (columna y filtro "Pago" del legacy): no forma parte del modelo simplificado del curso.
- **Ver documentos** (`encriptarURL(...)`): el legacy "cifraba" la URL para que no se pudiera cambiar el id. En ASP.NET Core la protección correcta es **comprobar en el servidor** que el usuario puede ver ese lote (autorización por recurso), no ocultar el id.
- **Modales de Bootstrap** (`modalConfirmacion`): sustituidos por `confirm()` para no depender de Bootstrap. Con Bootstrap 5 se haría igual: un `.js` que escucha el `submit`, sin `onclick`.

## Referencias

> Enlaces comprobados el 9 de octubre de 2026.

- [Actualizar de ASP.NET MVC a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/tooling?view=aspnetcore-10.0)
- [Vistas parciales](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/partial?view=aspnetcore-10.0)
- [Portar de EF6 a EF Core](https://learn.microsoft.com/es-es/ef/efcore-and-ef6/porting/)
- [Uso de Fetch (MDN)](https://developer.mozilla.org/es/docs/Web/API/Fetch_API/Using_Fetch)
- [Atributo `form` de los controles (MDN)](https://developer.mozilla.org/es/docs/Web/HTML/Element/input#form)
- [Mejora progresiva (MDN)](https://developer.mozilla.org/es/docs/Glossary/Progressive_Enhancement)
