# 5. De DataSet y DataTable a EF Core

Muchas aplicaciones Web Forms (y SIREI, previsiblemente, entre ellas) acceden a datos con **ADO.NET "desconectado"**: `SqlDataAdapter`, `DataSet`, `DataTable`, `DataView`, a veces **DataSets tipados** (`.xsd`) con `TableAdapter`. Este capítulo da una **tabla de traducción** y una **estrategia** para migrar ese código sin morir en el intento.

## 5.1 Primero, una buena noticia

`DataSet` y `DataTable` **siguen existiendo en .NET 10** (`System.Data`). El código ADO.NET compila y funciona en ASP.NET Core casi sin cambios.

| Pieza | ¿Existe en .NET 10? |
|---|---|
| `DataSet`, `DataTable`, `DataRow`, `DataView` | ✅ Sí |
| `SqlConnection`, `SqlCommand`, `SqlDataAdapter` | ✅ Sí, en el paquete `Microsoft.Data.SqlClient` (sustituye a `System.Data.SqlClient`) |
| DataSets tipados (`.xsd` + `TableAdapter`) | ⚠️ El código generado compila, pero el **diseñador** de Visual Studio no está disponible para proyectos .NET modernos |
| `SqlDataSource`, `ObjectDataSource`, `GridView` con *binding* | ❌ Son controles de Web Forms: no existen en ASP.NET Core |

Consecuencia práctica: **la migración del acceso a datos no tiene por qué ir a la vez que la migración de la interfaz**. Se puede llevar el ADO.NET tal cual a un adaptador de Infrastructure, migrar las pantallas, y reescribir el acceso a datos después, caso a caso.

## 5.2 Dos modelos mentales distintos

```
 ADO.NET desconectado                               EF Core
 ────────────────────                               ───────
 SQL escrito a mano          ──▶  DataTable         LINQ                  ──▶  objetos C# (List<Incidencia>)
 filas y columnas genéricas       fila["Estado"]    tipos de tu dominio        incidencia.Estado
 (object, hay que convertir)      (string)fila[..]  (comprobado al compilar)
 el DataTable guarda el estado    RowState          el DbContext guarda       ChangeTracker
 de cada fila                     Added/Modified    el estado de cada entidad  Added/Modified
 da.Update(tabla) genera          INSERT/UPDATE     SaveChanges() genera       INSERT/UPDATE
 los comandos                                       los comandos
```

Se parecen más de lo que parece: los dos **traen datos, recuerdan qué ha cambiado y generan los comandos para guardarlo**. La gran diferencia es que EF Core trabaja con **tipos de vuestro dominio** en lugar de filas genéricas.

## 5.3 Tabla de traducción

| ADO.NET / DataSet | EF Core | Notas |
|---|---|---|
| `SqlConnection` + cadena en `Web.config` | `DbContext` registrado con `AddDbContext` y cadena en `appsettings.json` | El contexto abre y cierra la conexión solo |
| `DataSet` (varias tablas + relaciones) | `DbContext` (varios `DbSet` + relaciones del modelo) | |
| `DataTable` | `DbSet<T>` / `List<T>` | |
| `DataRow` | Una entidad (`Incidencia`) o un DTO | |
| `fila["Titulo"]`, `(int)fila["Id"]` | `incidencia.Titulo`, `incidencia.Id` | Errores de nombre o tipo: **al compilar**, no en ejecución |
| `DBNull.Value`, `fila.IsNull("Fecha")` | `null` (`DateTimeOffset?`) | |
| `DataRelation` | Navegaciones + `HasOne`/`HasMany` | |
| `SqlDataAdapter.Fill(tabla)` | `await consulta.ToListAsync()` | |
| `SqlDataAdapter.Update(tabla)` | `await db.SaveChangesAsync()` | |
| `DataRow.RowState` | `db.Entry(entidad).State` | `Added`, `Modified`, `Deleted`, `Unchanged`... ¡casi los mismos nombres! |
| `tabla.AcceptChanges()` | Lo hace `SaveChanges` | |
| `DataView.RowFilter = "Estado = 'Abierta'"` | `.Where(i => i.Estado == EstadoIncidencia.Abierta)` | **En el servidor**, no en memoria |
| `DataView.Sort = "Prioridad DESC"` | `.OrderByDescending(i => i.Prioridad)` | En el servidor |
| `tabla.Compute("COUNT(Id)", "Estado='Abierta'")` | `.CountAsync(i => i.Estado == ...)` | En el servidor |
| `tabla.Select("Prioridad = 3")` | `.Where(...)` | |
| `SqlCommand.ExecuteNonQuery("UPDATE ...")` | `ExecuteUpdateAsync(...)` | Sin cargar entidades |
| `SqlCommand.ExecuteScalar("SELECT COUNT(*)...")` | `CountAsync()` / `AnyAsync()` / `MaxAsync()` | |
| Concurrencia optimista del adaptador (`WHERE` con valores originales) | Token de concurrencia (`IsRowVersion`, `IsConcurrencyToken`) | `DbUpdateConcurrencyException` |
| `SqlTransaction` | `SaveChanges` (implícita) o `db.Database.BeginTransactionAsync()` | |
| DataSet tipado `.xsd` | Entidades + configuración (o **scaffolding** desde la BD) | Ver 5.6 |
| Procedimiento almacenado con `CommandType.StoredProcedure` | `FromSql($"EXEC ...")` / `ExecuteSqlAsync($"EXEC ...")` | Se pueden mantener |

