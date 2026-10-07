# Lab 2 — De DataTable a EF Core: el informe por prioridad

**Duración:** 30 min · **Teoría relacionada:** [04 — Patrones de acceso a datos](../04-patrones-acceso-datos.md), [05 — De DataSet a EF Core](../05-dataset-a-ef-core.md), [06 — Rendimiento](../06-rendimiento-consultas.md)

## Objetivo

En SIREI hay muchos informes escritos así: ADO.NET, un `DataTable` y un bucle que cuenta. Vamos a **traducir uno** a EF Core y a colocarlo en **su sitio** de la arquitectura:

1. **Leer y analizar** el código legacy (no se ejecuta).
2. **Implementarlo** con EF Core como una consulta de lectura (`IIncidenciaConsultas`).
3. **Mostrarlo** en el panel de inicio, debajo de la tabla **Por categoría**.

Trabaja sobre tu copia del [Lab 1](lab-01-cambiar-categoria.md) (o sobre una copia nueva de `src/day-03`).

## Paso 1 — Analizar el código legacy (6 min)

Este es el informe "incidencias por prioridad" tal como estaría en una página Web Forms:

```csharp
// InformePrioridades.aspx.cs  (Web Forms, .NET Framework)
protected void Page_Load(object sender, EventArgs e)
{
    if (IsPostBack) return;

    var tabla = new DataTable("Incidencias");
    using (var cn = new SqlConnection(ConfigurationManager.ConnectionStrings["BD"].ConnectionString))
    {
        var da = new SqlDataAdapter("SELECT Prioridad, Estado FROM Incidencias", cn);
        da.Fill(tabla);
    }

    var totales = new Dictionary<int, int>();
    var pendientes = new Dictionary<int, int>();
    foreach (DataRow fila in tabla.Rows)
    {
        var prioridad = (int)fila["Prioridad"];
        var estado = (string)fila["Estado"];

        totales[prioridad] = totales.ContainsKey(prioridad) ? totales[prioridad] + 1 : 1;
        if (estado == "Abierta" || estado == "EnCurso")
            pendientes[prioridad] = pendientes.ContainsKey(prioridad) ? pendientes[prioridad] + 1 : 1;
    }

    gvInforme.DataSource = totales.OrderByDescending(t => t.Key).Select(t => new
    {
        Prioridad = ((Prioridad)t.Key).ToString(),
        Total = t.Value,
        Pendientes = pendientes.ContainsKey(t.Key) ? pendientes[t.Key] : 0
    });
    gvInforme.DataBind();
}
```

Con tu compañero, contestad:

| Pregunta | Vuestra respuesta |
|---|---|
| ¿Cuántas filas viajan de la base de datos al servidor web? | |
| ¿Dónde se hace el recuento? | |
| ¿Qué pasa si alguien escribe `"Encurso"` en lugar de `"EnCurso"`? | |
| ¿Qué pasa si el DBA cambia la columna `Prioridad` a `tinyint`? | |
| ¿Se puede probar este código sin base de datos ni servidor web? | |
| ¿En qué capas de nuestra arquitectura está repartido? | |

<details>
<summary>Respuestas</summary>

| Pregunta | Respuesta |
|---|---|
| Filas que viajan | **Todas** las de la tabla (hoy 204; con 10 años de datos, cientos de miles) |
| ¿Dónde se cuenta? | En el servidor web, en memoria, fila a fila |
| `"Encurso"` mal escrito | Compila, se ejecuta y **da un resultado incorrecto en silencio** (0 pendientes) |
| Columna `tinyint` | `(int)fila["Prioridad"]` lanza `InvalidCastException` **en ejecución**: la celda es un `byte` dentro de un `object` |
| ¿Se puede probar? | No: todo depende de `Page`, `SqlConnection` y la configuración estática |
| ¿Capas? | Todo en la **interfaz** (*code-behind*): acceso a datos, cálculo y presentación |

</details>

## Paso 2 — Application: el DTO y los contratos (6 min)

### 2a. El DTO

En `GestorIncidencias.Application/Incidencias/IncidenciaDto.cs`, debajo de `ResumenCategoriaDto`:

```csharp
/// <summary>Una fila del informe "incidencias por prioridad" (laboratorio 2).</summary>
public record ResumenPrioridadDto(Prioridad Prioridad, int Total, int Pendientes);
```

Fíjate: `Prioridad` es **el enum**, no un `string` ni un `int`. La vista decidirá cómo pintarlo.

