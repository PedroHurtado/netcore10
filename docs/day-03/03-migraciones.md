# 3. Migraciones

> **Capítulo solo teórico.** En el curso usamos InMemory, que no tiene esquema y por tanto no usa migraciones. Aquí explicamos qué son, para qué sirven, cómo se crean y cómo se llevan a producción, con ejemplos de **SQL Server**, que es donde las usaréis.

## 3.1 El problema

Con InMemory, cambiar una entidad es gratis. Con una base de datos real, **cada cambio en el modelo exige cambiar las tablas**: en tu equipo, en el de tus compañeros, en preproducción y en producción, **sin perder los datos que ya hay**.

La forma tradicional era "el script que se pasa a sistemas": un `.sql` escrito a mano, enviado por correo, que alguien ejecuta (o no) en cada entorno. Problemas conocidos:

- Nadie sabe con certeza **en qué versión** está cada base de datos.
- El script y el código que lo necesita **no viajan juntos**.
- Un script olvidado en un entorno se descubre cuando la aplicación falla.

## 3.2 La idea

Una **migración** es una **clase C#** con dos métodos: `Up` (aplicar el cambio) y `Down` (deshacerlo). La genera EF Core **comparando** el modelo actual con una "foto" del modelo de la migración anterior (el *snapshot*).

```
 Modelo C# (entidades + configuración)                Snapshot (foto del modelo
              │                                       en la última migración)
              └────────────── dotnet ef migrations add ──────┘
                                       │ compara
                                       ▼
                     20261007091500_CategoriasYComentarios.cs
                         Up():   AddColumn, CreateTable, InsertData, CreateIndex...
                         Down(): DropTable, DropColumn...
                                       │
                     dotnet ef database update   ·   script SQL   ·   bundle
                                       ▼
                     Base de datos  +  fila en __EFMigrationsHistory
```

**Para qué sirven:**

| Ventaja | Por qué |
|---|---|
| **Esquema versionado en Git** | La migración se sube junto al código que la necesita: mismo *commit*, misma revisión |
| **Cada BD sabe en qué versión está** | Tabla `__EFMigrationsHistory` con las migraciones aplicadas |
| **Repetible en todos los entornos** | Los mismos pasos, en el mismo orden, en desarrollo, preproducción y producción |
| **Revisable** | Se puede generar el script SQL para que lo revise y ejecute el DBA |
| **Reversible** | El método `Down` deshace el cambio |

## 3.3 Cómo se hace: las herramientas

| Pieza | Para qué |
|---|---|
| Herramienta **`dotnet-ef`** (mejor como herramienta *local* del repositorio, con la misma versión que EF Core) | Los comandos `dotnet ef ...` |
| Paquete **`Microsoft.EntityFrameworkCore.Design`** en el proyecto de inicio (Web) | `dotnet ef` **arranca la aplicación** para obtener el `DbContext` ya configurado |
| Paquete del **proveedor** (`...SqlServer`) en Infrastructure | El SQL de cada migración lo genera el proveedor |

Como el `DbContext` está en **Infrastructure** y `Program.cs` en **Web**, los comandos llevan dos parámetros:

| Parámetro | Valor | Significado |
|---|---|---|
| `--project` | `GestorIncidencias.Infrastructure` | Dónde **está** el `DbContext` y dónde se **escriben** las migraciones |
| `--startup-project` | `GestorIncidencias.Web` | Qué aplicación se **arranca** para obtener configuración y servicios |

## 3.4 Los comandos

| Comando | Qué hace |
|---|---|
| `dotnet ef migrations add <Nombre>` | Crea una migración con los cambios del modelo desde la anterior |
| `dotnet ef migrations list` | Lista las migraciones y marca con `(Pending)` las que faltan en la BD |
| `dotnet ef migrations remove` | Borra la **última** migración (solo si **no** se ha aplicado) |
| `dotnet ef database update` | Aplica las migraciones pendientes |
| `dotnet ef database update <Nombre>` | Lleva la BD **exactamente** a esa migración (hacia delante o hacia atrás, ejecutando `Down`) |
| `dotnet ef migrations script --idempotent` | Genera el SQL sin ejecutarlo (para el DBA) |
| `dotnet ef migrations has-pending-model-changes` | ¿Hay cambios en el modelo sin migración? (útil en integración continua) |
| `dotnet ef migrations bundle` | Crea un ejecutable que aplica las migraciones (despliegue) |
| `dotnet ef dbcontext scaffold` | Genera entidades a partir de una BD **existente** (capítulo 5) |

