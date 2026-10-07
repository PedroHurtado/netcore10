# 6. Buenas prácticas de consultas y rendimiento

La mayoría de los problemas de rendimiento de una aplicación web con base de datos **no están en C#**: están en **cuántas consultas** se hacen y **cuántos datos** traen. Este capítulo resume las reglas que más impacto tienen, cada una con su versión mala y buena, y dónde se aplican en el proyecto.

## 6.0 La regla número cero: medir

Antes de optimizar, **mirad el SQL** que genera cada pantalla (capítulo 1.8: log de EF Core o `ToQueryString()`, con una base de datos real). Para cada pantalla, preguntaos:

1. ¿**Cuántas** sentencias SQL se ejecutan? (¿1-3, o 50?)
2. ¿**Cuántas filas y columnas** devuelve cada una? (¿las que se ven, o la tabla entera?)
3. ¿Cuánto tarda cada una? (`Executed DbCommand (0ms)`)

Con 204 incidencias todo es instantáneo; los problemas aparecen con los datos reales de producción. Por eso **un problema que se ve en el SQL ya es un problema aunque hoy tarde 0 ms**.

> En el curso usamos InMemory, que no genera SQL: el SQL de este capítulo es el que generaría EF Core con SQL Server. Las reglas se aplican igual al código, que es el mismo.

## 6.1 Filtrar, ordenar y agregar en la base de datos

```csharp
// ❌ MAL: trae TODA la tabla y cuenta en memoria
var todas = await db.Incidencias.ToListAsync();
var abiertas = todas.Count(i => i.Estado == EstadoIncidencia.Abierta);

// ✅ BIEN: SELECT COUNT(*) ... WHERE Estado = 'Abierta'
var abiertas = await db.Incidencias.CountAsync(i => i.Estado == EstadoIncidencia.Abierta);
```

**El ejemplo real del proyecto.** En el día 2, el panel de inicio hacía esto:

```csharp
var incidencias = await servicio.ListarAsync(ct: ct);             // todas las incidencias, todas las columnas
Abiertas: incidencias.Count(i => i.Estado == EstadoIncidencia.Abierta),
EnCurso:  incidencias.Count(i => i.Estado == EstadoIncidencia.EnCurso), ...
```

En el día 3, `EfIncidenciaConsultas.ObtenerResumenAsync` lo resuelve con agrupaciones en SQL:

```sql
SELECT [i].[Estado], COUNT(*) AS [Total]
FROM [Incidencias] AS [i]
GROUP BY [i].[Estado]
```

Cuatro filas en lugar de toda la tabla.

### `IQueryable` frente a `IEnumerable`: dónde se corta

```csharp
IQueryable<Incidencia> consulta = db.Incidencias;           // nada ejecutado todavía: se está construyendo SQL
consulta = consulta.Where(i => i.Estado == estado);         // sigue siendo SQL
var lista = await consulta.ToListAsync();                   // ← AQUÍ se ejecuta

lista.Where(...)                                            // a partir de aquí: C# en memoria
```

Todo lo que se encadena **antes** de `ToListAsync`/`FirstOrDefaultAsync`/`CountAsync` va al SQL. Lo de después, en memoria. Un `AsEnumerable()` o un `ToList()` "de más" en medio de una consulta convierte el resto en filtrado en memoria.

> Si en un `Where` se usa algo que EF Core **no sabe traducir** a SQL (un método vuestro, por ejemplo), lanza `The LINQ expression ... could not be translated`. Es un error **bueno**: EF Core se niega a traerse la tabla entera en silencio (EF Core 1.x y 2.x sí lo hacían, con la "evaluación en cliente" automática). La excepción a la regla es el **último** `Select`: ahí EF Core sí puede llamar a métodos C# sobre las columnas ya leídas.

## 6.2 Proyectar: pedir solo las columnas necesarias

```csharp
// ❌ Trae las 10 columnas (incluida Descripcion, hasta 2.000 caracteres por fila) y sigue los cambios
var lista = await db.Incidencias.Include(i => i.Categoria).ToListAsync();

// ✅ Solo lo que pinta el listado
var lista = await db.Incidencias
    .Select(i => new { i.Id, i.Titulo, Categoria = i.Categoria!.Nombre })
    .ToListAsync();
```

Una proyección:

- Reduce columnas (menos datos por la red y menos memoria).
- **No necesita `Include`**: `i.Categoria.Nombre` dentro del `Select` genera el `JOIN`.
- **No activa el seguimiento de cambios** (no son entidades).
- Puede incluir agregados: `i.Comentarios.Count` → subconsulta `COUNT(*)`.

Es lo que hace `EfIncidenciaConsultas` con la expresión `ADto` (capítulo 4).

