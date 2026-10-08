# Lab 1 — De Web Forms a MVC: la búsqueda de incidencias

**Duración:** 30 min · **Teoría relacionada:** [01 — De Web Forms a MVC](../01-webforms-a-mvc.md), [02 — Gestión del estado](../02-gestion-estado.md)

## Objetivo

Convertir una pantalla **Web Forms** típica (`TextBox`, `DropDownList` con `AutoPostBack`, `GridView` paginado, `ViewState` y `Session`) en su equivalente ASP.NET Core: añadir al listado de incidencias una **búsqueda por título** que funcione igual en la web y en la API, atravesando todas las capas.

1. **Analizar** la página legacy y decidir qué se hace con cada pieza (no se ejecuta).
2. **Implementar** el filtro por texto en Application e Infrastructure.
3. **Conectarlo** a la vista MVC y a la API.

Todo el código está en este documento: **cópialo**, pero lee los comentarios. Trabaja sobre una copia de `src/day-04`.

## Paso 0 — Preparar tu copia (3 min)

Copia la carpeta `src/day-04` a tu carpeta de trabajo y ejecútala:

```bash
cd mi-copia-day-04
dotnet run --project GestorIncidencias.Web
```

Abre http://localhost:5196/Incidencias. Te llevará al **login**: entra como `luis@demo.local` con la contraseña que aparece en `appsettings.Development.json` (`Incidencias:ClaveUsuariosDemo`).

✅ **Comprueba:** ves el listado paginado ("Página 1 de 11 · 204 incidencias"). Para la aplicación con `Ctrl+C`.

## Paso 1 — Analizar la página legacy (6 min)

Así es la búsqueda de incidencias en una aplicación Web Forms:

```aspx
<%-- BuscarIncidencias.aspx --%>
<asp:TextBox ID="txtTexto" runat="server" />
<asp:DropDownList ID="ddlEstado" runat="server" AutoPostBack="true"
                  OnSelectedIndexChanged="ddlEstado_SelectedIndexChanged">
    <asp:ListItem Value="" Text="(todos)" />
    <asp:ListItem Value="Abierta" /><asp:ListItem Value="EnCurso" />
    <asp:ListItem Value="Resuelta" /><asp:ListItem Value="Cerrada" />
</asp:DropDownList>
<asp:Button ID="btnBuscar" runat="server" Text="Buscar" OnClick="btnBuscar_Click" />

<asp:GridView ID="gvResultados" runat="server" AutoGenerateColumns="false"
              AllowPaging="true" PageSize="20" OnPageIndexChanging="gvResultados_PageIndexChanging">
    <Columns>
        <asp:BoundField DataField="Id" HeaderText="#" />
        <asp:HyperLinkField DataTextField="Titulo" HeaderText="Título"
                            DataNavigateUrlFields="Id" DataNavigateUrlFormatString="Detalle.aspx?id={0}" />
        <asp:BoundField DataField="Estado" HeaderText="Estado" />
    </Columns>
</asp:GridView>
<asp:Label ID="lblTotal" runat="server" />
```

```csharp
// BuscarIncidencias.aspx.cs
protected void Page_Load(object sender, EventArgs e)
{
    if (!IsPostBack)
    {
        if (Session["UltimaBusqueda"] is string texto)      // recordar la última búsqueda
            txtTexto.Text = texto;
        Buscar();
    }
}

protected void btnBuscar_Click(object sender, EventArgs e)
{
    gvResultados.PageIndex = 0;
    Session["UltimaBusqueda"] = txtTexto.Text;
    Buscar();
}

protected void ddlEstado_SelectedIndexChanged(object sender, EventArgs e)
{
    gvResultados.PageIndex = 0;
    Buscar();
}

protected void gvResultados_PageIndexChanging(object sender, GridViewPageEventArgs e)
{
    gvResultados.PageIndex = e.NewPageIndex;
    Buscar();
}

private void Buscar()
{
    var sql = "SELECT * FROM Incidencias WHERE Titulo LIKE '%" + txtTexto.Text + "%'";
    if (ddlEstado.SelectedValue != "")
        sql += " AND Estado = '" + ddlEstado.SelectedValue + "'";

    var tabla = new DataTable();
    using (var cn = new SqlConnection(ConfigurationManager.ConnectionStrings["BD"].ConnectionString))
        new SqlDataAdapter(sql, cn).Fill(tabla);

    gvResultados.DataSource = tabla;      // el GridView pagina EN MEMORIA: trae todas las filas
    gvResultados.DataBind();
    lblTotal.Text = tabla.Rows.Count + " incidencias";
}
```