## 3.5 Cómo sería en nuestro proyecto

Si el Gestor de Incidencias usara SQL Server, el día 3 habría producido estas migraciones:

| Migración | Qué contendría |
|---|---|
| `Inicial` | La tabla `Incidencias` tal como era el modelo del día 2 |
| `CategoriasYComentarios` | Columna `CategoriaId`, tablas `Categorias` y `Comentarios`, las 5 categorías de `HasData`, índices y claves ajenas |
| `IndiceEstadoPrioridad` | El índice para el listado (capítulo 6) |

Cada migración son **tres ficheros**:

| Fichero | Contenido | ¿Se edita? |
|---|---|---|
| `20261007091500_CategoriasYComentarios.cs` | `Up()` y `Down()` | A veces (3.7) |
| `20261007091500_CategoriasYComentarios.Designer.cs` | Metadatos: cómo era el modelo en ese momento | **Nunca** |
| `IncidenciasDbContextModelSnapshot.cs` (uno para todas) | Foto del modelo **actual**: con ella se compara la próxima vez | **Nunca** a mano |

Fragmento de `CategoriasYComentarios.Up()`:

```csharp
migrationBuilder.AddColumn<int>(name: "CategoriaId", table: "Incidencias", type: "int", nullable: true);

migrationBuilder.CreateTable(
    name: "Comentarios",
    columns: table => new
    {
        Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
        Texto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
        Autor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
        Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
        IncidenciaId = table.Column<int>(type: "int", nullable: false)                 // ← la propiedad sombra
    },
    constraints: table =>
    {
        table.PrimaryKey("PK_Comentarios", x => x.Id);
        table.ForeignKey("FK_Comentarios_Incidencias_IncidenciaId", x => x.IncidenciaId,
            principalTable: "Incidencias", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
    });

migrationBuilder.InsertData(
    table: "Categorias",
    columns: new[] { "Id", "Nombre" },
    values: new object[,] { { 1, "Hardware" }, { 2, "Software" }, /* ... */ });
```

Fijaos en `nullable: true` en `CategoriaId`: como la relación es **opcional**, las incidencias que ya existían se quedan con `NULL`. Si fuera obligatoria (`int`), la migración necesitaría un **valor por defecto** para las filas existentes. **Pensar en los datos que ya hay es la parte difícil de una migración.**

## 3.6 La tabla `__EFMigrationsHistory`

```
 MigrationId                               ProductVersion
 ───────────────────────────────────────── ──────────────
 20261007091200_Inicial                    10.0.12
 20261007091500_CategoriasYComentarios     10.0.12
 20261007091800_IndiceEstadoPrioridad      10.0.12
```

Así sabe EF Core qué migraciones tiene **esta** base de datos y cuáles le faltan.

> ⚠️ **Nunca se modifica ni se borra una migración que ya se ha aplicado en otro entorno** (o que otro compañero ya ha aplicado). Si hay un error, se crea **otra migración** que lo corrija.

## 3.7 Revisar siempre lo generado

EF Core genera **cambios de esquema**. Lo que no puede adivinar son tus **intenciones con los datos**:

| Situación | Lo que puede generar EF Core | Lo que necesitas |
|---|---|---|
| Renombrar `Descripcion` → `Detalle` | `DropColumn` + `AddColumn` (**¡se pierden los datos!**) | `RenameColumn` |
| Partir `Nombre` en `Nombre` y `Apellidos` | Dos columnas nuevas vacías | Un `migrationBuilder.Sql("UPDATE ...")` que reparta los datos |
| Columna nueva obligatoria en una tabla con datos | `AddColumn(..., nullable: false, defaultValue: "")` | Decidir el valor adecuado para las filas existentes |

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<string>("Solicitante", "Incidencias", type: "nvarchar(100)", nullable: true);
    migrationBuilder.Sql("UPDATE Incidencias SET Solicitante = N'(no consta)' WHERE Solicitante IS NULL");
}
```

**Regla: se revisan el `Up` y el `Down` antes de aplicar una migración.** Se tarda un minuto y evita perder datos.

## 3.8 Cómo se aplican en cada entorno

| Forma | Cómo | Desarrollo | Producción |
|---|---|---|---|
| **`dotnet ef database update`** | Desde el equipo del desarrollador | ✅ | ❌ Requiere el SDK y el código fuente |
| **`Migrate()` al arrancar** | `await db.Database.MigrateAsync()` en el arranque | ✅ Cómodo | ❌ Varias instancias arrancando a la vez, la cuenta de la aplicación necesitaría permisos para cambiar el esquema y, si falla, la aplicación no arranca |
| **Script SQL idempotente** | `dotnet ef migrations script --idempotent -o cambios.sql` → lo revisa y ejecuta el DBA | Para revisar | ✅ **Lo habitual en la Administración** |
| **Bundle** | `dotnet ef migrations bundle` → `efbundle.exe --connection "..."` | — | ✅ En *pipelines* de despliegue |

Un script **idempotente** comprueba `__EFMigrationsHistory` antes de cada migración y solo aplica las que faltan, así que se puede ejecutar en cualquier entorno sin saber en qué versión está:

```sql
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261007091500_CategoriasYComentarios')
BEGIN
    ALTER TABLE [Incidencias] ADD [CategoriaId] int NULL;
