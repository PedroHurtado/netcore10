# 4. Patrones de acceso a datos

Sabemos **cómo** hablar con la base de datos. Ahora toca decidir **dónde** se escribe ese código y **con qué forma**. Hay mucha opinión (y mucho dogma) sobre esto; aquí veremos las opciones habituales, qué problema resuelve cada una y cuál hemos elegido en el proyecto.

## 4.1 El punto de partida: ¿dónde estaba el SQL en el legacy?

```
 Web Forms (SIREI, típico)                     MVC 5 (Noticom, típico)
 ─────────────────────────                     ───────────────────────
 Page_Load / btnGuardar_Click                  Controller
   └─ SqlConnection + SqlCommand                 └─ new NoticomEntities()   (EF6, EDMX)
      └─ DataSet / DataTable                        └─ db.Noticias.Where(...).ToList()
         └─ GridView.DataSource = ...                  └─ return View(lista)

 El SQL está en la PÁGINA.                     El contexto está en el CONTROLADOR.
```

Las dos tienen el mismo problema que vimos el día 2: **la lógica y el acceso a datos están pegados a la interfaz**. Imposible de probar sin base de datos, imposible de reutilizar desde una API o un proceso nocturno.

## 4.2 Las opciones

| Patrón | Idea | A favor | En contra |
|---|---|---|---|
| **`DbContext` directo** en el controlador | El controlador usa `db.Incidencias...` | Lo más simple. El `DbContext` ya **es** repositorio y unidad de trabajo | Lógica y consultas en la capa web; difícil de probar; viola la regla de dependencia de Clean |
| **Repositorio genérico** `IRepository<T>` | `GetAll()`, `GetById()`, `Add()`, `Update()`, `Delete()` para todo | Parece "limpio" | Repite lo que ya hace `DbSet<T>`; o expone `IQueryable` (y no abstrae nada) o devuelve listas (y se trae tablas enteras). **Lo desaconsejamos** |
| **Repositorio por agregado** | `IIncidenciaRepository` con los métodos que necesitan los casos de uso | Interfaz pequeña y con nombres de negocio. El dominio no ve EF Core | Un método más por cada necesidad nueva |
| **Servicio de consultas** (lectura) | `IIncidenciaConsultas` que devuelve **DTOs** con proyección | Consultas eficientes, a medida de cada pantalla | Otro tipo más |
| **Unidad de trabajo** explícita | `IUnitOfWork.Commit()` que coordina varios repositorios | Transacción que abarca varios agregados | Con EF Core, `SaveChanges` **ya es** una unidad de trabajo |
| **SQL directo** (Dapper, `FromSql`, `SqlQuery`) | Escribir el SQL a mano | Control total; informes complejos; procedimientos almacenados existentes | Sin seguimiento de cambios; el SQL no se comprueba al compilar |

## 4.3 Lo que hemos elegido: dos caminos (CQRS "ligero")

```
                         ┌──────────────────────── Application ────────────────────────┐
                         │                                                              │
  Controlador ──▶ IIncidenciaService ──ESCRIBIR──▶ IIncidenciaRepository  (entidades, tracking)
   / Página                              │                       │
   / API                                 └──LEER───▶ IIncidenciaConsultas   (DTOs, proyección)
                         └──────────────────────────────────────┼───────────────────────┘
                                                                 │ implementan (Infrastructure)
                                        EfIncidenciaRepository ──┤── EfIncidenciaConsultas
                                                                 ▼
                                                         IncidenciasDbContext
```

**CQRS** (*Command Query Responsibility Segregation*) significa separar **escrituras** (comandos) de **lecturas** (consultas). Aquí lo aplicamos en su versión mínima: **misma base de datos, mismo `DbContext`, dos interfaces**. No hay bus de mensajes, ni dos bases de datos, ni MediatR.

### ¿Por qué separar? Porque leer y escribir tienen necesidades opuestas

| | Escribir (comando) | Leer (consulta) |
|---|---|---|
| ¿Qué necesito? | La **entidad** completa, con sus reglas | Solo las **columnas** que se ven en pantalla |
| ¿Seguimiento de cambios? | **Sí**: voy a modificar y guardar | **No**: no voy a guardar nada |
| ¿Datos relacionados? | El agregado (incidencia + comentarios, con `Include`) | Lo que pida la pantalla: nombre de la categoría (`JOIN`), nº de comentarios (`COUNT`) |
| ¿Forma del resultado? | `Incidencia` | `IncidenciaDto`, `ResumenIncidenciasDto`... |
| ¿Cuántas filas? | Una | Una página |

