# 1. Método: migrar una pantalla de principio a fin

Hoy no hay temario nuevo: hay que **aplicar** lo de los cuatro días anteriores a dos aplicaciones reales. Para no improvisar con cada pantalla, usamos siempre la misma receta. Es la que seguiremos en SIREI (Lab 1) y en Noticom (Lab 2), y la que podéis llevaros para el resto de pantallas.

> La unidad de trabajo de una migración incremental es **la pantalla** (o el caso de uso), no la capa. Migrar "primero todo el acceso a datos, luego todos los controladores..." retrasa meses el momento de tener algo útil en producción.

## 1.1 La receta en ocho pasos

| # | Paso | Pregunta que responde | Resultado | Día del curso |
|---|---|---|---|---|
| 1 | **Leer el legacy** | ¿Qué hace la pantalla, de verdad? (no lo que dice la documentación) | Lista de operaciones, reglas y 🚩 | Día 3 (cap. 7) |
| 2 | **Contrato de URLs** | ¿Qué URL y qué verbo tiene cada operación? ¿Qué URLs antiguas hay que conservar? | Tabla `GET/POST → acción` | Día 4 (cap. 1) |
| 3 | **Dominio** | ¿Qué reglas de negocio hay escondidas en eventos, vistas o JavaScript? | Entidad con métodos que devuelven `Resultado` + pruebas unitarias | Día 2 |
| 4 | **Casos de uso y puertos** | ¿Qué necesita leer y escribir cada operación? | `IXxxService`, `IXxxRepository`, `IXxxConsultas`, DTO | Día 2 y 3 |
| 5 | **Infraestructura** | ¿Cómo se mapea el esquema EXISTENTE? | `DbContext` + configuraciones (`ToTable`, `HasColumnName`), sin tocar la BD | Día 3 |
| 6 | **Controlador** | ¿Cómo se traduce cada evento a una acción? | Controlador fino: enlazar → caso de uso → vista o redirección | Día 2 y 4 |
| 7 | **Vista** | ¿Qué controles pasan a qué HTML? ¿Qué JavaScript sobra? | Vistas Razor con Tag Helpers; JS en `wwwroot/js`, sin Razor | Día 2 y 4 |
| 8 | **Transversal y comparación** | ¿Está protegida? ¿Deja rastro? ¿Hace lo mismo que la vieja? | Autorización, logs, pruebas de integración, comparación lado a lado | Día 4 |

El orden 3 → 7 (de dentro hacia fuera) no es obligatorio, pero ayuda: cuando llegas al controlador, las reglas ya están probadas y el controlador sale casi solo.

## 1.2 Paso 1 — Leer el legacy con una plantilla

Para cada pantalla, rellenad esta ficha (es la del Lab 3 del día 3, reducida a una pantalla):

