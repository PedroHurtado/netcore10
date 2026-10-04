# Lab 3 — Servicios, inyección de dependencias y EF Core InMemory

**Duración:** 30 min · **Teoría relacionada:** [06 — Inyección de dependencias](../06-inyeccion-dependencias.md)

## Objetivo

Sustituir la lista estática del Lab 1 por una arquitectura de servicios inyectados:

```
Endpoint ──▶ IIncidenciaService ──▶ IIncidenciaRepository ──▶ IncidenciasDbContext (EF Core InMemory)
```

## Paso 1 — Paquete de EF Core InMemory

```bash
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

## Paso 2 — DbContext

Crea `Data/IncidenciasDbContext.cs`:

```csharp
public class IncidenciasDbContext(DbContextOptions<IncidenciasDbContext> options) : DbContext(options)
{
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Incidencia>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Titulo).IsRequired().HasMaxLength(120);
        });
    }
}
```

Regístralo:

```csharp
builder.Services.AddDbContext<IncidenciasDbContext>(opt => opt.UseInMemoryDatabase("GestorIncidencias"));
```

## Paso 3 — Repositorio

1. Crea la interfaz `Services/IIncidenciaRepository.cs` con métodos asíncronos: `ObtenerTodasAsync`, `ObtenerPorIdAsync`, `AgregarAsync`, `ActualizarAsync`, `ContarAbiertasAsync`.
2. Implementa `Services/EfIncidenciaRepository.cs` recibiendo `IncidenciasDbContext` en el constructor.
3. Regístralo como **Scoped**: `builder.Services.AddScoped<IIncidenciaRepository, EfIncidenciaRepository>();`

✅ **Comprueba:** ¿por qué Scoped y no Singleton? (Pista: ¿qué lifetime tiene el DbContext?)

## Paso 4 — Servicio de negocio

Crea `Services/IncidenciaService.cs` (interfaz `IIncidenciaService` + implementación) con `ListarAsync`, `ObtenerAsync`, `CrearAsync` y `ResolverAsync`.

Dependencias por constructor:

- `IIncidenciaRepository`
- `TimeProvider` → regístralo con `builder.Services.AddSingleton(TimeProvider.System);`
- `ILogger<IncidenciaService>`

Reglas de negocio:

- No se puede resolver una incidencia que no existe o que ya está resuelta/cerrada.
- `FechaAlta` y `FechaResolucion` salen de `reloj.GetUtcNow()`, **nunca** de `DateTimeOffset.UtcNow`.

Usa el tipo `Resultado<T>` de la solución para devolver éxito o error sin lanzar excepciones.

## Paso 5 — Endpoints limpios

Mueve los endpoints a `Endpoints/IncidenciasEndpoints.cs` como método de extensión `MapIncidencias()` y usa `MapGroup("/api/incidencias")`. Los endpoints **solo** traducen HTTP ↔ servicio:

```csharp
grupo.MapGet("/", async (IIncidenciaService servicio, CancellationToken ct) =>
    (await servicio.ListarAsync(ct)).Select(IncidenciaResponse.Desde));
```

Borra la lista estática de `Program.cs` y llama a `app.MapIncidencias();`.

✅ **Comprueba:** todo el fichero `.http` funciona igual que antes.

## Paso 6 — Datos iniciales con `IHostedService`

Crea `Services/DatosDemoInitializer.cs` que inserte tres incidencias al arrancar.

1. Primero, intenta inyectar `IIncidenciaRepository` **directamente en el constructor** y regístralo con `builder.Services.AddHostedService<DatosDemoInitializer>();`. Arranca. **¿Qué error aparece?**
2. Corrígelo inyectando `IServiceScopeFactory` y creando un ámbito con `CreateScope()`.

<details>
<summary>Explicación</summary>

Un `IHostedService` es Singleton. Si recibe un servicio Scoped en el constructor, lo retendría para siempre (*captive dependency*). En Development el contenedor valida los ámbitos y lanza: *Cannot consume scoped service 'IIncidenciaRepository' from singleton 'IHostedService'*.
</details>

## Paso 7 — Visualizar los lifetimes

Copia `Lifetimes/Operaciones.cs` de la solución, registra las tres variantes y crea `GET /demo/lifetimes`. Llama varias veces y completa la tabla:

| | ¿Igual en endpoint y consumidor? | ¿Igual entre dos peticiones? |
|---|---|---|
| Transient | | |
| Scoped | | |
| Singleton | | |

## Reto (opcional): cambiar la implementación sin tocar el negocio

1. Crea `ListaIncidenciaRepository : IIncidenciaRepository` que guarde los datos en un `ConcurrentDictionary<int, Incidencia>`.
2. ¿Con qué lifetime debes registrarlo para que los datos no se pierdan entre peticiones? ¿Por qué tiene que ser *thread-safe*?
3. Cambia **solo** la línea de registro en `Program.cs` y comprueba que la aplicación sigue funcionando sin modificar `IncidenciaService` ni los endpoints.
4. Vuelve a dejar `EfIncidenciaRepository`.

## Para reflexionar

- ¿Cuántos `new` de clases de negocio quedan en tu código? ¿Quién crea ahora los objetos?
- ¿Cómo probarías `IncidenciaService.ResolverAsync` sin base de datos y controlando la fecha?

## Referencias

> Enlaces comprobados el 4 de octubre de 2026.

- [Inserción de dependencias en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-10.0)
- [Instrucciones para la inserción de dependencias](https://learn.microsoft.com/es-es/dotnet/core/extensions/dependency-injection/guidelines) — Dependencias cautivas y otros antipatrones.
- [Proveedor de bases de datos en memoria de EF Core](https://learn.microsoft.com/es-es/ef/core/providers/in-memory/)
- [Duración, configuración e inicialización de DbContext](https://learn.microsoft.com/es-es/ef/core/dbcontext-configuration/)
- [Uso de servicios con ámbito dentro de un servicio en segundo plano](https://learn.microsoft.com/es-es/dotnet/core/extensions/scoped-service) — Solución del paso 6.
- [Tareas en segundo plano con servicios hospedados](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0)
- [Grupos de rutas (`MapGroup`)](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-10.0#route-groups)
- [¿Qué es la clase `TimeProvider`?](https://learn.microsoft.com/es-es/dotnet/standard/datetime/timeprovider-overview)
