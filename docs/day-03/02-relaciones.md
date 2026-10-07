# 2. Relaciones entre entidades

Hasta el día 2 había **una sola tabla**. Una aplicación real (SIREI, Noticom) tiene decenas, relacionadas entre sí. Hoy el Gestor de Incidencias incorpora dos relaciones, las dos más habituales:

```
 ┌──────────────┐         ┌─────────────────────┐         ┌──────────────────┐
 │  Categorias  │ 1     N │     Incidencias     │ 1     N │   Comentarios    │
 │──────────────│◀────────│─────────────────────│────────▶│──────────────────│
 │ Id      PK   │         │ Id            PK    │         │ Id          PK   │
 │ Nombre  UQ   │         │ Titulo              │         │ Texto            │
 └──────────────┘         │ ...                 │         │ Autor            │
                          │ CategoriaId   FK ?  │         │ Fecha            │
                          └─────────────────────┘         │ IncidenciaId FK  │
                                                          └──────────────────┘
    N:1 OPCIONAL: una incidencia puede             1:N OBLIGATORIA: un comentario
    no tener categoría (CategoriaId NULL)          siempre pertenece a una incidencia
    Borrar categoría → SET NULL                    Borrar incidencia → CASCADE
```

## 2.1 Vocabulario mínimo

| Término | Significado | En el proyecto |
|---|---|---|
| **Principal** | La entidad "de la que se depende" (lado 1) | `Categoria`, `Incidencia` (respecto a sus comentarios) |
| **Dependiente** | La que tiene la clave ajena (lado N) | `Incidencia` (respecto a su categoría), `Comentario` |
| **Clave ajena (FK)** | Columna que apunta a la clave del principal | `Incidencias.CategoriaId`, `Comentarios.IncidenciaId` |
| **Navegación de referencia** | Propiedad que apunta a **una** entidad | `Incidencia.Categoria` |
| **Navegación de colección** | Propiedad que apunta a **muchas** | `Incidencia.Comentarios` |
| **Propiedad sombra** | Existe en el modelo de EF Core y en la tabla, pero **no en la clase** | `IncidenciaId` en `Comentario` |

## 2.2 Relación N:1 opcional — Incidencia → Categoría

### Las clases

[Categoria.cs](../../src/day-03/GestorIncidencias.Domain/Categorias/Categoria.cs) es un catálogo sencillo:

```csharp
public class Categoria
{
    private Categoria() { }                        // para EF Core
    public Categoria(string nombre) => Nombre = nombre.Trim();

    public int Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
}
```

Y en [Incidencia.cs](../../src/day-03/GestorIncidencias.Domain/Incidencias/Incidencia.cs):

```csharp
public int? CategoriaId { get; private set; }        // la FK: int? → la relación es OPCIONAL
public Categoria? Categoria { get; private set; }    // la navegación
```

¿Por qué las dos? `CategoriaId` permite **asignar** una categoría sin cargarla (`Incidencia.Crear(..., categoriaId: 2)`) y **filtrar** sin `JOIN`. `Categoria` permite **leer** su nombre en consultas (`i.Categoria.Nombre`).

> 💡 Fijaos en que `Categoria` **no** tiene una colección `Incidencias`. No la necesitamos: nadie pregunta "dame la categoría con todas sus incidencias" (serían miles). Una navegación que no se usa solo invita a cargar datos de más. **Las relaciones se pueden declarar en un solo sentido.**

### La configuración

```csharp
builder.HasOne(i => i.Categoria)          // una incidencia tiene UNA categoría...
    .WithMany()                           // ...y una categoría tiene MUCHAS incidencias (sin navegación de vuelta)
    .HasForeignKey(i => i.CategoriaId)
    .OnDelete(DeleteBehavior.SetNull);    // si se borra la categoría, CategoriaId = NULL
```

### Datos de referencia con `HasData`

Las categorías son **datos que la aplicación necesita para funcionar**, en todos los entornos. Se declaran en el modelo ([CategoriaConfiguracion.cs](../../src/day-03/GestorIncidencias.Infrastructure/Persistencia/Configuraciones/CategoriaConfiguracion.cs)) y, con una base de datos real, viajan **dentro de la migración** (capítulo 3). Con InMemory las inserta `EnsureCreated` al arrancar:

```csharp
builder.HasData(
    new { Id = 1, Nombre = "Hardware" },
    new { Id = 2, Nombre = "Software" },
    new { Id = 3, Nombre = "Redes y comunicaciones" },
    new { Id = 4, Nombre = "Cuentas y accesos" },
    new { Id = 5, Nombre = "Otros" });
```

- Tipos **anónimos** porque `Categoria` no tiene setters públicos.
- Los `Id` son **fijos**: EF Core los necesita para saber, en la siguiente migración, si una fila ha cambiado o se ha borrado.

