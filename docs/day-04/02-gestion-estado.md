# 2. Gestión del estado: Session, ViewState y alternativas modernas

HTTP no tiene memoria: cada petición llega sola. Web Forms escondía ese hecho con **ViewState** y animaba a usar **Session** para casi todo. En ASP.NET Core **ViewState no existe** y **Session** existe pero funciona distinto. Este capítulo explica dónde guardar cada cosa.

## 2.1 El inventario de "estados" de Web Forms

| Mecanismo Web Forms | Dónde vive | Ámbito | En ASP.NET Core |
|---|---|---|---|
| **ViewState** | Campo oculto `__VIEWSTATE` en cada página (base64, a veces cientos de KB) | Una página, entre *postbacks* | **No existe**. Ruta, query string, campos ocultos o volver a leer |
| **ControlState** | Dentro del ViewState | Un control | No existe |
| **Session** | Memoria del servidor (InProc), StateServer o SQL Server | Un usuario, entre páginas | **ISession**: hay que activarla; solo guarda bytes/texto |
| **Application** | Memoria del servidor | Toda la aplicación | Servicio **Singleton** o **IOptions** (configuración) |
| **Cache** | Memoria del servidor | Toda la aplicación, con caducidad | **IMemoryCache**, **HybridCache**, **IDistributedCache** |
| **Cookies** | Navegador | Un usuario | Igual: `Request.Cookies` / `Response.Cookies.Append` |
| **QueryString** | URL | Una petición | Igual, y con **model binding** (`?estado=Abierta` → parámetro `estado`) |
| **HiddenField** | Formulario | Una petición (POST) | `<input type="hidden" asp-for="...">` |
| **Context.Items** | Memoria, durante la petición | Una petición | `HttpContext.Items` (mejor: un servicio *Scoped*) |
| `Server.Transfer` + `PreviousPage` | — | — | No existe |
| — | Cookie cifrada | Hasta la siguiente petición | **TempData** (mensajes tras una redirección, día 2) |

## 2.2 Adiós a ViewState: qué hacer con lo que guardaba

ViewState guardaba automáticamente **todo**: el texto de los `TextBox`, el contenido del `GridView`, la página actual, el elemento seleccionado... En ASP.NET Core hay que preguntarse, para cada dato, **¿de dónde lo saco en la siguiente petición?**

| Lo que guardaba ViewState | Alternativa | En el proyecto |
|---|---|---|
| El **id** del registro que se está editando (`ViewState["IdExpediente"]`) | En la **ruta**: `/Incidencias/Detalle/5` | `Detalle(int id)` |
| Filtros y página actual de un listado | En la **query string** (GET): `?estado=Abierta&pagina=2`. Además, la URL se puede guardar en favoritos y compartir | `Index(EstadoIncidencia? estado, int pagina)` |
| El contenido de un `GridView` (para no volver a consultar) | **Volver a consultar**, paginado y con proyección (día 3). Es más barato que subir y bajar el ViewState en cada clic | `ListarAsync` |
| Los valores del formulario tras un error | El propio **POST** los trae: `return View(formulario)` | `Crear(IncidenciaFormulario formulario)` |
| Opciones de un desplegable | Volver a cargarlas (y, si cambian poco, **caché**: apartado 2.5) | `CargarCategoriasAsync` |
| Pasos de un asistente (*wizard*) | Campos ocultos con lo acumulado, o guardar un **borrador** en la base de datos. Sesión solo si es pequeño | — |
| Un mensaje para la siguiente pantalla | **TempData** + Post-Redirect-Get | `TempData["Mensaje"]` |

> **Mito:** "sin ViewState hay que consultar más la base de datos". En la práctica, una consulta paginada con proyección (día 3) es mucho más ligera que enviar y recibir 200 KB de ViewState en **cada** clic, que además hay que deserializar y validar.

> **Seguridad:** el ViewState viajaba al navegador y volvía. Sin `EnableViewStateMac` (o con la clave de máquina filtrada), se podía manipular. Hoy la regla es simple: **lo que llega del navegador no es de fiar**. El `id` de la ruta se comprueba en el servidor (¿existe?, ¿puede este usuario tocarlo?).

## 2.3 Session en ASP.NET Core

### Cómo se activa

En [Program.cs](../../src/day-04/GestorIncidencias.Web/Program.cs):

