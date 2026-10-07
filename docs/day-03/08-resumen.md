# 8. Resumen del día 3 y avance del día 4

## Lo que hemos aprendido

| Concepto | En una frase | Dónde está en el proyecto |
|---|---|---|
| Proveedor | EF Core habla con cada BD a través de un paquete; InMemory no es una BD real | `Infrastructure/DependencyInjection.cs` |
| Cadena de conexión | En `appsettings` (o secretos), leída con `GetConnectionString`, nunca con contraseña en el código | Capítulo 1.4 (InMemory no la necesita) |
| `DbContext` | Unidad de trabajo + mapa de identidad + seguimiento de cambios; uno por petición (Scoped) | `IncidenciasDbContext.cs` |
| API fluida | El mapeo fuera de la entidad: el dominio no conoce EF Core | `Persistencia/Configuraciones/` |
| Conversiones | Enum como texto o número (¡ojo al `ORDER BY`!) | `IncidenciaConfiguracion` |
| Relación N:1 opcional | FK `int?` + navegación; `SetNull` al borrar | `Incidencia.CategoriaId` / `Categoria` |
| Relación 1:N en un agregado | Colección de solo lectura sobre campo privado; FK sombra; `Cascade` | `Incidencia.Comentarios`, `AgregarComentario` |
| `HasData` | Datos de referencia del modelo (en SQL Server, dentro de la migración) | `CategoriaConfiguracion` |
| Migraciones | `Up`/`Down` generados comparando con el *snapshot*; versionados en Git | Capítulo 3 (InMemory no las usa) |
| `__EFMigrationsHistory` | Qué migraciones tiene **esta** BD | Capítulo 3 |
| Aplicar migraciones | `database update` en desarrollo; script idempotente revisado o *bundle* en producción | Capítulo 3 |
| CQRS ligero | Escribir con repositorio (entidades), leer con consultas (DTOs proyectados) | `IIncidenciaRepository` / `IIncidenciaConsultas` |
| DataSet → EF Core | Mismas ideas (estado de filas, actualizar), pero tipado y en el servidor | Capítulo 5 y Lab 2 |
| Relación en la práctica | Cambiar la clave ajena desde el dominio; comprobar que existe desde la aplicación | Lab 1 |
| Rendimiento | Mirar el SQL; filtrar/agregar en la BD; proyectar; paginar; evitar N+1; índices | `EfIncidenciaConsultas`, `Pagina<T>` |
| Migración legacy | Inventario → clasificar → estrategia (completa o *Strangler Fig*) | Capítulo 7 y Lab 3 |

## Chuleta: comandos de migraciones (para cuando trabajéis con SQL Server)

```bash
# Desde la carpeta de la solución; --project = dónde está el DbContext, --startup-project = la aplicación web
dotnet ef migrations add NombreDescriptivo  --project X.Infrastructure --startup-project X.Web   # tras cambiar el modelo
dotnet ef migrations list                   --project X.Infrastructure --startup-project X.Web   # ¿qué falta por aplicar?
dotnet ef database update                   --project X.Infrastructure --startup-project X.Web   # aplicar (desarrollo)
dotnet ef database update MigracionAnterior ...                                                  # deshacer hasta ahí (Down)
dotnet ef migrations remove                 ...                                                  # borrar la última (si NO está aplicada)
dotnet ef migrations script --idempotent -o cambios.sql ...                                      # SQL para el DBA
dotnet ef migrations has-pending-model-changes ...                                               # ¿me he olvidado alguna?
```

## Chuleta: una consulta de lectura bien hecha

```csharp
public async Task<Pagina<FilaDto>> ListarAsync(Filtro filtro, int pagina, int tamano, CancellationToken ct)
{
    IQueryable<Entidad> consulta = db.Entidades;                        // 1. empezar por el IQueryable
    if (filtro.Estado is not null)
        consulta = consulta.Where(e => e.Estado == filtro.Estado);      // 2. filtrar en SQL

    var total = await consulta.CountAsync(ct);                          // 3. total para el paginador

    var elementos = await consulta
        .OrderBy(e => e.Nombre).ThenBy(e => e.Id)                       // 4. orden ESTABLE
        .Skip((pagina - 1) * tamano).Take(tamano)                       // 5. paginar
        .Select(e => new FilaDto(e.Id, e.Nombre, e.Relacionada!.Nombre, e.Hijos.Count))   // 6. proyectar
        .ToListAsync(ct);                                               // 7. async + CancellationToken

    return new Pagina<FilaDto>(elementos, pagina, tamano, total);
}
```

## Chuleta: una relación nueva en Clean Architecture

1. **Domain**: propiedad FK (`int?` u `int`) y navegación; si es una colección del agregado, campo privado + `IReadOnlyCollection` + método que aplica la regla.
2. **Infrastructure**: `HasOne/HasMany ... WithMany/WithOne ... HasForeignKey ... OnDelete` en la configuración.
3. Con una base de datos real: `dotnet ef migrations add ...` → **revisar** `Up`/`Down` → aplicar.
4. **Application**: DTO y consulta (proyección) para leerla; comando para escribirla.
5. **Web**: vista / formulario / acción.

## Avance del día 4

Partiremos de `src/day-03`:

1. **De Web Forms a Razor/MVC**: controles, eventos y gestión del estado (`Session`, `ViewState` y sus alternativas), con ejemplos sacados del análisis de SIREI.
2. **Herramientas oficiales de Microsoft** para la migración y **migración desde MVC 5** (Noticom).
3. **Seguridad**: autenticación y autorización, ASP.NET Core Identity, OAuth2/OpenID Connect; CSRF y XSS (repaso de lo que ya hacemos).
4. **Logging, rendimiento y pruebas**: logging estructurado y pruebas de los casos de uso (ahora que el acceso a datos está detrás de interfaces).

## Referencias generales

> Enlaces comprobados el 7 de octubre de 2026.

- [Documentación de Entity Framework Core](https://learn.microsoft.com/es-es/ef/core/) — Punto de entrada de toda la documentación de EF Core.
- [Introducción al rendimiento](https://learn.microsoft.com/es-es/ef/core/performance/) — Lectura recomendada para repasar el capítulo 6.
- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0) — Guía oficial; base del Módulo 3 que seguimos mañana.
- [Patrón de la higuera estranguladora](https://learn.microsoft.com/es-es/azure/architecture/patterns/strangler-fig) — El patrón de la migración incremental.