END;
...
```

### "He cambiado el modelo y se me ha olvidado la migración"

Desde EF Core 9, `Migrate()` y `database update` comprueban que el modelo coincide con la última migración. Si no coincide, **fallan** con este mensaje:

```
The model for context 'IncidenciasDbContext' has pending changes. Add a new migration before updating the database.
```

Es una protección: mejor un error claro al desplegar que un `Invalid column name` en mitad de una petición. Solución: `dotnet ef migrations add <Nombre>`.

## 3.9 Flujo de trabajo en equipo

1. Cambias la entidad o su configuración.
2. `dotnet ef migrations add NombreDescriptivo` (`AnadirSolicitante`, `IndiceEstadoPrioridad`...).
3. **Revisas** el `Up` y el `Down`.
4. `dotnet ef database update` en tu base de datos de desarrollo.
5. Pruebas.
6. *Commit* de **todo junto**: entidad, configuración, migración **y** snapshot.

> **Conflicto típico:** dos compañeros crean una migración a la vez en ramas distintas. Al unir las ramas, el *snapshot* puede quedar incoherente. Solución: uno de los dos quita la suya (`migrations remove`, si no la ha aplicado fuera de su equipo), trae los cambios del otro y la vuelve a crear.

## 3.10 ¿Y con una base de datos legacy?

Con la base de datos de SIREI **no se empieza con migraciones**: el esquema ya existe, lo gestiona su DBA y quizá lo usan otras aplicaciones. Lo habitual es:

1. Generar las entidades desde la BD con `dotnet ef dbcontext scaffold` (capítulo 5).
2. Seguir gestionando el esquema como hasta ahora.
3. Adoptar migraciones, si se adoptan, cuando la aplicación nueva pase a ser **la dueña** del esquema.

## Referencias

> Enlaces comprobados el 7 de octubre de 2026.

- [Descripción general de las migraciones](https://learn.microsoft.com/es-es/ef/core/managing-schemas/migrations/)
- [Administración de migraciones](https://learn.microsoft.com/es-es/ef/core/managing-schemas/migrations/managing) — Añadir, quitar, personalizar el código y SQL a mano.
- [Aplicación de migraciones](https://learn.microsoft.com/es-es/ef/core/managing-schemas/migrations/applying) — Scripts SQL, *bundles*, `Migrate()` en tiempo de ejecución y sus riesgos.
- [Uso de un proyecto de migraciones separado](https://learn.microsoft.com/es-es/ef/core/managing-schemas/migrations/projects)
- [Referencia de herramientas de EF Core (CLI de .NET)](https://learn.microsoft.com/es-es/ef/core/cli/dotnet) — Todos los comandos `dotnet ef`.
- [Creación de DbContext en tiempo de diseño](https://learn.microsoft.com/es-es/ef/core/cli/dbcontext-creation) — Cómo obtiene `dotnet ef` el contexto a partir del proyecto de inicio.
- [Cambios importantes en EF Core 9](https://learn.microsoft.com/es-es/ef/core/what-is-new/ef-core-9.0/breaking-changes) — Excepción por cambios pendientes en el modelo al aplicar migraciones.
- [Tutorial: Instalación y uso de herramientas locales de .NET](https://learn.microsoft.com/es-es/dotnet/core/tools/local-tools-how-to-use)
