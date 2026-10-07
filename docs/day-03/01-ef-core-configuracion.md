# 1. Entity Framework Core: configuración

Hasta ahora hemos usado EF Core casi sin mirarlo: una entidad, un `DbSet` y el proveedor **InMemory**. Hoy vemos cómo se configura EF Core en un proyecto real: proveedor, cadena de conexión, `DbContext`, mapeo y conversiones.

En el curso **seguimos con InMemory**: no hay que instalar nada y el código es el mismo que con SQL Server. Cuando algo funcione distinto en una base de datos real, lo indicamos.

Todo el código está en [src/day-03](../../src/day-03/).

## 1.1 Qué es EF Core (y qué no es)

EF Core es un **ORM** (*Object-Relational Mapper*): traduce entre **objetos C#** y **tablas SQL**.

```
   Código C#                         EF Core                          Base de datos
 ─────────────                ─────────────────────               ──────────────────
 db.Incidencias               traduce LINQ a SQL     ──SELECT──▶   tabla Incidencias
   .Where(i => i.Estado          (proveedor)
       == Abierta)            materializa filas      ◀──filas───
   .ToListAsync()                en objetos
                              detecta cambios         ──UPDATE──▶
 db.SaveChangesAsync()           y genera SQL
```

| EF Core **sí** hace | EF Core **no** hace |
|---|---|
| Generar el SQL de consultas LINQ | Decidir por ti qué datos necesitas (si pides 10.000 filas, trae 10.000) |
| Detectar qué ha cambiado y generar `INSERT`/`UPDATE`/`DELETE` | Arreglar un diseño de tablas malo |
| Crear y evolucionar el esquema (**migraciones**, capítulo 3) | Sustituir saber SQL: hay que **leer** el SQL que genera |
| Gestionar transacciones en `SaveChanges` | Funcionar igual con todos los proveedores (cada uno tiene sus límites) |

> Si venís de **ADO.NET con `DataSet`** (SIREI) o de **EF6 con EDMX** (Noticom), el capítulo 5 traduce cada concepto. De momento basta con esto: EF Core es "ADO.NET + mapeo + seguimiento de cambios", y por debajo sigue usando `DbConnection` y `DbCommand`.

## 1.2 El proveedor

EF Core no habla con la base de datos directamente: lo hace a través de un **proveedor** (un paquete NuGet).

| Proveedor | Paquete | Cuándo |
|---|---|---|
| **InMemory** | `Microsoft.EntityFrameworkCore.InMemory` | **El curso**: ejemplos sin instalar nada |
| **SQL Server** | `Microsoft.EntityFrameworkCore.SqlServer` | Lo habitual en producción en la Administración |
| SQLite, PostgreSQL, Oracle, MySQL... | Paquetes propios o de terceros | Según la organización |

Cambiar de proveedor es **una línea** en [DependencyInjection.cs](../../src/day-03/GestorIncidencias.Infrastructure/DependencyInjection.cs):

```csharp
services.AddDbContext<IncidenciasDbContext>(opt => opt.UseInMemoryDatabase("GestorIncidencias"));   // curso
services.AddDbContext<IncidenciasDbContext>(opt => opt.UseSqlServer(cadenaConexion));               // producción
```

Gracias a la arquitectura del día 2, **nada más cambiaría**: ni el dominio, ni los casos de uso, ni los controladores.

### Lo que InMemory NO hace (y una base de datos real sí)

InMemory guarda listas de objetos en memoria. Es perfecto para los ejemplos del curso, pero conviene saber lo que **no** es:

| | InMemory | SQL Server |
|---|---|---|
| ¿Es relacional? | No | Sí |
| ¿Genera SQL? | No (no hay nada que ver en el log) | Sí |
| ¿Migraciones? | No (no hay esquema) | Sí (capítulo 3) |
| ¿Comprueba longitudes, unicidad...? | No | Sí: la BD rechaza los datos incorrectos |
| ¿Persiste al parar la aplicación? | No: cada arranque empieza de cero | Sí |
| ¿Sirve para pruebas automáticas? | Microsoft lo **desaconseja** | — |