Con tu compañero, completad la tabla: ¿qué es cada pieza en ASP.NET Core?

| Pieza legacy | ¿Qué hacemos? |
|---|---|
| `txtTexto`, `ddlEstado`, `btnBuscar` | |
| `AutoPostBack` + `ddlEstado_SelectedIndexChanged` | |
| `gvResultados` con `AllowPaging` y `PageIndexChanging` | |
| La página actual y los filtros (los guarda el ViewState) | |
| `Session["UltimaBusqueda"]` | |
| `"... LIKE '%" + txtTexto.Text + "%'"` | |
| `SELECT *` + `DataTable` + paginación del `GridView` | |
| `HyperLinkField` a `Detalle.aspx?id={0}` | |

<details>
<summary>Respuestas</summary>

| Pieza legacy | En ASP.NET Core |
|---|---|
| `txtTexto`, `ddlEstado`, `btnBuscar` | Un `<form method="get">` con un `<input name="texto">`, un `<select name="estado">` y un botón. El *model binding* los convierte en parámetros de la acción `Index` |
| `AutoPostBack` + `SelectedIndexChanged` | Desaparece: el usuario pulsa **Buscar** (si se quiere "al cambiar", un `.js` que envíe el formulario; nunca JavaScript *inline* por la CSP) |
| `GridView` paginado | `<table>` + `@foreach` + `Pagina<T>` (día 3) |
| Página y filtros en el ViewState | En la **query string**: `/Incidencias?texto=correo&estado=Abierta&pagina=2`. La URL se puede guardar y compartir |
| `Session["UltimaBusqueda"]` | **Sobra**: la búsqueda está en la URL (el navegador la recuerda en el historial). No se migra |
| Concatenación en el SQL | 🚩 **Inyección SQL**. Con LINQ, EF Core genera `LIKE` **parametrizado** |
| `SELECT *` + paginar en memoria | 🚩 Trae **todas** las filas en cada clic. Paginación en SQL con `Skip`/`Take` y proyección (ya lo hace `ListarAsync`) |
| `HyperLinkField` | `<a asp-action="Detalle" asp-route-id="@incidencia.Id">` (ya existe) |

</details>

## Paso 2 — Application: el filtro en los contratos (6 min)

### 2a. El puerto de lectura

En `GestorIncidencias.Application/Abstracciones/IIncidenciaConsultas.cs`, **sustituye** la línea de `ListarAsync` por:

```csharp
    Task<Pagina<IncidenciaDto>> ListarAsync(EstadoIncidencia? estado, string? texto, int pagina, int tamanoPagina, CancellationToken ct = default);
```

### 2b. El caso de uso

En `GestorIncidencias.Application/Incidencias/IIncidenciaService.cs`, **sustituye** la línea de `ListarAsync` por:

```csharp
    Task<Pagina<IncidenciaDto>> ListarAsync(EstadoIncidencia? estado = null, int pagina = 1, int tamanoPagina = Pagina<IncidenciaDto>.TamanoPorDefecto, string? texto = null, CancellationToken ct = default);
```

El parámetro nuevo va **al final** (antes de `ct`) y es **opcional**: así siguen compilando las llamadas que ya existían y usan nombres (`ListarAsync(estado, pagina, ct: ct)`).

En `GestorIncidencias.Application/Incidencias/IncidenciaService.cs`, **sustituye** el método `ListarAsync` por:

```csharp
    public Task<Pagina<IncidenciaDto>> ListarAsync(
        EstadoIncidencia? estado = null, int pagina = 1, int tamanoPagina = Pagina<IncidenciaDto>.TamanoPorDefecto,
        string? texto = null, CancellationToken ct = default)
    {
        (pagina, tamanoPagina) = Pagina<IncidenciaDto>.Normalizar(pagina, tamanoPagina);
        texto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();   // "   " = sin filtro
        return consultas.ListarAsync(estado, texto, pagina, tamanoPagina, ct);
    }
```

Compila: `dotnet build`.

❌ **Error esperado** (uno o varios):

```
error CS0535: 'EfIncidenciaConsultas' does not implement interface member 'IIncidenciaConsultas.ListarAsync(EstadoIncidencia?, string?, int, int, CancellationToken)'
```

Normal: el puerto ha cambiado, pero el adaptador todavía no.

## Paso 3 — Infrastructure: el `WHERE` (4 min)

En `GestorIncidencias.Infrastructure/Persistencia/EfIncidenciaConsultas.cs`, en el método `ListarAsync`:

1. Añade `string? texto` a la firma, **después** de `estado`:

```csharp
    public async Task<Pagina<IncidenciaDto>> ListarAsync(
        EstadoIncidencia? estado, string? texto, int pagina, int tamanoPagina, CancellationToken ct = default)
```

2. Debajo del filtro por estado, añade el filtro por texto:

```csharp
        if (estado is not null)
            consulta = consulta.Where(i => i.Estado == estado);
        if (texto is not null)
            consulta = consulta.Where(i => i.Titulo.Contains(texto));   // SQL: WHERE Titulo LIKE '%' + @texto + '%'
```

Como el filtro se añade al `IQueryable` **antes** del `CountAsync` y del `Skip`/`Take`, el total y la paginación ya tienen en cuenta la búsqueda. Con SQL Server, EF Core lo traduciría a:

```sql
SELECT COUNT(*) FROM [Incidencias] AS [i]
WHERE [i].[Titulo] LIKE N'%' + @texto + N'%'          -- @texto es un PARÁMETRO: sin inyección SQL
```

Compila: `dotnet build`.

❌ **Error esperado:**

```
error CS1503: Argument 4: cannot convert from 'System.Threading.CancellationToken' to 'string?'
```

en `IncidenciasApiController.cs`. La API llamaba a `ListarAsync(estado, pagina, tamano, ct)` **por posición**, y la cuarta posición ahora es `texto`. Lo arreglamos en el paso 5.

## Paso 4 — Web: el formulario y el paginador (7 min)

### 4a. El ViewModel

En `GestorIncidencias.Web/Models/ViewModels.cs`, añade `FiltroTexto` a `ListadoIncidenciasViewModel`:

```csharp
public record ListadoIncidenciasViewModel(
    Pagina<IncidenciaDto> Pagina,
    EstadoIncidencia? FiltroEstado,
    string? FiltroTexto,
    IReadOnlyList<VisitaReciente> VistasRecientemente);
```

### 4b. El controlador

En `GestorIncidencias.Web/Controllers/IncidenciasController.cs`, **sustituye** la acción `Index` por:

```csharp
    public async Task<IActionResult> Index(EstadoIncidencia? estado, string? texto, int pagina = 1, CancellationToken ct = default)
    {
        var resultado = await servicio.ListarAsync(estado, pagina, texto: texto, ct: ct);
        var recientes = HttpContext.Session.ObtenerVisitas();               // estado de SESIÓN (ver Estado/HistorialVisitas.cs)
        return View(new ListadoIncidenciasViewModel(resultado, estado, texto, recientes));   // Views/Incidencias/Index.cshtml
    }
```

El parámetro `texto` se rellena solo desde `?texto=...`: es el *model binding* haciendo el trabajo de `txtTexto.Text`.

### 4c. La vista

En `GestorIncidencias.Web/Views/Incidencias/Index.cshtml`:

1. Dentro del `<form ... method="get" class="filtro">`, **antes** de `<label for="estado">`, añade el cuadro de texto:

```cshtml
        <label for="texto">Título:</label>
        <input id="texto" name="texto" value="@Model.FiltroTexto" type="search" placeholder="Contiene..." />
```

2. Cambia el texto del botón `Filtrar` por `Buscar` (opcional).

3. Sustituye el mensaje de lista vacía por uno que valga para los dos filtros:

```cshtml
    <p class="vacio">No hay incidencias que cumplan el filtro.</p>
```

4. En los **dos** enlaces del paginador (`← Anterior` y `Siguiente →`), añade `asp-route-texto="@Model.FiltroTexto"` junto a `asp-route-estado`. Por ejemplo:

```cshtml
<a asp-action="Index" asp-route-estado="@Model.FiltroEstado" asp-route-texto="@Model.FiltroTexto" asp-route-pagina="@(pagina.NumeroPagina + 1)">Siguiente →</a>
```

Sin esto, al pasar de página **se perdería la búsqueda**: es lo que en Web Forms hacía el ViewState sin que lo vieras.

## Paso 5 — La API (2 min)

En `GestorIncidencias.Web/Controllers/Api/IncidenciasApiController.cs`, **sustituye** la acción `Listar` por:

```csharp
    public Task<Pagina<IncidenciaDto>> Listar(
        [FromQuery] EstadoIncidencia? estado, [FromQuery] string? texto, [FromQuery] int pagina = 1, [FromQuery] int tamano = Pagina<IncidenciaDto>.TamanoPorDefecto,
        CancellationToken ct = default) =>
        servicio.ListarAsync(estado, pagina, tamano, texto, ct);
```

✅ **Comprueba:** `dotnet build` compila sin errores.

## Paso 6 — Probar (2 min)