| | `HasData` (datos de **referencia**) | Inicializador (datos de **demostración**) |
|---|---|---|
| Ejemplo | Categorías, tipos de documento, provincias | Incidencias de ejemplo |
| ¿Dónde viven? | En el modelo; en SQL Server, en la migración (`InsertData`) | En código que se ejecuta al arrancar |
| ¿En producción? | **Sí** | **No** (`CargarDatosDemo: false`) |
| ¿Cómo se cambian? | Se edita `HasData` y se crea una migración | Se edita el código |

## 2.3 Relación 1:N dentro de un agregado — Incidencia → Comentarios

### Un agregado: la incidencia manda

Un comentario **no tiene sentido sin su incidencia** y además hay una regla: *no se puede comentar una incidencia cerrada*. ¿Dónde ponemos esa regla? En la **raíz del agregado** (la incidencia). Así nadie puede añadir un comentario saltándosela:

```csharp
public class Incidencia
{
    private readonly List<Comentario> _comentarios = [];                        // campo privado
    public IReadOnlyCollection<Comentario> Comentarios => _comentarios.AsReadOnly();   // solo lectura

    public Resultado<Comentario> AgregarComentario(string texto, string autor, DateTimeOffset fecha)
    {
        if (Estado == EstadoIncidencia.Cerrada)
            return Resultado<Comentario>.Fallo("No se pueden añadir comentarios a una incidencia cerrada.", TipoError.Conflicto);
        // ... validación de texto y autor ...
        var comentario = new Comentario(texto, autor, fecha);   // constructor internal
        _comentarios.Add(comentario);
        return Resultado<Comentario>.Ok(comentario);
    }
}
```

| Decisión | Por qué |
|---|---|
| `Comentarios` es `IReadOnlyCollection` | `incidencia.Comentarios.Add(...)` **no compila** fuera de la clase |
| El constructor de `Comentario` es `internal` | Solo el ensamblado Domain (en la práctica, `Incidencia`) puede crear comentarios |
| No hay `DbSet<Comentario>` ni `IComentarioRepository` | Los comentarios se leen y guardan **a través de su incidencia** |
| `Comentario` no tiene `IncidenciaId` | El dominio no lo necesita: un comentario ya "vive dentro" de su incidencia |

### La configuración: campo privado y propiedad sombra

```csharp
builder.HasMany(i => i.Comentarios)
    .WithOne()                              // Comentario no tiene navegación hacia Incidencia
    .HasForeignKey("IncidenciaId")          // FK como PROPIEDAD SOMBRA (string, no lambda)
    .IsRequired()                           // un comentario siempre tiene incidencia
    .OnDelete(DeleteBehavior.Cascade);      // borrar la incidencia borra sus comentarios

// La propiedad Comentarios no tiene setter: EF Core usa el campo _comentarios.
builder.Navigation(i => i.Comentarios).UsePropertyAccessMode(PropertyAccessMode.Field);
```

Una **propiedad sombra** es una columna que existe en la tabla y que EF Core gestiona, pero que no aparece en la clase. Si alguna vez hay que consultarla, se usa `EF.Property`:

```csharp
db.Set<Comentario>().Where(c => EF.Property<int>(c, "IncidenciaId") == id)
```

(Es exactamente lo que hace `EfIncidenciaConsultas.ObtenerDetalleAsync`.)

### Guardar un comentario

El caso de uso ([IncidenciaService.cs](../../src/day-03/GestorIncidencias.Application/Incidencias/IncidenciaService.cs)) sigue el patrón de siempre: **cargar → pedir a la entidad → guardar**.

```csharp
public Task<Resultado<IncidenciaDto>> ComentarAsync(int id, ComentarIncidenciaComando comando, CancellationToken ct = default) =>
    ModificarAsync(id, incidencia =>
    {
        var comentario = incidencia.AgregarComentario(comando.Texto, comando.Autor, reloj.GetUtcNow());
        return comentario.Exito ? Resultado.Ok() : Resultado.Fallo(comentario.Error!, comentario.Tipo!.Value);
    }, ct);
```

Al hacer `SaveChanges`, EF Core descubre el comentario nuevo en `_comentarios`. En SQL Server generaría:

```sql
INSERT INTO [Comentarios] ([Autor], [Fecha], [IncidenciaId], [Texto])
OUTPUT INSERTED.[Id]
VALUES (@p0, @p1, @p2, @p3);
```

Nadie ha escrito `IncidenciaId = 1`: **EF Core rellena la clave ajena** porque el comentario está en la colección de la incidencia 1.

## 2.4 Cargar datos relacionados

Por defecto, una consulta trae **solo la entidad pedida**: las navegaciones quedan vacías (`null` o colección vacía). Hay tres formas de cargarlas:

| Forma | Código | SQL | Cuándo |
|---|---|---|---|
| **Carga ansiosa** (*eager*) | `db.Incidencias.Include(i => i.Comentarios)` | `LEFT JOIN` en la misma consulta | Necesitas las entidades relacionadas para **modificarlas** |
| **Carga explícita** | `db.Entry(incidencia).Collection(i => i.Comentarios).LoadAsync()` | Una consulta más, cuando tú decides | Cargar algo "a posteriori" solo en algunos casos |
| **Carga diferida** (*lazy*) | Acceder a `incidencia.Comentarios` y que se cargue solo | Una consulta **cada vez** que tocas la navegación | ⚠️ Desactivada por defecto. Evitadla en web (capítulo 6: N+1) |
| **Proyección** | `Select(i => new { i.Titulo, Categoria = i.Categoria.Nombre })` | `JOIN` y solo las columnas pedidas | **Leer** para mostrar: la opción preferida |

En el proyecto:

- El **repositorio** (escritura) usa `Include`, porque carga el **agregado completo** para que la entidad aplique sus reglas:

  ```csharp
  db.Incidencias.Include(i => i.Comentarios).FirstOrDefaultAsync(i => i.Id == id, ct);
  ```

- Las **consultas** (lectura) usan **proyección**: `i.Categoria.Nombre` en un `Select` genera un `LEFT JOIN` sin necesidad de `Include` (capítulo 4).

> ⚠️ **Sin `Include` ni proyección, la navegación es `null`.** `incidencia.Categoria.Nombre` daría `NullReferenceException` aunque la incidencia tenga `CategoriaId = 2`. Con EF6 y *lazy loading* (Noticom) "funcionaba" sola; en EF Core hay que pedirla. Es una de las sorpresas más habituales al migrar.

## 2.5 Comportamiento al borrar

| `DeleteBehavior` | Al borrar el principal... | Uso típico |
|---|---|---|
| `Cascade` | Se borran los dependientes | Partes de un agregado (comentarios de una incidencia) |
| `SetNull` | La FK de los dependientes pasa a `NULL` | Relaciones opcionales (incidencias de una categoría) |
| `Restrict` / `NoAction` | Error: no se puede borrar mientras tenga dependientes | Proteger datos ("no borres una categoría en uso") |

> En aplicaciones de la Administración lo habitual es **no borrar nunca** (baja lógica con un campo `Activo` o `FechaBaja`). El comportamiento al borrar sigue siendo importante: es la red de seguridad si alguien borra a mano.

## 2.6 Otras relaciones (para reconocerlas)

| Relación | Ejemplo | En EF Core |
|---|---|---|
| **1:1** | Incidencia ↔ Informe de cierre | `HasOne(...).WithOne(...)`; la FK va en el dependiente |
| **N:M** | Incidencias ↔ Etiquetas | `HasMany(i => i.Etiquetas).WithMany()`: EF Core crea la tabla intermedia solo |
| **N:M con datos** | Incidencias ↔ Técnicos, con "fecha de asignación" | Entidad intermedia explícita (`Asignacion`) con dos relaciones 1:N |
| **Tipo propio** (*owned*) | Una `Direccion` (calle, CP, municipio) dentro de un expediente | `OwnsOne(e => e.Direccion)`: columnas en la misma tabla, sin `Id` propio |

## 2.7 Lo que vemos en la aplicación

| Dónde | Qué ver |
|---|---|
| `/Incidencias` | Columnas **Categoría** y **Coment.** (número de comentarios) |
| `/Incidencias/Detalle/2` | Categoría "Software" y dos comentarios |
| Ficha de una incidencia abierta | Formulario **Añadir comentario** (y el comentario aparece al momento) |
| Ficha de una incidencia **cerrada** | Sin formulario. Por la API: `POST /api/incidencias/{id}/comentarios` → **409** |
| `/Incidencias/Crear` | Desplegable de categorías leído de la tabla `Categorias` |
| `POST /api/incidencias` con `"categoriaId": 99` | **400** "No existe la categoría 99" (regla de **aplicación**: el dominio no puede consultar la BD) |

## Referencias

> Enlaces comprobados el 7 de octubre de 2026.

- [Introducción a las relaciones](https://learn.microsoft.com/es-es/ef/core/modeling/relationships)
- [Relaciones de uno a muchos](https://learn.microsoft.com/es-es/ef/core/modeling/relationships/one-to-many) — Opcionales y obligatorias, con y sin navegación de vuelta.
- [Propiedades de sombra e indexador](https://learn.microsoft.com/es-es/ef/core/modeling/shadow-properties) — `EF.Property` y claves ajenas sombra.
- [Campos de respaldo](https://learn.microsoft.com/es-es/ef/core/modeling/backing-field) — Colecciones expuestas como solo lectura sobre un campo privado.
- [Propagación de datos](https://learn.microsoft.com/es-es/ef/core/modeling/data-seeding) — `HasData` frente a `UseSeeding` y la inicialización personalizada.
- [Carga de datos relacionados](https://learn.microsoft.com/es-es/ef/core/querying/related-data/) — Carga diligente, explícita y diferida.
- [Tipos de entidad en propiedad](https://learn.microsoft.com/es-es/ef/core/modeling/owned-entities)