Consecuencia práctica: **un código que funciona con InMemory puede fallar con SQL Server** (una consulta que no se puede traducir, un título demasiado largo, una clave duplicada). En un proyecto real se desarrolla contra la misma base de datos que en producción.

## 1.3 Qué ha cambiado en el proyecto

| Fichero | Cambio |
|---|---|
| `Domain/Categorias/Categoria.cs` | **Nuevo**: catálogo de categorías |
| `Domain/Incidencias/Comentario.cs` | **Nuevo**: comentarios de una incidencia |
| `Domain/Incidencias/Incidencia.cs` | Categoría, comentarios, `AgregarComentario`, `Reabrir` |
| `Infrastructure/Persistencia/Configuraciones/` | Configuración de las tres entidades, relaciones e índices |
| `Infrastructure/Persistencia/EfIncidenciaConsultas.cs` | **Nuevo**: lecturas con proyección (capítulo 4) |
| `Infrastructure/Persistencia/InicializadorBaseDatos.cs` | Sustituye a `DatosDemoInitializer`: `EnsureCreated` + datos de demostración con histórico |
| `Application/Comun/Pagina.cs` | **Nuevo**: resultados paginados (capítulo 6) |

## 1.4 La cadena de conexión (con una base de datos real)

InMemory no necesita cadena de conexión. Con SQL Server, se escribe en `appsettings.json`:

```json
"ConnectionStrings": {
  "GestorIncidencias": "Server=SERVIDOR;Database=GestorIncidencias;Trusted_Connection=True;TrustServerCertificate=True"
}
```

