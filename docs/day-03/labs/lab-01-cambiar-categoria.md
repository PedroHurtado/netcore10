# Lab 1 — Cambiar la categoría de una incidencia

**Duración:** 35 min · **Teoría relacionada:** [01 — Configuración](../01-ef-core-configuracion.md), [02 — Relaciones](../02-relaciones.md), [03 — Migraciones](../03-migraciones.md)

## Objetivo

Hoy una incidencia recibe su categoría al crearse y ya no se puede cambiar. Vamos a permitir **asignar, cambiar o quitar la categoría desde la ficha**, trabajando con la relación N:1 `Incidencia → Categoria` y atravesando todas las capas, como en el lab 2 del día 2.

> **Reglas:**
> - La categoría elegida tiene que **existir** (regla de **aplicación**: hace falta consultar la base de datos).
> - Una incidencia **cerrada** no admite cambios (regla de **dominio**: la decide la propia entidad).
> - Elegir "(sin categoría)" deja `CategoriaId = null` (la relación es **opcional**).

Todo el código está en este documento: **cópialo**, pero lee los comentarios. Trabaja sobre una copia de `src/day-03`.

## Paso 0 — Preparar tu copia (3 min)

Copia la carpeta `src/day-03` a tu carpeta de trabajo y ejecútala:

```bash
cd mi-copia-day-03
dotnet run --project GestorIncidencias.Web
```

✅ **Comprueba:** el panel de http://localhost:5196 dice **204 incidencias en total** y muestra la tabla **Por categoría**. Abre `/Incidencias/Detalle/1`: la categoría es "Hardware" y hay un comentario.

Para la aplicación con `Ctrl+C`.

## Paso 1 — Domain: la regla (5 min)

En `GestorIncidencias.Domain/Incidencias/Incidencia.cs`, añade este método **encima de `AgregarComentario`**:

```csharp
    /// <summary>Asigna, cambia o quita (null) la categoría. Una incidencia cerrada no admite cambios.</summary>
    public Resultado CambiarCategoria(int? categoriaId)
    {
        if (Estado == EstadoIncidencia.Cerrada)
            return Resultado.Fallo("No se puede cambiar la categoría de una incidencia cerrada.", TipoError.Conflicto);

        CategoriaId = categoriaId;
        return Resultado.Ok();
    }
```

✅ **Comprueba:** `dotnet build GestorIncidencias.Domain` compila sin errores.

**Piensa:** el método cambia `CategoriaId`, pero no toca la navegación `Categoria`. ¿Por qué es suficiente?

<details>
<summary>Respuesta</summary>

En una relación, **lo que se guarda es la clave ajena**. Al hacer `SaveChanges`, EF Core actualiza la columna `CategoriaId` y listo. La navegación `Categoria` solo sirve para **leer** datos de la categoría, y de eso se encargan las consultas con proyección (`i.Categoria.Nombre`).

</details>

## Paso 2 — Application: el caso de uso (7 min)

### 2a. La interfaz

En `GestorIncidencias.Application/Incidencias/IIncidenciaService.cs`, añade **encima de `ComentarAsync`**:

```csharp
    Task<Resultado<IncidenciaDto>> CambiarCategoriaAsync(int id, int? categoriaId, CancellationToken ct = default);
```

### 2b. La implementación

En `GestorIncidencias.Application/Incidencias/IncidenciaService.cs`, añade **encima de `ComentarAsync`**:

```csharp
    public async Task<Resultado<IncidenciaDto>> CambiarCategoriaAsync(int id, int? categoriaId, CancellationToken ct = default)
    {
        // Regla de APLICACIÓN: la categoría tiene que existir (igual que en CrearAsync).
        if (categoriaId is { } cid && !await repositorio.ExisteCategoriaAsync(cid, ct))
            return Resultado<IncidenciaDto>.Fallo($"No existe la categoría {cid}.");

        // Regla de DOMINIO (no si está cerrada): la aplica la entidad.
        return await ModificarAsync(id, incidencia => incidencia.CambiarCategoria(categoriaId), ct);
    }
```

`ModificarAsync` ya hace el resto: cargar la incidencia con el repositorio, pedirle el cambio, guardar y leer el DTO actualizado (que trae el **nombre** de la nueva categoría gracias a la proyección).