### 2b. El puerto de lectura

Es una **lectura**, así que va en `GestorIncidencias.Application/Abstracciones/IIncidenciaConsultas.cs` (no en el repositorio). Añade debajo de `ObtenerResumenAsync`:

```csharp
    Task<IReadOnlyList<ResumenPrioridadDto>> ResumenPorPrioridadAsync(CancellationToken ct = default);
```

### 2c. El caso de uso

En `IIncidenciaService.cs`, debajo de `ObtenerResumenAsync`:

```csharp
    Task<IReadOnlyList<ResumenPrioridadDto>> ResumenPorPrioridadAsync(CancellationToken ct = default);
```

Y en `IncidenciaService.cs`, encima de `ListarCategoriasAsync` (es una lectura: solo delega):

```csharp
    public Task<IReadOnlyList<ResumenPrioridadDto>> ResumenPorPrioridadAsync(CancellationToken ct = default) =>
        consultas.ResumenPorPrioridadAsync(ct);
```

Compila: `dotnet build`.

❌ **Error esperado:**

```
error CS0535: 'EfIncidenciaConsultas' does not implement interface member 'IIncidenciaConsultas.ResumenPorPrioridadAsync(CancellationToken)'
```

Normal: el puerto está declarado, pero falta el adaptador.

## Paso 3 — Infrastructure: la consulta con EF Core (7 min)

En `GestorIncidencias.Infrastructure/Persistencia/EfIncidenciaConsultas.cs`, encima de `ListarCategoriasAsync`:

```csharp
    public async Task<IReadOnlyList<ResumenPrioridadDto>> ResumenPorPrioridadAsync(CancellationToken ct = default) =>
        await db.Incidencias
            .GroupBy(i => i.Prioridad)
            .OrderByDescending(g => g.Key)                // ordenar ANTES de proyectar (ver errores típicos)
            .Select(g => new ResumenPrioridadDto(
                g.Key,
                g.Count(),
                g.Count(i => i.Estado == EstadoIncidencia.Abierta || i.Estado == EstadoIncidencia.EnCurso)))
            .ToListAsync(ct);
```

✅ **Comprueba:** `dotnet build` compila sin errores.

Compara con el legacy: el bucle, los dos diccionarios y las cadenas `"Abierta"`/`"EnCurso"` se han convertido en un `GroupBy` con dos `Count`. Con SQL Server, EF Core lo traduciría a **una** sentencia que devuelve **4 filas**:

```sql
SELECT [i].[Prioridad], COUNT(*), COUNT(CASE
    WHEN [i].[Estado] IN (N'Abierta', N'EnCurso') THEN 1
END)
FROM [Incidencias] AS [i]
GROUP BY [i].[Prioridad]
ORDER BY [i].[Prioridad] DESC
```

Es la misma técnica que ya usa `ObtenerResumenAsync` para las categorías: échale un vistazo.

## Paso 4 — Web: mostrarlo en el panel (6 min)

### 4a. El ViewModel

En `GestorIncidencias.Web/Models/ViewModels.cs`, sustituye `PanelViewModel` por:

```csharp
public record PanelViewModel(
    string NombreAplicacion,
    ResumenIncidenciasDto Resumen,
    IReadOnlyList<ResumenPrioridadDto> PorPrioridad);
```

### 4b. El controlador

En `GestorIncidencias.Web/Controllers/HomeController.cs`, sustituye las dos líneas del final de `Index`:

```csharp
        var resumen = await servicio.ObtenerResumenAsync(ct);
        var porPrioridad = await servicio.ResumenPorPrioridadAsync(ct);
        return View(new PanelViewModel(opciones.Value.NombreAplicacion, resumen, porPrioridad));
```

### 4c. La vista

En `GestorIncidencias.Web/Views/Home/Index.cshtml`, **encima** de `<h2>La misma aplicación, tres interfaces</h2>`:

```cshtml
<h2>Por prioridad</h2>
<table>
    <thead>
        <tr><th>Prioridad</th><th class="numero">Pendientes</th><th class="numero">Total</th></tr>
    </thead>
    <tbody>
        @foreach (var fila in Model.PorPrioridad)
        {
            <tr>
                <td class="prioridad-@fila.Prioridad.ToString().ToLowerInvariant()">@fila.Prioridad</td>
                <td class="numero">@fila.Pendientes</td>
                <td class="numero">@fila.Total</td>
            </tr>
        }
    </tbody>
</table>
```

