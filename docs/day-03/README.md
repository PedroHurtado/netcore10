# Día 3 — Datos y persistencia con EF Core, y análisis de aplicaciones legacy

**Módulos del temario:** Módulo 4 completo (Entity Framework Core: configuración y migraciones · patrones de acceso a datos · migración de DataSets y DataTables · consultas y rendimiento) y Módulo 3, 1ª parte (análisis de aplicaciones legacy: qué migrar y qué refactorizar · migración completa frente a coexistencia · servicios compartidos).

## Enfoque del día

- Se mantiene el estilo del día 2: **teoría breve sobre el código ya terminado** de [src/day-03](../../src/day-03/), con demostraciones en vivo.
- **Seguimos con EF Core InMemory: no se instala nada.** Lo que cambia con una base de datos real (SQL Server) se explica en cada capítulo, y el SQL de los documentos es el que generaría EF Core contra SQL Server.
- Las **migraciones se explican, no se practican**: qué son, para qué sirven, cómo se crean y cómo se llevan a producción ([capítulo 3](03-migraciones.md)).
- La práctica ronda el 30 % del tiempo con **tres laboratorios**: dos guiados con todo el código (comprobado paso a paso, con tablas de errores típicos) y uno de análisis, en grupo y sin código.
- El último bloque **conecta con la migración**: el análisis de SIREI que empieza hoy es la entrada del caso práctico del día 5.

## Objetivos

Al terminar el día el alumno será capaz de:

1. Explicar cómo se configura EF Core (proveedor, cadena de conexión, `DbContext`, API fluida) sin que el dominio dependa de EF Core, y qué limitaciones tiene InMemory frente a una base de datos real.
2. Modelar relaciones N:1 y 1:N (incluida una colección dentro de un agregado) y cargar datos relacionados de la forma adecuada.
3. Explicar qué son las migraciones, cómo se crean y revisan, y cómo se aplican en cada entorno.
4. Separar lecturas (consultas con proyección a DTO) de escrituras (repositorio con entidades).
5. Traducir código ADO.NET con `DataSet`/`DataTable` a EF Core y elegir una estrategia para hacerlo en una aplicación real.
6. Revisar una consulta con criterios de rendimiento: N+1, filtrado en memoria, columnas de más, paginación, índices.
7. Hacer el inventario de una aplicación legacy, clasificar sus piezas y proponer una estrategia (completa o incremental).

## Agenda (9:00 – 14:00)

| Hora | Bloque | Tipo | Material |
|---|---|---|---|
| 9:00 – 9:10 | Repaso del día 2 y objetivos | — | [Resumen día 2](../day-02/07-resumen.md) |
| 9:10 – 9:40 | **EF Core: configuración** | Teoría + demo | [01](01-ef-core-configuracion.md) |
| 9:40 – 10:10 | **Relaciones**: categorías y comentarios | Teoría + demo | [02](02-relaciones.md) |
| 10:10 – 10:35 | **Migraciones**: qué son, cómo se hacen y para qué sirven | Teoría | [03](03-migraciones.md) |
| 10:35 – 11:10 | **Lab 1** — Cambiar la categoría de una incidencia | Práctica guiada | [Lab 1](labs/lab-01-cambiar-categoria.md) |
| 11:10 – 11:25 | *Descanso* | | |
| 11:25 – 11:45 | **Patrones de acceso a datos**: repositorio, consultas, CQRS ligero | Teoría + debate | [04](04-patrones-acceso-datos.md) |
| 11:45 – 12:05 | **De DataSet y DataTable a EF Core** | Teoría | [05](05-dataset-a-ef-core.md) |
| 12:05 – 12:35 | **Rendimiento de consultas** | Teoría + demo | [06](06-rendimiento-consultas.md) |
| 12:35 – 13:05 | **Lab 2** — De DataTable a EF Core: el informe por prioridad | Práctica guiada | [Lab 2](labs/lab-02-informe-prioridad.md) |
| 13:05 – 13:30 | **Análisis de aplicaciones legacy** y estrategias de migración | Teoría + debate | [07](07-analisis-legacy.md) |
| 13:30 – 13:52 | **Lab 3** — Primer análisis de SIREI | Práctica en grupo | [Lab 3](labs/lab-03-analisis-sirei.md) |
| 13:52 – 14:00 | Repaso y avance del día 4 | — | [Resumen](08-resumen.md) |

### Guion de las demostraciones