## 5.4 Antes y después: el mismo informe

El informe "incidencias por prioridad" al estilo legacy:

```csharp
// ANTES: ADO.NET + DataTable
var tabla = new DataTable("Incidencias");
using (var cn = new SqlConnection(ConfigurationManager.ConnectionStrings["BD"].ConnectionString))
{
    var da = new SqlDataAdapter("SELECT Prioridad, Estado FROM Incidencias", cn);
    da.Fill(tabla);                                     // ① trae TODAS las filas
}

foreach (var p in new[] { 3, 2, 1, 0 })
{
    var total = Convert.ToInt32(tabla.Compute("COUNT(Prioridad)", $"Prioridad = {p}"));       // ② cuenta en memoria
    var pendientes = Convert.ToInt32(tabla.Compute("COUNT(Prioridad)",
        $"Prioridad = {p} AND Estado IN ('Abierta', 'EnCurso')"));                               // ③ cadenas sin comprobar
    // ... añadir a la lista del GridView
}
```

```csharp
// DESPUÉS: EF Core
var filas = await db.Incidencias
    .GroupBy(i => i.Prioridad)
    .Select(g => new
    {
        Prioridad = g.Key,
        Total = g.Count(),
        Pendientes = g.Count(i => i.Estado == EstadoIncidencia.Abierta || i.Estado == EstadoIncidencia.EnCurso)
    })
    .OrderByDescending(x => x.Prioridad)
    .ToListAsync(ct);
```

SQL que generaría EF Core con SQL Server:

```sql
SELECT [i].[Prioridad], COUNT(*) AS [Total], COUNT(CASE
    WHEN [i].[Estado] IN (N'Abierta', N'EnCurso') THEN 1
END) AS [Pendientes]
FROM [Incidencias] AS [i]
GROUP BY [i].[Prioridad]
ORDER BY [i].[Prioridad] DESC
```

| | Antes | Después |
|---|---|---|
| Filas que viajan desde la BD | **Todas** (204 hoy; 500.000 dentro de 5 años) | **4** (una por prioridad) |
| Dónde se cuenta | En el servidor web, en memoria | En la base de datos |
| Si alguien renombra `Estado` | Falla **en ejecución** (cadena `"Estado IN (...)"`) | Falla **al compilar** |
| `Prioridad` llega como | `object` (si la columna es `bigint` o `tinyint`, `(int)fila["Prioridad"]` lanza `InvalidCastException`) | `Prioridad` (el enum) |
| Probar sin base de datos | No | La lógica de negocio sí (está en el dominio) |

> El "después" no es mejor **por ser EF Core**: un `SELECT ... GROUP BY` escrito a mano en el `SqlDataAdapter` sería igual de eficiente. Es mejor porque **EF Core hace fácil lo correcto** (filtrar y agregar en el servidor) y difícil lo incorrecto (equivocarse de nombre de columna).

## 5.5 Estrategia para migrar el acceso a datos de una aplicación legacy

No se reescribe todo de golpe. Orden recomendado:

1. **Inventario.** Buscar en el código `SqlConnection`, `SqlDataAdapter`, `DataSet`, `.xsd`, `SqlDataSource`, `ExecuteReader`, `CommandType.StoredProcedure`. Contar cuántos hay y dónde (¿en el *code-behind*? ¿en una capa DAL?).
2. **Sacar el SQL de las páginas.** Si está en el *code-behind*, moverlo a clases (aún con ADO.NET) detrás de una interfaz con nombres de negocio. Esto **ya se puede hacer en el propio .NET Framework**, antes de migrar.
3. **Base de datos existente → modelo EF Core por *scaffolding*** (5.6). No se rediseña la BD: EF Core se adapta a ella.
4. **Reescribir por casos de uso**, empezando por las **lecturas** (menos riesgo) y por lo más usado. Cada uno: misma interfaz, nueva implementación con EF Core, comparar resultados con la antigua.
5. **Escrituras** al final, con pruebas: es donde están las reglas de negocio escondidas (*"al guardar, si el estado es 3, actualizar también la tabla de avisos"*).
6. **Lo que no compense** (informes enormes, procedimientos almacenados probados durante años) se queda en SQL, envuelto en su adaptador.

## 5.6 BD existente: ingeniería inversa (*scaffolding*)

Con una base de datos legacy **no se empieza por las entidades**: se generan desde las tablas.

```bash
dotnet ef dbcontext scaffold "Server=...;Database=SIREI;Trusted_Connection=True;TrustServerCertificate=True" \
    Microsoft.EntityFrameworkCore.SqlServer \
    --output-dir Persistencia/Modelo --context SireiDbContext \
    --table dbo.Expedientes --table dbo.Tramites \
    --no-onconfiguring
```

| Opción | Para qué |
|---|---|
| `--table` | Solo las tablas que se van a usar (una BD legacy puede tener 300) |
| `--no-onconfiguring` | No escribir la cadena de conexión en el código generado |
| `--use-database-names` | Respetar los nombres de la BD tal cual (`ID_EXPEDIENTE`) |
| `--data-annotations` | Generar atributos en lugar de API fluida (no lo usaremos: dominio limpio) |

Lo generado es un **punto de partida**: clases anémicas con setters públicos, nombres de la BD... Igual que hicimos con `Incidencia` el día 2, después se van convirtiendo en entidades con comportamiento.

> En este escenario **no hay migraciones**: la BD la gestiona su DBA como siempre. Las migraciones de EF Core se adoptan, si se adoptan, cuando la aplicación nueva pasa a ser la dueña del esquema.

## 5.7 Errores típicos al pasar de DataTable a EF Core

| Síntoma | Causa | Solución |
|---|---|---|
| La página tarda segundos | Se ha traducido `da.Fill(tabla)` por `db.Tabla.ToList()` y luego se filtra en memoria | Filtrar **antes** de `ToList` (`Where`, `Select`, paginación) |
| `NullReferenceException` en `x.Categoria.Nombre` | En EF6/DataSet "venía todo"; en EF Core las navegaciones no se cargan solas | `Include` o, mejor, proyección |
| El cambio no se guarda | Se modificó una entidad leída con `AsNoTracking`, o un DTO | Leer con seguimiento (el repositorio) y llamar a `SaveChanges` |
| `InvalidCastException` | Código ADO.NET con `(int)fila["Col"]` y el proveedor devuelve otro tipo (`long`, `decimal`...) | `Convert.ToInt32(...)` o, mejor, EF Core con tipos |
| "The LINQ expression could not be translated" | Se ha usado en el `Where` un método C# que no existe en SQL | Reescribir la condición con operaciones traducibles (capítulo 6) |

## Referencias

> Enlaces comprobados el 7 de octubre de 2026.

- [DataSets, DataTables y DataViews](https://learn.microsoft.com/es-es/dotnet/framework/data/adonet/dataset-datatable-dataview/) — Documentación de ADO.NET.
- [DataTable (System.Data) en .NET 10](https://learn.microsoft.com/es-es/dotnet/api/system.data.datatable?view=net-10.0)
- [Seguimiento de cambios](https://learn.microsoft.com/es-es/ef/core/change-tracking/) — El equivalente a `RowState`.
- [Ingeniería inversa](https://learn.microsoft.com/es-es/ef/core/managing-schemas/scaffolding/) — `dotnet ef dbcontext scaffold` sobre una base de datos existente.
- [Comparación de EF6 y EF Core](https://learn.microsoft.com/es-es/ef/efcore-and-ef6/) — Útil para Noticom.