```csharp
builder.Services.AddDistributedMemoryCache();   // DÓNDE se guarda: memoria de este servidor
builder.Services.AddSession(o =>
{
    o.Cookie.Name = ".GestorIncidencias.Sesion";
    o.Cookie.HttpOnly = true;                   // JavaScript no puede leer la cookie
    o.Cookie.IsEssential = true;
    o.IdleTimeout = TimeSpan.FromMinutes(20);   // como <sessionState timeout="20"> en Web.config
});
// ...
app.UseSession();                               // después de UseRouting/UseAuthorization, antes de los endpoints
```

### Diferencias con `Session["..."]` de Web Forms

| | Web Forms | ASP.NET Core |
|---|---|---|
| Activación | Activada por defecto | **Hay que activarla** (`AddSession` + `UseSession`) |
| Qué guarda | Cualquier objeto (`Session["Ds"] = dataSet`) | **Bytes o texto**: los objetos se **serializan** (JSON) |
| Dónde | InProc / StateServer / SQL Server (`Web.config`) | Cualquier `IDistributedCache`: memoria, **Redis**, **SQL Server** |
| Acceso concurrente | **Bloqueo**: dos peticiones del mismo usuario con sesión de escritura se ejecutan una detrás de otra | **Sin bloqueo**: la última que escribe gana |
| Identificador | Cookie `ASP.NET_SessionId` (o en la URL, *cookieless*) | Cookie (nunca en la URL) |
| Relación con el inicio de sesión | Ninguna | Ninguna: **hay que vaciarla al cerrar sesión** |

### El ejemplo del proyecto: "Vistas recientemente"

[Estado/HistorialVisitas.cs](../../src/day-04/GestorIncidencias.Web/Estado/HistorialVisitas.cs) guarda en la sesión las últimas 5 incidencias abiertas:

```csharp
public static void RegistrarVisita(this ISession sesion, int id, string titulo)
{
    var visitas = sesion.ObtenerVisitas()
        .Where(v => v.Id != id)
        .Prepend(new VisitaReciente(id, titulo))
        .Take(Maximo)
        .ToList();

    sesion.SetString(Clave, JsonSerializer.Serialize(visitas));   // a JSON: la sesión solo guarda texto o bytes
}
```

El controlador lo usa así:

```csharp
// Detalle: registrar la visita
HttpContext.Session.RegistrarVisita(id, detalle.Incidencia.Titulo);

// Index: leer el historial y pasarlo al ViewModel
var recientes = HttpContext.Session.ObtenerVisitas();
```

Y al **cerrar sesión** ([CuentaController.Logout](../../src/day-04/GestorIncidencias.Web/Controllers/CuentaController.cs)) se vacía: `HttpContext.Session.Clear()`. Si no, el siguiente usuario de ese navegador vería el historial del anterior.

Es un buen uso de la sesión: **poco**, **por usuario** y **prescindible** (si se pierde, la aplicación sigue funcionando).

### Cuándo NO usar la sesión

- 🚩 Para guardar **resultados de consultas** o un `DataSet` entero "para no volver a consultar" (lo vimos en SIREI).
- 🚩 Para pasar datos **entre dos pantallas** que se podrían pasar por la URL.
- 🚩 Para cosas que **no pueden perderse**: con `AddDistributedMemoryCache`, un reinicio de la aplicación (o de IIS) borra todas las sesiones.
- 🚩 Con **varios servidores** detrás de un balanceador y la sesión en memoria: cada petición puede ir a un servidor distinto. O sesión distribuida (Redis, SQL Server) o "afinidad" en el balanceador.

### Durante la convivencia con SIREI

Si se migra por partes (*Strangler Fig*, día 3), la aplicación nueva puede necesitar leer la `Session` de la vieja. Para eso existen los **System.Web adapters** con **sesión remota**: la aplicación ASP.NET Core pide los valores a la aplicación .NET Framework a través de una API interna. Es una solución **de transición**: a medida que se migran pantallas, la sesión compartida debería ir desapareciendo.

## 2.4 `Application[...]` y `HttpContext.Current`

| Web Forms | ASP.NET Core |
|---|---|
| `Application["NombreAplicacion"]` (configuración) | `IOptions<IncidenciasOptions>` (día 1) |
| `Application["Contador"]` con `Application.Lock()` | Un servicio **Singleton** con su propio control de concurrencia (`Interlocked`, `lock`) |
| `HttpContext.Current` en cualquier capa | Nada estático. En controladores y vistas: `HttpContext`. Fuera de la web: **una interfaz** (`IUsuarioActual`) cuya implementación web usa `IHttpContextAccessor` |