Ejecuta la aplicación, entra como `luis@demo.local` y prueba:

| # | Acción | Resultado esperado |
|---|---|---|
| 1 | Busca `correo` | **21 incidencias**, 2 páginas (20 "No se reciben correos externos" + "Caída del servicio de correo") |
| 2 | Pulsa **Siguiente →** | Página 2 de 2; la URL conserva `texto=correo` y el cuadro sigue relleno |
| 3 | Busca `de` con estado **Abierta** | 2 incidencias |
| 4 | Busca `xyz` | "No hay incidencias que cumplan el filtro." |
| 5 | Busca `impresora` y después `Impresora` | La primera encuentra 1; la segunda, **ninguna** (ver abajo) |
| 6 | En el `.http`, pide un token y ejecuta `GET {{host}}/api/incidencias?texto=VPN` con `Authorization: Bearer {{token}}` | La incidencia 4 (con `vpn` en minúsculas, ninguna: ver el caso 5) |
| 7 | Copia la URL de la búsqueda del caso 2, abre otra pestaña y pégala | La misma página de resultados (prueba de que el estado está en la URL, no en el servidor) |

> **Caso 5 — otra trampa de InMemory:** InMemory compara textos como C# (`string.Contains`, distingue mayúsculas). **SQL Server**, con la intercalación habitual (`Modern_Spanish_CI_AS`, `CI` = *case insensitive*), encontraría las dos. Una razón más para probar contra la base de datos real (capítulo 8).

## Errores típicos

| Síntoma | Causa probable | Solución |
|---|---|---|
| `CS0535 'EfIncidenciaConsultas' does not implement ... ListarAsync(...)` | Falta el paso 3, o los parámetros no están en el mismo orden que en la interfaz | Paso 3: `(estado, texto, pagina, tamanoPagina, ct)` |
| `CS1503 Argument 4: cannot convert from 'CancellationToken' to 'string?'` | La API llama por posición | Paso 5 |
| `CS7036 There is no argument given that corresponds to the required parameter 'VistasRecientemente'` | Se añadió `FiltroTexto` al ViewModel pero el controlador sigue pasando 3 argumentos | Paso 4b |
| `CS1061 'ListadoIncidenciasViewModel' does not contain a definition for 'FiltroTexto'` (en la vista) | Falta el paso 4a | Paso 4a |
| La búsqueda funciona pero al pasar de página se pierde | Falta `asp-route-texto` en los enlaces del paginador | Paso 4c.4 |
| El total sale bien pero la página trae incidencias que no cumplen el filtro | El `Where` del texto está **después** del `CountAsync`/`Skip`, o se aplica a otra variable | Paso 3: aplicarlo a `consulta` antes de contar |
| Al buscar, el listado no cambia | El `<input>` no tiene `name="texto"` (el *model binding* usa el `name`) | Paso 4c.1 |

## Ampliación opcional

1. **Razor Pages:** añade la misma búsqueda a `/Paginas/Incidencias` (propiedad `[BindProperty(SupportsGet = true)] public string? Texto { get; set; }` en `IndexModel`).
2. **Debate:** ¿y si queremos que la búsqueda no distinga mayúsculas en **cualquier** base de datos? (Pista: `EF.Functions.Like`, `ToLower()` y su efecto en los índices, o columnas con intercalación.)

## Resumen: qué has tocado

| Capa | Fichero | Cambio |
|---|---|---|
| Domain | — | **Nada**: buscar no es una regla de negocio |
| Application | `IIncidenciaConsultas.cs`, `IIncidenciaService.cs`, `IncidenciaService.cs` | Parámetro `texto` (normalizado: vacío = sin filtro) |
| Infrastructure | `EfIncidenciaConsultas.cs` | `Where(i => i.Titulo.Contains(texto))` → `LIKE` parametrizado |
| Web | `ViewModels.cs`, `IncidenciasController.cs`, `Index.cshtml` | Formulario GET, filtro en el ViewModel y en el paginador |
| Web (API) | `IncidenciasApiController.cs` | `?texto=` en `GET /api/incidencias` |
| Estado | — | **Ni ViewState ni Session**: todo en la URL |

## Referencias

> Enlaces comprobados el 8 de octubre de 2026.

- [Tag Helpers en formularios](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/working-with-forms?view=aspnetcore-10.0) — `asp-route-*` en enlaces.
- [Administración de sesiones y estados](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/app-state?view=aspnetcore-10.0) — Query string frente a sesión.
- [Consultas SQL en EF Core](https://learn.microsoft.com/es-es/ef/core/querying/sql-queries) — Inyección SQL y parámetros.