## 6.3 Sin seguimiento cuando solo se lee: `AsNoTracking`

Si se cargan **entidades** que no se van a modificar:

```csharp
var incidencias = await db.Incidencias.AsNoTracking().Where(...).ToListAsync();
```

EF Core no guarda la "foto" de cada entidad: menos memoria y menos CPU. En el proyecto apenas lo necesitamos porque las lecturas son **proyecciones** (que ya no se siguen). Regla práctica:

| Situación | Qué usar |
|---|---|
| Leer para mostrar | **Proyección** a DTO (mejor) o `AsNoTracking` |
| Leer para modificar y guardar | Consulta normal (con seguimiento) — el **repositorio** |

## 6.4 El problema N+1

El error de rendimiento más frecuente con cualquier ORM:

```csharp
// ❌ N+1: 1 consulta para la lista + 1 consulta POR CADA incidencia
var incidencias = await db.Incidencias.Take(20).ToListAsync();          // 1 consulta
foreach (var i in incidencias)
{
    var n = await db.Set<Comentario>()
        .CountAsync(c => EF.Property<int>(c, "IncidenciaId") == i.Id);  // 20 consultas más
}
```

En el log se ve como **la misma sentencia repetida** muchas veces con distinto parámetro. Con 20 filas son 21 consultas; con una página de 100, 101.

Con **carga diferida** (*lazy loading*, típica de EF6 y Noticom) es todavía peor, porque **no se ve en el código**: basta con escribir `@incidencia.Categoria.Nombre` en una vista dentro de un `foreach`.

```csharp
// ✅ UNA consulta: el recuento va como subconsulta
var filas = await db.Incidencias.Take(20)
    .Select(i => new { i.Id, i.Titulo, NumeroComentarios = i.Comentarios.Count })
    .ToListAsync();
```

> En EF Core la carga diferida está **desactivada** por defecto (hay que instalar un paquete y activarla expresamente). **No la activéis** en una aplicación web: convierte cada vista en una fábrica de N+1.

## 6.5 `Include` de varias colecciones: la explosión cartesiana

```csharp
db.Expedientes
  .Include(e => e.Documentos)      // 20 por expediente
  .Include(e => e.Tramites)        // 30 por expediente
  .ToListAsync();
```

Con un solo `JOIN` el resultado tiene **20 × 30 = 600 filas por expediente**, casi todas con datos repetidos. EF Core avisa en el log. Opciones:

```csharp
.AsSplitQuery()     // EF Core lanza 3 consultas (expedientes, documentos, trámites) en lugar de 1 enorme
```

o, mejor, **proyectar** solo lo que se necesita. En nuestro agregado solo hay una colección (comentarios), así que no lo necesitamos.

## 6.6 Paginar siempre

```csharp
// ✅ Lo que hace EfIncidenciaConsultas.ListarAsync
var total = await consulta.CountAsync(ct);
var elementos = await consulta
    .OrderByDescending(i => i.Prioridad)
    .ThenBy(i => i.Id)                         // ← desempate: orden ESTABLE
    .Skip((pagina - 1) * tamanoPagina)
    .Take(tamanoPagina)
    .Select(ADto)
    .ToListAsync(ct);
```

```sql
... WHERE [i].[Estado] = @estado
    ORDER BY [i].[Prioridad] DESC, [i].[Id]
    OFFSET @p ROWS FETCH NEXT @p1 ROWS ONLY
```

Reglas:

- **Ningún listado sin límite**, tampoco en la API (`GET /api/incidencias` devuelve ahora una `Pagina<IncidenciaDto>`).
- **Orden estable**: sin el `ThenBy(i => i.Id)`, dos incidencias con la misma prioridad podrían salir en páginas distintas en cada petición (o en ninguna).
- **Validar el tamaño de página** que llega de fuera: `Pagina.Normalizar` limita a 100 (si no, `?tamano=1000000` es un "trae toda la tabla").
- Para tablas **enormes** y páginas muy profundas, `OFFSET 100000` es lento: la alternativa es la **paginación por clave** (*keyset*: `Where(i => i.Id > ultimoIdVisto).Take(20)`).

## 6.7 Índices

Un índice es a una tabla lo que el índice alfabético a un libro. EF Core crea **automáticamente** índices para las claves ajenas (`IX_Comentarios_IncidenciaId`, `IX_Incidencias_CategoriaId`). El resto, los decidimos nosotros según **cómo se consulta**:

```csharp
// El listado filtra por Estado y ordena por Prioridad
builder.HasIndex(i => new { i.Estado, i.Prioridad });       // en SQL Server → migración IndiceEstadoPrioridad

builder.HasIndex(c => c.Nombre).IsUnique();                 // además de rendimiento: integridad
```