✅ **Comprueba:** `dotnet build GestorIncidencias.Application` compila sin errores.

**Piensa:** ¿por qué la comprobación de que la categoría existe está aquí y no en `Incidencia.CambiarCategoria`?

<details>
<summary>Respuesta</summary>

Porque para saberlo hay que **consultar la base de datos**, y el dominio no conoce ni el repositorio ni EF Core. Es la misma decisión que tomamos en `CrearAsync` ([capítulo 2.7](../02-relaciones.md#27-lo-que-vemos-en-la-aplicación)).

</details>

## Paso 3 — Web: la acción del controlador (6 min)

Abre `GestorIncidencias.Web/Controllers/IncidenciasController.cs`.

### 3a. El desplegable necesita saber qué categoría marcar

Sustituye el método `CargarCategoriasAsync` (al final del fichero) por esta versión, que admite la categoría **seleccionada**:

```csharp
    private async Task CargarCategoriasAsync(CancellationToken ct, int? seleccionada = null) =>
        ViewBag.Categorias = new SelectList(await servicio.ListarCategoriasAsync(ct), nameof(CategoriaDto.Id), nameof(CategoriaDto.Nombre), seleccionada);
```

Las llamadas que ya existían (`await CargarCategoriasAsync(ct);`) siguen compilando: el parámetro nuevo es opcional.

### 3b. La ficha carga las categorías

En la acción `Detalle`, añade una línea **antes** de `return View(detalle);`:

```csharp
        await CargarCategoriasAsync(ct, seleccionada: detalle.Incidencia.CategoriaId);
        return View(detalle);
```

### 3c. La acción POST

Añade **encima de la acción `Comentar`**:

```csharp
    // POST /Incidencias/CambiarCategoria/5   (campo categoriaId del desplegable; vacío = sin categoría)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarCategoria(int id, int? categoriaId, CancellationToken ct) =>
        TrasCambio(id, await servicio.CambiarCategoriaAsync(id, categoriaId, ct), "Categoría actualizada.");
```

Fíjate en `int? categoriaId`: si el usuario elige "(sin categoría)", el formulario envía `categoriaId=` (vacío) y el *model binding* lo convierte en `null`.

✅ **Comprueba:** `dotnet build` (toda la solución) compila sin errores.

## Paso 4 — Web: el formulario en la ficha (5 min)

En `GestorIncidencias.Web/Views/Incidencias/Detalle.cshtml`, busca:

```cshtml
    <dt>Categoría</dt>
    <dd>@(incidencia.Categoria ?? "—")</dd>
```

y sustitúyelo por:

```cshtml
    <dt>Categoría</dt>
    <dd>
        @if (incidencia.Estado == EstadoIncidencia.Cerrada)
        {
            @(incidencia.Categoria ?? "—")
        }
        else
        {
            <form asp-action="CambiarCategoria" asp-route-id="@incidencia.Id" method="post" class="filtro">
                <select name="categoriaId" asp-items="ViewBag.Categorias">
                    <option value="">(sin categoría)</option>
                </select>
                <button type="submit" class="boton-pequeno">Cambiar</button>
            </form>
        }
    </dd>
```

## Paso 5 — Probar (6 min)

Ejecuta la aplicación y prueba estos casos:

| # | Acción | Resultado esperado |
|---|---|---|
| 1 | Abre `/Incidencias/Detalle/1` | El desplegable muestra **Hardware** seleccionada |
| 2 | Elige **Otros** y pulsa **Cambiar** | Aviso verde "Categoría actualizada." y "Otros" seleccionada |
| 3 | Vuelve a `/Incidencias` | La incidencia 1 aparece con categoría **Otros** |
| 4 | Vuelve al panel de inicio | En **Por categoría**, Hardware tiene un pendiente menos y Otros uno más |
| 5 | En la ficha 1, elige **(sin categoría)** y pulsa **Cambiar** | Categoría vacía; en el panel aparece la fila **(sin categoría)** |
| 6 | Abre una incidencia **cerrada** (por ejemplo, la 5) | No hay desplegable: solo el nombre de la categoría |

> **Para pensar:** ¿qué pasa si alguien envía a mano un `POST /Incidencias/CambiarCategoria/1` con `categoriaId=99`? ¿Y si lo envía para una incidencia cerrada? **La vista oculta; la aplicación y el dominio protegen.** Lo verás con el aviso rojo correspondiente.

## Paso 6 — ¿Y con una base de datos real? (3 min, para pensar)

Con SQL Server y migraciones (capítulo 3), ¿habría que crear una migración después de este laboratorio?

<details>
<summary>Respuesta</summary>

**No.** No hemos cambiado el **modelo de EF Core**: ni entidades nuevas, ni propiedades nuevas, ni configuración. `CategoriaId` ya existía como columna; solo hemos añadido **comportamiento** (un método) y pantallas. Las migraciones dependen del modelo, no de la lógica.

¿Cuándo sí haría falta? Si hubiéramos añadido, por ejemplo, una propiedad `FechaCambioCategoria` a `Incidencia`: entonces `dotnet ef migrations add AnadirFechaCambioCategoria` generaría un `AddColumn`.

</details>

## Errores típicos

| Síntoma | Causa probable | Solución |
|---|---|---|
| `CS0535 ... does not implement interface member 'CambiarCategoriaAsync'` | Falta el paso 2b | Añadir el método en `IncidenciaService` |
| `CS1061 'Incidencia' does not contain a definition for 'CambiarCategoria'` | Falta el paso 1, o el método está fuera de la clase | Revisar las llaves `{ }` en `Incidencia.cs` |
| `CS1739 ... does not have a parameter named 'seleccionada'` | No se ha sustituido `CargarCategoriasAsync` (paso 3a) | Paso 3a |
| El desplegable de la ficha solo muestra "(sin categoría)" | Falta la llamada a `CargarCategoriasAsync` en `Detalle` (paso 3b) | Paso 3b |
| El desplegable no marca la categoría actual | Se llamó sin `seleccionada:` | Paso 3b |
| Al pulsar **Cambiar**: **404** | La acción no se llama `CambiarCategoria` o falta `[HttpPost]` | Paso 3c |
| Al pulsar **Cambiar**: **400 Bad Request** | El `<form>` no tiene `method="post"` (sin token antiforgery) | Paso 4 |
| Se elige "(sin categoría)" y no cambia | El parámetro de la acción es `int` en lugar de `int?` | Paso 3c |

## Ampliación opcional: desde la API

En `IncidenciasApiController`, añade:

```csharp
    // PUT /api/incidencias/5/categoria   (cuerpo: 3, o null para quitarla)
    [HttpPut("{id:int}/categoria")]
    public async Task<ActionResult<IncidenciaDto>> CambiarCategoria(int id, [FromBody] int? categoriaId, CancellationToken ct) =>
        Responder(await servicio.CambiarCategoriaAsync(id, categoriaId, ct));
```

Pruébalo con `PUT /api/incidencias/1/categoria`, `Content-Type: application/json` y cuerpo `3`, `null` o `99`.

**Observa:** no hace falta tocar Domain ni Application. Con `99` (categoría inexistente) obtendrás un 400 con el mismo mensaje que en la interfaz web.

## Resumen: qué has tocado

| Capa | Fichero | Cambio |
|---|---|---|
| Domain | `Incidencia.cs` | `CambiarCategoria`: regla "no si está cerrada" y asignación de la clave ajena |
| Application | `IIncidenciaService.cs`, `IncidenciaService.cs` | Caso de uso con la regla "la categoría existe" |
| Infrastructure | — | **Nada** (el repositorio ya tenía `ExisteCategoriaAsync`) |
| Web | `IncidenciasController.cs` | Desplegable con selección, acción POST |
| Web | `Detalle.cshtml` | Formulario con el desplegable |
| Base de datos | — | **Nada**: el modelo no ha cambiado, no habría migración |

## Referencias

> Enlaces comprobados el 7 de octubre de 2026.

- [Relaciones de uno a muchos](https://learn.microsoft.com/es-es/ef/core/modeling/relationships/one-to-many) — Relaciones opcionales y claves ajenas.
- [Tag Helpers en formularios](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/working-with-forms?view=aspnetcore-10.0) — `<select>` con `asp-items`.