y se lee con `GetConnectionString`:

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuracion)
{
    var cadena = configuracion.GetConnectionString("GestorIncidencias")
        ?? throw new InvalidOperationException("Falta la cadena de conexión 'GestorIncidencias'.");

    services.AddDbContext<IncidenciasDbContext>(opt => opt.UseSqlServer(cadena));
    // ...
}
```

Tres reglas:

1. **En producción la cadena no va en `appsettings.json`**, y menos con usuario y contraseña. Recordad el día 1: variable de entorno `ConnectionStrings__GestorIncidencias`, *user secrets* en desarrollo o un almacén de secretos.
2. **Seguridad integrada** (`Trusted_Connection=True`) siempre que se pueda: la aplicación se conecta con la identidad del grupo de aplicaciones de IIS y no hay contraseña que guardar.
3. **`EnableSensitiveDataLogging()` solo en desarrollo**: escribe en el log los valores de los parámetros SQL (`@p0='María López'`), que en producción son datos personales.

> **Equivalencia con Web Forms / MVC 5:** antes era `<connectionStrings>` en `Web.config` y `ConfigurationManager.ConnectionStrings["BD"]`. Ahora es `ConnectionStrings` en `appsettings.json` y `IConfiguration.GetConnectionString("...")`, **inyectado**, nunca estático.

## 1.5 El `DbContext`

[IncidenciasDbContext.cs](../../src/day-03/GestorIncidencias.Infrastructure/Persistencia/IncidenciasDbContext.cs):

```csharp
public class IncidenciasDbContext(DbContextOptions<IncidenciasDbContext> options) : DbContext(options)
{
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    // No hay DbSet<Comentario>: los comentarios se guardan a través de su incidencia (capítulo 2).

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IncidenciasDbContext).Assembly);
}
```

El `DbContext` es una **sesión de trabajo** con la base de datos. Hace tres cosas:

| Papel | Qué significa | Ejemplo |
|---|---|---|
| **Unidad de trabajo** | Acumula cambios y los guarda **todos juntos** en `SaveChanges`, en **una transacción** | El inicializador añade 204 incidencias con sus comentarios y hace **un** `SaveChangesAsync` |
| **Mapa de identidad** | En el mismo contexto, la incidencia 3 es siempre **el mismo objeto** | Dos consultas de la incidencia 3 devuelven la misma instancia |
| **Seguimiento de cambios** | Guarda una "foto" de lo que cargó y compara al guardar | `incidencia.Resolver(...)` + `SaveChanges` → `UPDATE ... SET Estado = 'Resuelta'` |

### Tiempo de vida: *Scoped*

`AddDbContext` lo registra como **Scoped**: **un contexto por petición HTTP**. Es lo correcto porque:

- **No es seguro para varios hilos**: dos peticiones no pueden compartirlo.
- Debe ser **de vida corta**: cuanto más vive, más entidades sigue y más memoria y tiempo consume.

Fuera de una petición (un `IHostedService`, como `InicializadorBaseDatos`) hay que **crear el ámbito a mano**:

```csharp
using var scope = scopeFactory.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<IncidenciasDbContext>();
```

> ❌ **Error clásico:** inyectar el `DbContext` en un servicio **Singleton**. El contexto quedaría vivo para siempre, compartido entre peticiones simultáneas → excepciones de concurrencia y fugas de memoria. ASP.NET Core lo detecta en Development ("Cannot consume scoped service ... from singleton").

## 1.6 Configurar el modelo: convenciones y API fluida

EF Core deduce casi todo por **convención**:

| Convención | Resultado |
|---|---|
| Propiedad `Id` o `<Clase>Id` | Clave primaria (autonumérica si es `int`) |
| `string` frente a `string?` | Columna obligatoria (`NOT NULL`) o que admite `NULL` |
| `int?`, `DateTimeOffset?` | Columna que admite `NULL` |
| `enum` | Columna numérica |
| Propiedad `CategoriaId` + navegación `Categoria` | Clave ajena |

Lo que la convención no sabe se configura con la **API fluida**, en una clase por entidad ([IncidenciaConfiguracion.cs](../../src/day-03/GestorIncidencias.Infrastructure/Persistencia/Configuraciones/IncidenciaConfiguracion.cs)):

```csharp
public class IncidenciaConfiguracion : IEntityTypeConfiguration<Incidencia>
{
    public void Configure(EntityTypeBuilder<Incidencia> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Titulo).IsRequired().HasMaxLength(Incidencia.TituloLongitudMaxima);
        builder.Property(i => i.Estado).HasConversion<string>().HasMaxLength(20);   // enum como texto
        builder.Ignore(i => i.EstaAbierta);                                          // calculada: no se guarda
        // relaciones e índices: capítulos 2 y 6
    }
}
```

Con una base de datos relacional se añadiría también `builder.ToTable("Incidencias")` para fijar el nombre de la tabla (InMemory no tiene tablas, así que ese método no existe para él).

### ¿API fluida o atributos?

También se podría escribir `[MaxLength(120)]` o `[Table("Incidencias")]` en la entidad. **No lo hacemos** porque la entidad está en **Domain**, y Domain no debe depender de EF Core (regla de dependencia, día 2). Con la API fluida:

- El dominio no tiene ni un `using Microsoft.EntityFrameworkCore`.
- Todo el mapeo está en un sitio (Infrastructure/Persistencia/Configuraciones).
- Hay opciones que **solo** existen en la API fluida (claves ajenas sombra, campos privados, conversiones, `HasData`...).

> Fijaos en que la configuración usa las **constantes del dominio** (`Incidencia.TituloLongitudMaxima`). Una única fuente de verdad: la misma constante valida en el dominio, en el formulario (día 2) y define el tamaño de la columna.

## 1.7 Conversiones de valor: enums como número o como texto

Una **conversión** dice cómo se guarda una propiedad cuyo tipo no existe en la base de datos. El caso más habitual son los enums:

```csharp
builder.Property(i => i.Estado).HasConversion<string>();   // "Abierta", "EnCurso"...
// Prioridad: sin configurar → número (0, 1, 2, 3)
```

| | Como número (por defecto) | Como texto (`HasConversion<string>()`) |
|---|---|---|
| Legible en la tabla | ❌ `2` | ✅ `'Alta'` |
| Ocupa | Poco | Más |
| `ORDER BY` en SQL | ✅ Orden del enum: Baja < Media < Alta < Crítica | ❌ **Alfabético**: Alta, Baja, Crítica, Media |
| Renombrar un valor del enum | ✅ Sin efecto | ❌ Rompe los datos existentes |
| Reordenar valores del enum | ❌ Rompe los datos existentes | ✅ Sin efecto |

Por eso en el proyecto **`Estado` va como texto** (se filtra, no se ordena, y se lee bien en la tabla) y **`Prioridad` como número** (el listado **ordena** por prioridad).

> ⚠️ **Una trampa de InMemory:** el día 2, la prioridad estaba guardada como texto y el listado salía bien ordenado (Crítica, Alta, Media, Baja). Es porque InMemory ordena **objetos C#**. Con SQL Server, el mismo código habría ordenado alfabéticamente. Otro motivo para no fiarse de InMemory más allá de los ejemplos.

## 1.8 Ver el SQL (con una base de datos real)

Con un proveedor relacional, cada sentencia aparece en el log si se activa esta categoría en `appsettings.Development.json`:

```json
"Logging": {
  "LogLevel": {
    "Microsoft.EntityFrameworkCore.Database.Command": "Information"
  }
}
```

```
info: Microsoft.EntityFrameworkCore.Database.Command[20101]
      Executed DbCommand (2ms) [Parameters=[@estado='Abierta' (Size = 20)], CommandType='Text', CommandTimeout='30']
      SELECT COUNT(*)
      FROM [Incidencias] AS [i]
      WHERE [i].[Estado] = @estado