| Apartado | Qué anotar |
|---|---|
| **Operaciones** | Cada evento (`Page_Load`, `xxx_Click`, `RowCommand`...) o acción (`ActionResult`) y qué hace en una frase |
| **Entradas** | Query string, campos del formulario, `Session`, `ViewState`, `sessionStorage`, cookies |
| **Datos** | Tablas y columnas que lee y escribe; consultas (¿concatenadas? ¿`SELECT *`?); procedimientos |
| **Reglas** | Toda condición que pueda impedir una operación: en el *code-behind*, en el *markup* (`Visible=`), en el JavaScript, en SQL |
| **Seguridad** | Quién puede entrar y quién puede hacer cada operación; ¿se comprueba en el servidor? |
| **Dependencias de `System.Web`** | `Session`, `HttpContext.Current`, `Server.MapPath`, `ConfigurationManager`... |
| **🚩** | Las señales de alarma del [día 3, cap. 7.6](../day-03/07-analisis-legacy.md#76-señales-de-alarma-en-un-análisis) |

> **Truco:** las reglas que solo están en la interfaz ("el botón no se ve si...") son las más peligrosas. En la versión nueva **tienen que estar en el servidor**: cualquiera puede enviar un POST sin pasar por la pantalla.

## 1.3 Paso 2 — El contrato de URLs

Antes de escribir código, una tabla:

| Operación legacy | Verbo + URL nueva | Acción | Respuesta |
|---|---|---|---|
| `BuscarExpedientes.aspx` (carga y búsqueda) | `GET /Sirei/Expedientes?texto=&estado=&pagina=` | `Index` | Vista |
| `ExpedienteDetalle.aspx?id=5` | `GET /Sirei/Expedientes/Detalle/5` | `Detalle(id)` | Vista / 404 |
| `btnGuardar_Click` | `POST /Sirei/Expedientes/Detalle/5` | `Detalle(id, formulario)` | 302 (PRG) / vista con errores |
| `gvTramites_RowCommand` | `POST /Sirei/Expedientes/CompletarTramite/5?tramiteId=12` | `CompletarTramite` | 302 |
| URL antigua `ExpedienteDetalle.aspx?id=5` | `GET` | `DetalleLegacy` | **301** a la nueva |

Reglas: **leer = GET** (enlazable, cacheable, "atrás" funciona); **cambiar = POST** con antiforgery; el estado de la pantalla (filtros, página) **en la URL**; el identificador **en la ruta**.

### Las URLs antiguas no se rompen

Los usuarios tienen marcadores, hay correos con enlaces y otras aplicaciones enlazan a `ExpedienteDetalle.aspx?id=5`. Dos opciones:

```csharp
// 1) Una acción que redirige (la que usamos: se ve en el controlador y se prueba fácil)
[HttpGet("/ExpedienteDetalle.aspx")]
[AllowAnonymous]
public IActionResult DetalleLegacy(int id) => RedirectToActionPermanent(nameof(Detalle), new { id });

// 2) El middleware de reescritura, para muchas reglas parecidas (Microsoft.AspNetCore.Rewrite)
app.UseRewriter(new RewriteOptions()
    .AddRedirect(@"^Expedientes/Buscar\.aspx$", "Sirei/Expedientes", StatusCodes.Status301MovedPermanently));
```

Mientras convivan las dos aplicaciones (migración incremental con YARP, día 3), esto no hace falta: las rutas no migradas siguen yendo al legacy. Se añade **cuando la pantalla vieja se apaga**.

## 1.4 Pasos 3 a 5 — De dentro hacia fuera

El patrón es exactamente el de `Incidencia`, que lleváis viendo cuatro días:

```
 Regla en el legacy                                    Dónde va
 ───────────────────────────────────────────────       ────────────────────────────────────────────
 if (ddlEstado.SelectedValue == "4" && pendientes)  →  Expediente.Actualizar(...) → Resultado.Fallo(...)
 btnGuardar.Visible = estado != 4                   →  Expediente: "un expediente cerrado no admite cambios"
                                                       + @if en la vista (comodidad, no seguridad)
 lote.IdEstado = 2 (sin comprobar nada)             →  Lote.Validar(): solo si está pendiente
 "existe el lote" / "la categoría existe"           →  caso de uso (necesita la BD)
 "solo los técnicos validan"                        →  [Authorize(Policy = ...)] en el controlador
```

### Mapear un esquema que ya existe

La base de datos **no es nuestra**: la sigue usando la aplicación vieja. El modelo nuevo se adapta a ella desde la configuración de EF Core, sin ensuciar el dominio:

```csharp
builder.ToTable("Expedientes");
builder.Property(e => e.Estado).HasColumnName("IdEstado");   // enum con valores fijos 1..4
builder.HasMany(e => e.Tramites).WithOne().HasForeignKey("IdExpediente");   // clave ajena sombra
builder.Property(e => e.Version).IsConcurrencyToken();
```

| Cuidado con | Por qué |
|---|---|
| **Migraciones de EF Core** | No se generan sobre una BD compartida. Si hace falta una columna (p. ej. `Version`), se pacta y se añade con un script, y el legacy la ignora |
| **Enums** | Fijad los valores (`Cerrado = 4`). Reordenar un enum cambiaría el significado de los datos guardados |
| **Fechas** | El legacy suele guardar hora local (`DateTime.Now`); la aplicación nueva, UTC. Decidid una conversión y aplicadla en un solo sitio (`ValueConverter`) |
| **Nulos y longitudes** | Comparad la configuración con el esquema real (`sp_help 'Expedientes'`). Un `IsRequired()` que en la BD es `NULL` rompe al leer filas antiguas |
| **Datos "imposibles"** | Años de datos traen estados que el código nuevo considera inválidos. Las reglas validan los **cambios**, no impiden **leer** lo que ya hay |

## 1.5 Pasos 6 y 7 — Controlador y vista

| Legacy | ASP.NET Core |
|---|---|
| Un `.aspx` o una vista con un `TipoEjecucion` que cambia todo | Una acción por operación; el "modo" de pantalla como **enum**, no como string mágico |
| Datos de la pantalla en `ViewState` / `Session` / `sessionStorage` | Ruta, query string y formulario; lo demás se vuelve a leer |
| `lblError.Text = ...` | `ModelState.AddModelError` + `return View(...)`, o `TempData` + redirección |
| AJAX que hace POST para leer | GET a una acción que devuelve una **vista parcial** |
| JavaScript inline con Razor dentro | Fichero en `wwwroot/js`, datos en `data-*` o en un `<script type="application/json">` (skill *razor-js-config*) |
| Columnas ocultas por JavaScript | La vista decide qué pinta a partir del ViewModel |

Cada aplicación migrada va en su propia **área** (`Areas/Sirei`, `Areas/Noticom`): sus controladores, vistas y URLs (`/Sirei/...`) quedan agrupados y no se mezclan con lo demás.

## 1.6 Paso 8 — "Definición de terminado" de una pantalla migrada

Una pantalla no está migrada porque "se ve". Está migrada cuando:

- [ ] Hace **lo mismo** que la vieja con los mismos datos (comparadas lado a lado, con casos normales y con los raros).
- [ ] Las reglas de negocio están en el **dominio** y tienen **pruebas unitarias**.
- [ ] Ninguna regla depende solo de la interfaz: lo oculto también está **prohibido en el servidor**.
- [ ] **Autorización** declarada (`FallbackPolicy` + políticas) y probada con un 403.
- [ ] Todos los POST tienen **antiforgery**; nada cambia datos con GET.
- [ ] Sin SQL concatenado, sin `SELECT *`, con **paginación** en la base de datos.
- [ ] Sin JavaScript ni estilos **inline** (la CSP no se queja en la consola).
- [ ] Las operaciones relevantes dejan **log** estructurado, sin datos sensibles.
- [ ] Hay al menos una **prueba de integración** que recorre la pantalla.
- [ ] Las **URLs antiguas** redirigen (si la vieja se apaga).

## Referencias

> Enlaces comprobados el 9 de octubre de 2026.

- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0) — Guía oficial.
- [Áreas en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/mvc/controllers/areas?view=aspnetcore-10.0)
- [Middleware de reescritura de URL](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/url-rewriting?view=aspnetcore-10.0)
- [Control de conflictos de simultaneidad (EF Core)](https://learn.microsoft.com/es-es/ef/core/saving/concurrency)
- [Ingeniería inversa / bases de datos existentes (EF Core)](https://learn.microsoft.com/es-es/ef/core/managing-schemas/scaffolding/) — `dotnet ef dbcontext scaffold` genera un primer mapeo a partir del esquema real.