## Paso 5 — Probar (3 min)

Ejecuta la aplicación y abre http://localhost:5196.

✅ **Comprueba:** debajo de **Por categoría** aparece **Por prioridad**, con este contenido si acabas de arrancar (los datos de demostración se cargan en cada arranque):

| Prioridad | Pendientes | Total |
|---|---|---|
| Critica | 1 | 51 |
| Alta | 1 | 51 |
| Media | 0 | 51 |
| Baja | 1 | 51 |

✅ **Comprueba:** crea una incidencia de prioridad **Alta** desde `/Incidencias/Crear` y vuelve al panel: Alta pasa a **2 pendientes y 52 en total**.

## Paso 6 — Comparar (2 min)

| | Legacy (`DataTable`) | EF Core |
|---|---|---|
| Filas que viajan desde la BD | Todas | 4 |
| Dónde se cuenta | Servidor web, en un bucle | Base de datos (`GROUP BY`) |
| Nombre de columna o estado mal escrito | Error en ejecución o resultado incorrecto | **No compila** |
| Capa | Todo en el *code-behind* | Consulta en Infrastructure, contrato en Application, pintado en Web |
| Cambiar de BD o volver a ADO.NET | Reescribir la página | Otra implementación de `IIncidenciaConsultas` |

> **Matiz:** el legacy es lento por **contar en memoria**, no por usar `DataTable`. Un `SELECT Prioridad, COUNT(*) ... GROUP BY Prioridad` en el `SqlDataAdapter` sería igual de eficiente. EF Core hace **fácil lo correcto** y **difícil equivocarse de nombre**.
>
> Y en una migración real, si no hay tiempo de reescribir, el código ADO.NET se puede llevar **tal cual** a una clase de Infrastructure que implemente `IIncidenciaConsultas`. Application y Web no cambiarían ([capítulo 5.5](../05-dataset-a-ef-core.md#55-estrategia-para-migrar-el-acceso-a-datos-de-una-aplicación-legacy)).

## Errores típicos

| Síntoma | Causa probable | Solución |
|---|---|---|
| `CS0535 'EfIncidenciaConsultas' does not implement ... 'ResumenPorPrioridadAsync'` | Falta el paso 3 | Paso 3 |
| `CS0535 'IncidenciaService' does not implement ...` | Falta la implementación del paso 2c | Paso 2c |
| `CS0246 'ResumenPrioridadDto' could not be found` | El DTO no está en `IncidenciaDto.cs` (o está en otro *namespace*) | Paso 2a |
| `CS7036 There is no argument given that corresponds to the required parameter 'PorPrioridad'` | Se cambió el `PanelViewModel` pero no el `HomeController` | Paso 4b |
| Al abrir el panel: `The LINQ expression ... could not be translated` | El `OrderByDescending` está **después** del `Select(new ResumenPrioridadDto(...))`: EF Core no sabe "mirar dentro" de un objeto construido con constructor | Ordenar por `g.Key` **antes** del `Select`, como en el paso 3 |
| `CS1061 'PanelViewModel' does not contain a definition for 'PorPrioridad'` (en `Index.cshtml`) | La vista usa `Model.PorPrioridad` pero el ViewModel no se cambió | Paso 4a |
| La tabla sale vacía | La consulta devuelve una lista vacía (método sin terminar) | Revisa el paso 3 |

## Ampliación opcional

1. **API:** expón el informe como `GET /api/incidencias/resumen/prioridades` en `IncidenciasApiController` (una línea, como `Categorias`).
2. **Debate:** el panel hace ahora 4 consultas (estados, críticas, categorías y prioridades). ¿Compensaría juntarlas? ¿Y cachear el panel 30 segundos? (Pista: Output Cache, día 2.)

## Referencias

> Enlaces comprobados el 7 de octubre de 2026.

- [Conjuntos de datos (DataSets), tablas de datos (DataTables) y vistas de datos (DataViews)](https://learn.microsoft.com/es-es/dotnet/framework/data/adonet/dataset-datatable-dataview/)
- [Operadores de consulta complejos](https://learn.microsoft.com/es-es/ef/core/querying/complex-query-operators) — `GroupBy` y su traducción a SQL.
- [Evaluación de cliente frente a servidor](https://learn.microsoft.com/es-es/ef/core/querying/client-eval) — Por qué algunas expresiones no se pueden traducir.