```

**Regla para los proyectos reales: no se da por buena una consulta sin haber mirado su SQL.** Es la herramienta principal del capítulo 6. Otras formas de verlo:

| Forma | Uso |
|---|---|
| `consulta.ToQueryString()` | Devuelve el SQL de un `IQueryable` sin ejecutarlo (para depurar) |
| `opciones.LogTo(Console.WriteLine)` | Log "rápido" sin pasar por `ILogger` (aplicaciones de consola) |
| SQL Server Profiler / Extended Events | Lo que llega realmente al servidor |

Con InMemory no hay SQL, así que en el curso **el SQL de los documentos es orientativo**: lo que EF Core generaría para SQL Server.

## 1.9 La aplicación al arrancar

[InicializadorBaseDatos.cs](../../src/day-03/GestorIncidencias.Infrastructure/Persistencia/InicializadorBaseDatos.cs) sustituye al `DatosDemoInitializer` del día 2:

```csharp
await db.Database.EnsureCreatedAsync(cancellationToken);    // crea la BD e inserta los HasData (categorías)

if (!opciones.Value.CargarDatosDemo)
    return;

db.Incidencias.AddRange(impresora, error500, correo, vpn);  // las 4 de siempre (Id 1 a 4), con comentarios
db.Incidencias.AddRange(historico);                         // 200 cerradas, para tener volumen
await db.SaveChangesAsync(cancellationToken);               // UNA unidad de trabajo
```

| Clave de configuración | Development | Producción | Para qué |
|---|---|---|---|
| `Incidencias:CargarDatosDemo` | `true` | `false` | Sembrar incidencias de ejemplo |
| `Incidencias:IncidenciasHistoricasDemo` | `200` | `0` | Volumen de datos para ver la paginación |

> `EnsureCreated` crea la base de datos **a partir del modelo**, sin migraciones. Con InMemory es justo lo que necesitamos. **Con una base de datos real no se usa**: no sabe evolucionar un esquema que ya existe. Para eso están las migraciones (capítulo 3).

## Referencias

> Enlaces comprobados el 7 de octubre de 2026.

- [Duración, configuración e inicialización de DbContext](https://learn.microsoft.com/es-es/ef/core/dbcontext-configuration/) — Tiempo de vida Scoped, `AddDbContext` y opciones.
- [Proveedor de bases de datos en memoria](https://learn.microsoft.com/es-es/ef/core/providers/in-memory/) — Sus limitaciones; Microsoft lo desaconseja para pruebas.
- [Conversiones de valores](https://learn.microsoft.com/es-es/ef/core/modeling/value-conversions) — Enums como texto y otros conversores incluidos.
- [Configuración masiva de modelos](https://learn.microsoft.com/es-es/ef/core/modeling/bulk-configuration) — Convenciones aplicadas a todo el modelo.
- [Registro sencillo](https://learn.microsoft.com/es-es/ef/core/logging-events-diagnostics/simple-logging) — `LogTo`, `EnableSensitiveDataLogging` y `EnableDetailedErrors`.