| Bloque | Qué enseñar en directo |
|---|---|
| 01 Configuración | `DependencyInjection.cs`: la línea `UseInMemoryDatabase` y cómo sería con `UseSqlServer` · `IncidenciasDbContext` y su tiempo de vida · `IncidenciaConfiguracion`: Estado como texto y Prioridad como número, y por qué (la trampa de InMemory con el orden) · `InicializadorBaseDatos`: `EnsureCreated` y una sola unidad de trabajo |
| 02 Relaciones | `Incidencia.cs`: `CategoriaId`/`Categoria`, `_comentarios`, `AgregarComentario` · configuración `HasOne`/`HasMany`, propiedad sombra, `UsePropertyAccessMode` · `HasData` de categorías · añadir un comentario en `/Incidencias/Detalle/2` · comentar una cerrada por la API (409) · crear con `"categoriaId": 99` (400) |
| 03 Migraciones | Sin código en ejecución: recorrer el fragmento de migración del capítulo, la tabla `__EFMigrationsHistory`, la tabla de formas de aplicarlas en cada entorno y el script idempotente |
| 04 Patrones | `IIncidenciaRepository` (lo que **no** tiene) frente a `IIncidenciaConsultas` · la expresión `ADto` · `LeerTrasGuardarAsync` · debate: ¿repositorio genérico? |
| 06 Rendimiento | Comparar el `HomeController` del día 2 (cargar todo y contar) con el del día 3 · `EfIncidenciaConsultas.ListarAsync`: filtro, recuento, orden estable, `Skip`/`Take`, proyección · `?tamano=100000` en la API (se limita a 100) · escribir en la pizarra un N+1 y su solución |
| 07 Legacy | Dibujar en la pizarra el esquema *Strangler Fig* con SIREI · la guía de decisión oficial (completa frente a incremental) |

## Proyecto de ejemplo

La solución completa al final del día está en [src/day-03](../../src/day-03/). Parte de `src/day-02` e incorpora ya el **Reabrir** del lab 2 del día 2. Los laboratorios de hoy **no** están incluidos: los hace cada alumno sobre su copia.

```
src/day-03/
├── GestorIncidencias.Domain/
│   ├── Categorias/Categoria.cs            ← NUEVO: catálogo
│   └── Incidencias/
│       ├── Incidencia.cs                  ← + CategoriaId/Categoria, Comentarios, AgregarComentario, Reabrir
│       └── Comentario.cs                  ← NUEVO: parte del agregado Incidencia
├── GestorIncidencias.Application/
│   ├── Abstracciones/
│   │   ├── IIncidenciaRepository.cs       ← solo ESCRITURA (agregado completo)
│   │   └── IIncidenciaConsultas.cs        ← NUEVO: LECTURA (DTOs proyectados)
│   ├── Comun/Pagina.cs                    ← NUEVO: resultados paginados
│   ├── Configuracion/IncidenciasOptions.cs ← + IncidenciasHistoricasDemo
│   └── Incidencias/                       ← DTOs ampliados (detalle, resumen, categorías), servicio con Comentar/Reabrir
├── GestorIncidencias.Infrastructure/      ← EF Core InMemory (como los días 1 y 2)
│   └── Persistencia/
│       ├── IncidenciasDbContext.cs        ← + Categorias
│       ├── Configuraciones/               ← Incidencia (relaciones, índice), Comentario, Categoria (HasData)
│       ├── EfIncidenciaRepository.cs      ← Include(Comentarios)
│       ├── EfIncidenciaConsultas.cs       ← NUEVO: proyecciones, paginación, GroupBy
│       └── InicializadorBaseDatos.cs      ← NUEVO: EnsureCreated + datos de demostración (sustituye a DatosDemoInitializer)
└── GestorIncidencias.Web/
    ├── Controllers/                       ← paginación, categorías, Comentar, Reabrir; API paginada
    └── Views/ y Pages/                    ← categoría, nº de comentarios, paginador, comentarios en la ficha
```

Para ejecutarlo:

```bash
cd src/day-03
dotnet run --project GestorIncidencias.Web
```

| URL | Qué muestra |
|---|---|
| http://localhost:5196/ | Panel con cifras agrupadas y tabla **Por categoría** |
| /Incidencias | Listado paginado (20 por página) con categoría y nº de comentarios |
| /Incidencias/Detalle/2 | Ficha con comentarios y formulario para añadir uno |
| /Incidencias/Crear | Formulario con desplegable de categorías |
| /Paginas/Incidencias | Listado paginado con Razor Pages |
| /api/incidencias?estado=Cerrada&pagina=2&tamano=10 | API paginada |
| /api/incidencias/2 | Incidencia **con sus comentarios** |
| /api/incidencias/categorias | Catálogo de categorías |
| /openapi/v1.json | Documento OpenAPI (solo Development) |

En Development se cargan 4 incidencias de ejemplo (Id 1 a 4, con categoría y comentarios) y 200 cerradas de histórico, y el límite de incidencias abiertas es 10. Como seguimos con InMemory, **cada arranque empieza de cero**.

## Referencias

> Enlaces comprobados el 7 de octubre de 2026. Cada documento de teoría y cada laboratorio tiene su propia sección de referencias.

- [Documentación de Entity Framework Core](https://learn.microsoft.com/es-es/ef/core/)
- [Descripción general de las migraciones](https://learn.microsoft.com/es-es/ef/core/managing-schemas/migrations/)
- [Introducción al rendimiento](https://learn.microsoft.com/es-es/ef/core/performance/)
- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0)