### El lado de escritura: `IIncidenciaRepository`

[IIncidenciaRepository.cs](../../src/day-03/GestorIncidencias.Application/Abstracciones/IIncidenciaRepository.cs) — **solo** lo que los casos de uso de escritura necesitan:

```csharp
public interface IIncidenciaRepository
{
    Task<Incidencia?> ObtenerPorIdAsync(int id, CancellationToken ct = default);   // agregado completo
    Task<int> ContarAbiertasAsync(CancellationToken ct = default);                  // regla de aplicación
    Task<bool> ExisteCategoriaAsync(int categoriaId, CancellationToken ct = default);
    Task AgregarAsync(Incidencia incidencia, CancellationToken ct = default);
    Task GuardarCambiosAsync(CancellationToken ct = default);
}
```

Fijaos en lo que **no** tiene: ni `ObtenerTodas`, ni `Actualizar(incidencia)`, ni `IQueryable`. No hace falta `Actualizar`: la entidad se cargó con seguimiento y EF Core sabe qué ha cambiado.

> **Cambio respecto al día 2:** `ObtenerTodasAsync` ha desaparecido del repositorio. Listar es una **lectura**: ahora la hace `IIncidenciaConsultas`.

### El lado de lectura: `IIncidenciaConsultas`

[IIncidenciaConsultas.cs](../../src/day-03/GestorIncidencias.Application/Abstracciones/IIncidenciaConsultas.cs) — una operación por necesidad de la interfaz:

```csharp
public interface IIncidenciaConsultas
{
    Task<Pagina<IncidenciaDto>> ListarAsync(EstadoIncidencia? estado, int pagina, int tamanoPagina, CancellationToken ct = default);
    Task<IncidenciaDto?> ObtenerAsync(int id, CancellationToken ct = default);
    Task<IncidenciaDetalleDto?> ObtenerDetalleAsync(int id, CancellationToken ct = default);
    Task<ResumenIncidenciasDto> ObtenerResumenAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct = default);
}
```

Su implementación, [EfIncidenciaConsultas.cs](../../src/day-03/GestorIncidencias.Infrastructure/Persistencia/EfIncidenciaConsultas.cs), usa una **proyección reutilizable**:

```csharp
private static readonly Expression<Func<Incidencia, IncidenciaDto>> ADto = i => new IncidenciaDto(
    i.Id, i.Titulo, i.Descripcion, i.Prioridad, i.Estado, i.FechaAlta, i.FechaResolucion,
    i.CategoriaId,
    i.Categoria != null ? i.Categoria.Nombre : null,   // → LEFT JOIN Categorias
    i.Comentarios.Count);                              // → subconsulta COUNT(*)
```

Es una **`Expression`**, no un método: EF Core puede **leerla** y traducirla a SQL. Si fuera un método normal (`IncidenciaDto.Desde(i)`, como en el día 2), EF Core tendría que traer la entidad entera a memoria para llamarlo.

### Los DTOs: el contrato con la interfaz

[IncidenciaDto.cs](../../src/day-03/GestorIncidencias.Application/Incidencias/IncidenciaDto.cs) ha crecido:

| DTO | Para | Novedad |
|---|---|---|
| `IncidenciaDto` | Listados y respuesta de los comandos | + `CategoriaId`, `Categoria`, `NumeroComentarios` |
| `IncidenciaDetalleDto` | Ficha | La incidencia **y** sus comentarios |
| `Pagina<T>` | Cualquier listado | Elementos + número de página + total |
| `ResumenIncidenciasDto` | Panel de inicio | Recuentos calculados en SQL |

### Y los comandos, ¿qué devuelven?

Un comando cambia datos y devuelve el `IncidenciaDto` actualizado. Pero el DTO lleva el **nombre** de la categoría y el **número** de comentarios, que la entidad no tiene cargados. Solución: tras guardar, el servicio **lee el DTO con la misma consulta** que usan las pantallas:

```csharp
await repositorio.GuardarCambiosAsync(ct);
return await LeerTrasGuardarAsync(id, ct);   // consultas.ObtenerAsync(id)
```

Una consulta más, pero **un único sitio** que construye el DTO. Si mañana el DTO cambia, solo cambia la proyección.

## 4.4 ¿Y el `DbContext` directamente? ¿No es más simple?

Sí, y en aplicaciones pequeñas o en una arquitectura **Vertical Slice** (día 2, capítulo 3) es perfectamente válido: cada rebanada usa el `DbContext` sin interfaces intermedias.

Nosotros mantenemos las interfaces porque:

1. **Regla de dependencia**: Application no puede referenciar EF Core (está en Infrastructure).
2. **Migración**: SIREI tiene SQL en ADO.NET. Durante la migración, `IIncidenciaConsultas` podría implementarse **primero con el SQL antiguo** (o Dapper) y más tarde con EF Core, **sin tocar ni una línea** de Application o Web. Es la gran ventaja del puerto/adaptador en un proyecto de migración.
3. **Pruebas**: los casos de uso se prueban con un repositorio falso (día 4).

> **Lo que NO hacemos:** un `IRepository<T>` genérico "por si acaso". Si una abstracción no tiene nombres de negocio, probablemente no está abstrayendo nada.

## 4.5 Cuando EF Core no es la mejor herramienta

| Necesidad | Herramienta |
|---|---|
| Un informe con SQL complejo (ventanas, CTE recursivas) | `db.Database.SqlQuery<FilaInforme>($"SELECT ...")` o Dapper |
| Reutilizar un **procedimiento almacenado** del legacy | `db.Incidencias.FromSql($"EXEC dbo.IncidenciasPendientes {usuario}")` |
| Actualizar 10.000 filas de golpe | `ExecuteUpdateAsync` / `ExecuteDeleteAsync` (capítulo 6) |
| Carga masiva (importar un Excel de 100.000 filas) | `SqlBulkCopy` (SQL Server) |

Todas encajan en el mismo esquema: van **dentro** de un adaptador de Infrastructure (por ejemplo, otra implementación de `IIncidenciaConsultas`). El resto de la aplicación no se entera.

> ⚠️ **SQL a mano = riesgo de inyección SQL.** `FromSql($"... {valor}")` y `SqlQuery($"... {valor}")` con **interpolación** generan **parámetros** (seguro). `FromSqlRaw("... " + valor)` con **concatenación** no (peligroso). Es el mismo error que `"... WHERE Id=" + txtId.Text` en Web Forms.

## 4.6 Transacciones y concurrencia (en una línea cada una)

- **Transacción**: un `SaveChanges` ya es una transacción. Si necesitáis varios `SaveChanges` atómicos: `await using var tx = await db.Database.BeginTransactionAsync(); ... await tx.CommitAsync();`.
- **Concurrencia**: si dos usuarios resuelven la misma incidencia a la vez, el último gana. La solución de EF Core es un **token de concurrencia** (en SQL Server, una columna `rowversion` con `IsRowVersion()`): si la fila cambió desde que la leíste, `SaveChanges` lanza `DbUpdateConcurrencyException`. Es el equivalente a la "concurrencia optimista" de los `DataAdapter` (capítulo 5).

## Referencias

> Enlaces comprobados el 7 de octubre de 2026.

- [Trabajar con datos en aplicaciones ASP.NET Core](https://learn.microsoft.com/es-es/dotnet/architecture/modern-web-apps-azure/work-with-data-in-asp-net-core-apps) — Libro de arquitectura de Microsoft: EF Core, repositorios y Dapper.
- [Diseño de la capa de persistencia de infraestructura](https://learn.microsoft.com/es-es/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design) — Repositorio por agregado y unidad de trabajo.
- [Consultas SQL](https://learn.microsoft.com/es-es/ef/core/querying/sql-queries) — `FromSql`, `SqlQuery` y protección frente a inyección SQL.
- [Transacciones](https://learn.microsoft.com/es-es/ef/core/saving/transactions)
- [Manejo de conflictos de concurrencia](https://learn.microsoft.com/es-es/ef/core/saving/concurrency)
- [Dapper (NuGet)](https://www.nuget.org/packages/Dapper)