Cada índice **acelera lecturas** y **ralentiza un poco las escrituras**. No se pone "uno por columna": se ponen para las consultas frecuentes, mirando el plan de ejecución (en SQL Server, el plan de ejecución real de SSMS).

## 6.8 Escrituras masivas sin cargar entidades

```csharp
// ❌ Carga 5.000 entidades, cambia una propiedad en cada una, 5.000 UPDATE
var viejas = await db.Incidencias.Where(i => i.Estado == EstadoIncidencia.Resuelta && i.FechaResolucion < limite).ToListAsync();
foreach (var i in viejas) i.Cerrar();
await db.SaveChangesAsync();

// ✅ UNA sentencia: UPDATE Incidencias SET Estado = 'Cerrada' WHERE ...
await db.Incidencias
    .Where(i => i.Estado == EstadoIncidencia.Resuelta && i.FechaResolucion < limite)
    .ExecuteUpdateAsync(s => s.SetProperty(i => i.Estado, EstadoIncidencia.Cerrada));
```

⚠️ `ExecuteUpdate`/`ExecuteDelete` **se saltan el dominio** (no se llama a `Cerrar()`, no se comprueban sus reglas) y el seguimiento de cambios. Úsalos en procesos masivos donde la regla sea trivial o se aplique en el propio `Where`, y documéntalo.

## 6.9 Otras buenas prácticas

| Práctica | Por qué |
|---|---|
| **Siempre `async`** (`ToListAsync`, `SaveChangesAsync`) y pasar el `CancellationToken` | Mientras la BD responde, el hilo atiende otras peticiones. Si el usuario cierra el navegador, se cancela la consulta |
| `AnyAsync()` en lugar de `CountAsync() > 0` | Para en la primera fila (`ExisteCategoriaAsync`) |
| `FirstOrDefaultAsync`/`SingleOrDefaultAsync` con filtro, no `ToList()` y luego `First()` | Trae una fila, no todas |
| No usar `Contains` con listas enormes (miles de Id) | Genera SQL gigante; mejor un `JOIN` o una tabla temporal |
| `DbContext` de vida corta (Scoped) | Cuantas más entidades sigue, más lento es `SaveChanges` |
| `AddDbContextPool` en aplicaciones con mucha carga | Reutiliza instancias del contexto (se mide antes de activarlo) |
| Consultas compiladas (`EF.CompileAsyncQuery`) | Solo para consultas **muy** calientes; casi nunca necesario |
| Caché de lo que casi no cambia | Las categorías se podrían cachear (`IMemoryCache`) o la página entera (Output Cache, día 2) |

## 6.10 Resumen: lista de comprobación para revisar una pantalla

- [ ] He mirado el SQL generado (log o `ToQueryString()`).
- [ ] El número de consultas **no crece** con el número de filas (no hay N+1).
- [ ] Filtro, orden, recuentos y agrupaciones se hacen en SQL.
- [ ] Solo se traen las columnas que se muestran (proyección).
- [ ] Hay paginación con orden estable, y el tamaño de página está limitado.
- [ ] Lo que se lee para mostrar no se sigue (proyección o `AsNoTracking`).
- [ ] Las columnas de los `WHERE`/`ORDER BY` frecuentes tienen índice.
- [ ] Todo es `async` y recibe el `CancellationToken`.

## Referencias

> Enlaces comprobados el 7 de octubre de 2026.

- [Introducción al rendimiento](https://learn.microsoft.com/es-es/ef/core/performance/)
- [Consultas eficaces](https://learn.microsoft.com/es-es/ef/core/performance/efficient-querying) — Proyección, N+1, carga diferida, paginación e índices.
- [Actualización eficaz](https://learn.microsoft.com/es-es/ef/core/performance/efficient-updating)
- [Temas de rendimiento avanzados](https://learn.microsoft.com/es-es/ef/core/performance/advanced-performance-topics) — `DbContext` pooling y consultas compiladas.
- [Seguimiento frente a consultas sin seguimiento](https://learn.microsoft.com/es-es/ef/core/querying/tracking)
- [Consultas únicas frente a consultas divididas](https://learn.microsoft.com/es-es/ef/core/querying/single-split-queries) — Explosión cartesiana y `AsSplitQuery`.
- [Evaluación de cliente frente a servidor](https://learn.microsoft.com/es-es/ef/core/querying/client-eval)
- [Paginación](https://learn.microsoft.com/es-es/ef/core/querying/pagination) — *Offset* frente a *keyset*.
- [ExecuteUpdate y ExecuteDelete](https://learn.microsoft.com/es-es/ef/core/saving/execute-insert-update-delete)