`HttpContext.Current` fue durante años la forma de "llegar a todo desde cualquier sitio", también desde la capa de negocio. Es lo que hacía imposible probar ese código. En el proyecto, el **único** sitio que lo usa (a través de `IHttpContextAccessor`) es [Seguridad/UsuarioActualHttp.cs](../../src/day-04/GestorIncidencias.Web/Seguridad/UsuarioActualHttp.cs).

## 2.5 `Cache[...]`: caché de datos

| Opción | Dónde guarda | Cuándo |
|---|---|---|
| `IMemoryCache` | Memoria de este servidor | Un solo servidor, datos que se pueden perder |
| `IDistributedCache` | Redis, SQL Server... | Varios servidores; solo bytes |
| **`HybridCache`** | **L1** en memoria + **L2** distribuida (opcional) | **La recomendada hoy** para casos nuevos: API sencilla, protección contra estampidas, serialización incluida |
| Output Cache (día 2) | Memoria del servidor | Páginas HTML completas |

En el proyecto, el **catálogo de categorías** se pide en cada formulario y casi nunca cambia: es el candidato perfecto. En [EfIncidenciaConsultas.cs](../../src/day-04/GestorIncidencias.Infrastructure/Persistencia/EfIncidenciaConsultas.cs):

```csharp
public async Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct = default) =>
    await cache.GetOrCreateAsync(
        "categorias",                                   // clave
        async token => await db.Categorias              // qué hacer si NO está en caché
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaDto(c.Id, c.Nombre))
            .ToListAsync(token),
        OpcionesCacheCategorias,                        // caduca a los 10 minutos
        cancellationToken: ct);
```

Equivale a este patrón de Web Forms, pero sin sus problemas:

```csharp
// Web Forms
var categorias = (DataTable)Cache["Categorias"];
if (categorias == null)                         // si 50 usuarios llegan a la vez, ¡50 consultas!
{
    categorias = CargarCategorias();
    Cache.Insert("Categorias", categorias, null, DateTime.Now.AddMinutes(10), Cache.NoSlidingExpiration);
}
```

| | `Cache["..."]` | `HybridCache` |
|---|---|---|
| 50 peticiones simultáneas con la caché vacía | 50 consultas a la BD (**estampida**) | **1** consulta; las demás esperan el resultado |
| Varios servidores | Cada uno con su copia (sin forma de invalidar en todos) | L2 compartida (si se registra Redis/SQL Server) |
| Tipado | `object` + *cast* | Genérico |

> Lo hemos puesto en **Infrastructure** (dentro del adaptador de consultas): Application y Web no saben que hay caché. Si mañana se quita, solo cambia ese fichero.
>
> **Ojo con la caducidad:** si se añadiera una pantalla para editar categorías, habría que invalidar la entrada al guardar (`cache.RemoveAsync("categorias")`), igual que el día 2 invalidábamos el Output Cache tras cada POST.

## 2.6 Resumen: ¿dónde guardo esto?

```
¿Lo necesita solo la SIGUIENTE petición tras una redirección?   → TempData
¿Identifica QUÉ se está viendo (id, filtros, página)?           → Ruta / query string
¿Es lo que el usuario está escribiendo en un formulario?        → El propio formulario (POST)
¿Es del USUARIO, pequeño y se puede perder?                     → Session
¿Es del usuario y NO se puede perder (borrador, preferencias)?  → Base de datos
¿Es igual para TODOS y cambia poco (catálogos)?                 → HybridCache
¿Es configuración?                                              → IOptions
```

## Referencias

> Enlaces comprobados el 8 de octubre de 2026.

- [Administración de sesiones y estados](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/app-state?view=aspnetcore-10.0) — Session, TempData, cookies, query string, campos ocultos y `HttpContext.Items`.
- [Biblioteca HybridCache](https://learn.microsoft.com/es-es/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0)
- [Almacenamiento en caché en memoria](https://learn.microsoft.com/es-es/aspnet/core/performance/caching/memory?view=aspnetcore-10.0) y [caché distribuida](https://learn.microsoft.com/es-es/aspnet/core/performance/caching/distributed?view=aspnetcore-10.0)
- [Acceso a HttpContext](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/http-context?view=aspnetcore-10.0) — `IHttpContextAccessor` y por qué no guardar el `HttpContext`.
- [Migración del estado de sesión](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/areas/session?view=aspnetcore-10.0) — Sesión remota compartida durante la migración incremental.
