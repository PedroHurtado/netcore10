# 2. Clean Architecture paso a paso

Clean Architecture la propuso Robert C. Martin ("Uncle Bob") en 2012 como síntesis de varias ideas anteriores (Hexagonal, Onion...). En .NET se ha convertido en la forma más habitual de organizar aplicaciones empresariales, y Microsoft la usa en su guía oficial de arquitectura.

En este capítulo la vamos a ver **aplicada a nuestro proyecto**, no en abstracto. Todo el código está en [src/day-02](../../src/day-02/).

## 2.1 La idea en un dibujo

```
          ┌───────────────────────────────────────────────┐
          │  Web (presentación)                           │
          │  Controladores · Vistas · Razor Pages · API   │
          │      ┌───────────────────────────────────┐    │
          │      │  Infrastructure                   │    │
          │      │  EF Core · Repositorios · Correo  │    │
          │      │     ┌─────────────────────────┐   │    │
          │      │     │  Application            │   │    │
          │      │     │  Casos de uso · Puertos │   │    │
          │      │     │   ┌─────────────────┐   │   │    │
          │      │     │   │  Domain         │   │   │    │
          │      │     │   │  Entidades      │   │   │    │
          │      │     │   │  Reglas         │   │   │    │
          │      │     │   └─────────────────┘   │   │    │
          │      │     └─────────────────────────┘   │    │
          │      └───────────────────────────────────┘    │
          └───────────────────────────────────────────────┘

                Las dependencias SOLO apuntan hacia dentro ──▶ ●
```

Cuanto más al centro, **más estable y más valioso**: las reglas de negocio cambian poco y son lo que da valor. Cuanto más hacia fuera, **más técnico y más cambiante**: frameworks, base de datos, interfaz.

## 2.2 La regla de dependencia

Es **la única regla** que hay que memorizar:

> **El código de un círculo interior no puede mencionar nada de un círculo exterior.** Ni una clase, ni una interfaz, ni un `using`.

En nuestra solución eso se traduce en las referencias entre proyectos:

```
  GestorIncidencias.Web ──────────┬──────────────▶ GestorIncidencias.Application ──▶ GestorIncidencias.Domain
                                  │                          ▲
                                  └──▶ GestorIncidencias.Infrastructure ─┘
```

| Proyecto | Referencia a | Paquetes NuGet |
|---|---|---|
| `Domain` | **nada** | **ninguno** |
| `Application` | `Domain` | Abstracciones ligeras: `Microsoft.Extensions.Options`, `Logging.Abstractions` |
| `Infrastructure` | `Application` | `EntityFrameworkCore.InMemory`, `Hosting.Abstractions` |
| `Web` | `Application`, `Infrastructure` | `AspNetCore.OpenApi` (y el SDK web) |

Compruébalo abriendo los `.csproj`: el de Domain está prácticamente vacío. **Si alguien escribe `using Microsoft.EntityFrameworkCore;` en el dominio, la solución no compila.** Esa es la garantía que no teníamos con carpetas.

> ¿Por qué `Web` referencia a `Infrastructure` si las flechas apuntan hacia dentro? Porque `Web` es la **raíz de composición** (2.7): el único sitio donde se "enchufan" las implementaciones a las interfaces. Los controladores **no** usan nada de Infrastructure; solo `Program.cs` llama a `AddInfrastructure()`.

## 2.3 Qué va en cada capa

| Capa | Contiene | NO contiene | En nuestro proyecto |
|---|---|---|---|
| **Domain** | Entidades con comportamiento, enums, reglas que dependen solo de la entidad, tipos de resultado | EF Core, ASP.NET, JSON, configuración, logging, DTOs | `Incidencia`, `Prioridad`, `EstadoIncidencia`, `Resultado` |
| **Application** | Casos de uso, interfaces de lo que necesita del exterior (**puertos**), DTOs de entrada/salida, reglas que implican varias entidades o configuración | SQL, EF Core, `HttpContext`, vistas, `IActionResult` | `IIncidenciaService`, `IncidenciaService`, `IIncidenciaRepository`, `IncidenciaDto`, `IncidenciasOptions` |
| **Infrastructure** | Implementaciones técnicas de los puertos (**adaptadores**): EF Core, ficheros, correo, APIs externas | Reglas de negocio | `IncidenciasDbContext`, `EfIncidenciaRepository`, `IncidenciaConfiguracion`, `DatosDemoInitializer` |
| **Web** | Controladores, vistas, Razor Pages, API, ViewModels, middleware, `Program.cs` | Reglas de negocio, consultas a la base de datos | `IncidenciasController`, `Views/`, `Pages/`, `IncidenciasApiController` |

## 2.4 Domain: entidades con comportamiento

### Antes (día 1): entidad "anémica"

```csharp
public class Incidencia
{
    public int Id { get; set; }
    public EstadoIncidencia Estado { get; set; }      // cualquiera puede cambiarlo
    public DateTimeOffset? FechaResolucion { get; set; }
    // ...
}
```

Es solo una bolsa de datos. Las reglas ("no se puede resolver algo cerrado") viven fuera, en el servicio... o en cualquier otro sitio.

### Ahora (día 2): entidad "rica"

```csharp
public class Incidencia
{
    private Incidencia() { }                                   // EF Core lo necesita; nadie más lo usa

    public int Id { get; private set; }
    public EstadoIncidencia Estado { get; private set; }       // solo se cambia desde dentro
    public DateTimeOffset? FechaResolucion { get; private set; }

    public static Resultado<Incidencia> Crear(string titulo, string? descripcion,
                                              Prioridad prioridad, DateTimeOffset fechaAlta)
    {
        titulo = (titulo ?? string.Empty).Trim();
        if (titulo.Length is < TituloLongitudMinima or > TituloLongitudMaxima)
            return Resultado<Incidencia>.Fallo("El título debe tener entre 5 y 120 caracteres.");
        // ...
        return Resultado<Incidencia>.Ok(new Incidencia { /* ... */ Estado = EstadoIncidencia.Abierta });
    }

    public Resultado Resolver(DateTimeOffset fecha)
    {
        if (!EstaAbierta)
            return Resultado.Fallo($"La incidencia ya está {Estado}.", TipoError.Conflicto);

        Estado = EstadoIncidencia.Resuelta;
        FechaResolucion = fecha;
        return Resultado.Ok();
    }
}
```

Código completo: [Incidencia.cs](../../src/day-02/GestorIncidencias.Domain/Incidencias/Incidencia.cs).

Qué ganamos:

1. **Es imposible crear una incidencia no válida**: el constructor es privado; solo existe `Crear`, que valida.
2. **Es imposible saltarse una transición**: `incidencia.Estado = Cerrada` no compila fuera de la clase (error CS0200: la propiedad es de solo lectura).
3. **Las reglas están en un único sitio** y se pueden probar con un test de tres líneas, sin base de datos ni servidor web.
4. **Sobreviven a cualquier migración**: esta clase funcionaría igual en Web Forms, en MVC 5, en una consola o en Blazor.

### ¿Por qué `Resultado` y no excepciones?

Que alguien intente cerrar una incidencia abierta **no es excepcional**: es un caso normal que la interfaz debe mostrar. [Resultado.cs](../../src/day-02/GestorIncidencias.Domain/Comun/Resultado.cs) devuelve éxito o error, y un `TipoError` (`Validacion`, `NoEncontrado`, `Conflicto`) que la capa Web traduce a HTTP 400, 404 o 409 sin que el dominio sepa nada de HTTP. Las excepciones las reservamos para lo realmente inesperado (la base de datos se cae).

## 2.5 Application: casos de uso y puertos

La capa de aplicación responde a la pregunta **"¿qué puede hacer el usuario con el sistema?"**: crear, listar, iniciar, resolver y cerrar incidencias. Cada método de `IIncidenciaService` es un **caso de uso**.

```csharp
public Task<Resultado<IncidenciaDto>> ResolverAsync(int id, CancellationToken ct = default) =>
    CambiarEstadoAsync(id, incidencia => incidencia.Resolver(reloj.GetUtcNow()), ct);

private async Task<Resultado<IncidenciaDto>> CambiarEstadoAsync(int id, Func<Incidencia, Resultado> cambio, CancellationToken ct)
{
    var incidencia = await repositorio.ObtenerPorIdAsync(id, ct);       // 1. cargar
    if (incidencia is null)
        return Resultado<IncidenciaDto>.Fallo($"No existe la incidencia {id}.", TipoError.NoEncontrado);

    var resultado = cambio(incidencia);                                   // 2. pedir el cambio a la entidad
    if (!resultado.Exito)
        return Resultado<IncidenciaDto>.Fallo(resultado.Error!, resultado.Tipo!.Value);

    await repositorio.GuardarCambiosAsync(ct);                            // 3. guardar
    return Resultado<IncidenciaDto>.Ok(IncidenciaDto.Desde(incidencia));  // 4. devolver un DTO
}
```

El caso de uso **orquesta**; no decide las reglas. Este patrón (cargar → pedir a la entidad → guardar → devolver DTO) se repite en casi todos los casos de uso de cualquier aplicación.

### Reglas de dominio frente a reglas de aplicación

| Regla | ¿Dónde? | Por qué |
|---|---|---|
| "El título tiene entre 5 y 120 caracteres" | Dominio (`Incidencia.Crear`) | Solo depende de la propia incidencia |
| "Solo se cierra una incidencia resuelta" | Dominio (`Incidencia.Cerrar`) | Solo depende del estado de la incidencia |
| "No puede haber más de N incidencias abiertas" | Aplicación (`IncidenciaService.CrearAsync`) | Depende de **otras** incidencias (consulta) y de la **configuración** |

### Puertos: interfaces que define quien las necesita

```csharp
// GestorIncidencias.Application/Abstracciones/IIncidenciaRepository.cs
public interface IIncidenciaRepository
{
    Task<Incidencia?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task GuardarCambiosAsync(CancellationToken ct = default);
    // ...
}
```

La interfaz está en **Application**, no en Infrastructure. Es Application quien dice "necesito algo que me dé incidencias y las guarde"; Infrastructure se adapta. En terminología hexagonal esto es un **puerto** (lo veremos en el capítulo 3).

### DTOs: la entidad no sale de Application

Los casos de uso devuelven `IncidenciaDto`, no `Incidencia`. Así la capa Web **no puede** llamar a `incidencia.Cerrar()` sin pasar por el caso de uso (y sin guardar). Además, la forma de los datos que ve la interfaz puede evolucionar sin tocar la entidad.

## 2.6 Infrastructure: los detalles técnicos

```csharp
// Adaptador: implementa el puerto con EF Core
public class EfIncidenciaRepository(IncidenciasDbContext db) : IIncidenciaRepository
{
    public Task<Incidencia?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        db.Incidencias.FirstOrDefaultAsync(i => i.Id == id, ct);

    // La entidad se obtuvo con seguimiento: EF Core detecta solo qué ha cambiado.
    public Task GuardarCambiosAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
```

El mapeo a la base de datos se hace con la **API fluida** en una clase aparte, para que la entidad no necesite atributos de EF Core:

```csharp
public class IncidenciaConfiguracion : IEntityTypeConfiguration<Incidencia>
{
    public void Configure(EntityTypeBuilder<Incidencia> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Titulo).IsRequired().HasMaxLength(Incidencia.TituloLongitudMaxima);
        builder.Property(i => i.Estado).HasConversion<string>();
        builder.Ignore(i => i.EstaAbierta);   // propiedad calculada
    }
}
```

> EF Core es capaz de leer y escribir propiedades con `private set` y de usar un constructor privado. No hace falta "estropear" la entidad para que la base de datos funcione.

Cada capa expone un método de extensión para registrar sus servicios:

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services)
{
    services.AddDbContext<IncidenciasDbContext>(opt => opt.UseInMemoryDatabase("GestorIncidencias"));
    services.AddScoped<IIncidenciaRepository, EfIncidenciaRepository>();   // puerto → adaptador
    services.AddHostedService<DatosDemoInitializer>();
    return services;
}
```

## 2.7 Web: la raíz de composición

```csharp
// Program.cs
builder.Services.AddApplication();      // casos de uso
builder.Services.AddInfrastructure();   // EF Core, repositorio
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
```

`Program.cs` es el **único** lugar que conoce todas las piezas. Los controladores solo ven `IIncidenciaService`:

```csharp
public class IncidenciasController(IIncidenciaService servicio) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolver(int id, CancellationToken ct) =>
        TrasCambioDeEstado(id, await servicio.ResolverAsync(id, ct), "Incidencia resuelta.");
}
```

Un controlador que solo traduce HTTP ↔ caso de uso se llama **controlador delgado** (*thin controller*). Lo veremos en detalle en el capítulo 4.

## 2.8 El truco que lo hace posible: compilación frente a ejecución

```
EN TIEMPO DE COMPILACIÓN (referencias)        EN TIEMPO DE EJECUCIÓN (llamadas)

 IncidenciaService ──usa──▶ IIncidenciaRepository     IncidenciaService
          (Application)          (Application)               │ llama a
                                       ▲                       ▼
                                       │ implementa      EfIncidenciaRepository
                         EfIncidenciaRepository                │
                            (Infrastructure)                   ▼
                                                          EF Core → BD
```

En **ejecución**, el negocio llama a la base de datos (como siempre). En **compilación**, la flecha va al revés: Infrastructure depende de Application. El contenedor de inyección de dependencias del día 1 es quien conecta las dos cosas al arrancar.

## 2.9 Recorrido de una petición: "Resolver la incidencia 3"

Sigue el camino en el código (es lo que haremos en el [Lab 1](labs/lab-01-recorrido-capas.md) con el depurador):

| # | Capa | Qué ocurre | Fichero |
|---|---|---|---|
| 1 | Web | El navegador envía `POST /Incidencias/Resolver/3` con el token antiforgery | `Views/Incidencias/Detalle.cshtml` |
| 2 | Web | El enrutamiento elige `IncidenciasController.Resolver(id: 3)` y valida el token | `Program.cs`, `IncidenciasController.cs` |
| 3 | Application | `IncidenciaService.ResolverAsync(3)` carga la incidencia a través del **puerto** | `IncidenciaService.cs` |
| 4 | Infrastructure | `EfIncidenciaRepository` la busca con EF Core | `EfIncidenciaRepository.cs` |
| 5 | Domain | `incidencia.Resolver(fecha)` comprueba la regla y cambia el estado | `Incidencia.cs` |
| 6 | Infrastructure | `GuardarCambiosAsync` → `SaveChangesAsync` | `EfIncidenciaRepository.cs` |
| 7 | Application | Devuelve `Resultado<IncidenciaDto>` | `IncidenciaService.cs` |
| 8 | Web | El controlador guarda un mensaje en `TempData` y redirige a `Detalle/3` | `IncidenciasController.cs` |
| 9 | Web | La vista `Detalle` muestra el nuevo estado y el mensaje | `Detalle.cshtml`, `_Mensajes.cshtml` |

## 2.10 Crear la solución desde cero (CLI)

Así se construyó la estructura de [src/day-02](../../src/day-02/) (no hace falta que lo repitáis; es para tenerlo de referencia):

```bash
mkdir GestorIncidencias && cd GestorIncidencias
dotnet new sln -n GestorIncidencias            # en .NET 10 genera GestorIncidencias.slnx

dotnet new classlib -n GestorIncidencias.Domain
dotnet new classlib -n GestorIncidencias.Application
dotnet new classlib -n GestorIncidencias.Infrastructure
dotnet new mvc      -n GestorIncidencias.Web   # plantilla MVC: controladores + vistas

dotnet sln add GestorIncidencias.Domain GestorIncidencias.Application GestorIncidencias.Infrastructure GestorIncidencias.Web

# Las flechas de la regla de dependencia:
dotnet add GestorIncidencias.Application    reference GestorIncidencias.Domain
dotnet add GestorIncidencias.Infrastructure reference GestorIncidencias.Application
dotnet add GestorIncidencias.Web            reference GestorIncidencias.Application GestorIncidencias.Infrastructure

dotnet add GestorIncidencias.Infrastructure package Microsoft.EntityFrameworkCore.InMemory
```

Un detalle práctico: [Directory.Build.props](../../src/day-02/Directory.Build.props) en la carpeta de la solución fija `TargetFramework`, `Nullable` e `ImplicitUsings` para **todos** los proyectos, así no se repite en cada `.csproj`.

## 2.11 "¿Dónde pongo...?"

Es la pregunta que más se repite al empezar. Una chuleta:

| Quiero... | Lo pongo en... | Motivo |
|---|---|---|
| Validar que el título tiene 5 caracteres | **Domain** (y, para mostrar el mensaje en el formulario, también en el **ViewModel** de Web) | La regla de verdad es del dominio; el ViewModel solo da feedback rápido |
| Limitar el número de incidencias abiertas | **Application** | Necesita consultar otras incidencias y leer configuración |
| Una consulta LINQ a EF Core | **Infrastructure** | Es un detalle técnico |
| Enviar un correo al resolver | Interfaz `INotificador` en **Application**, implementación SMTP en **Infrastructure** | Mismo patrón que el repositorio |
| Obtener la fecha actual | `TimeProvider` inyectado en **Application** | Para poder fijar la fecha en las pruebas |
| Leer `appsettings.json` | Clase de opciones en **Application**; enlace en **Web** (`Program.cs`) | La aplicación no sabe de dónde viene la configuración |
| Convertir un formulario en un comando | **Web** (`IncidenciaFormulario.AComando()`) | Es una necesidad de la interfaz |
| Elegir qué vista mostrar o a dónde redirigir | **Web** (controlador) | Es navegación |
| Decidir si se muestra el botón "Cerrar" | **Web** (vista), pero la regla la vuelve a comprobar el **Domain** | La vista oculta; el dominio protege |
| Log de "incidencia creada" | **Application** (`ILogger<T>`) | `ILogger` es una abstracción, no una tecnología |

## 2.12 Errores frecuentes

| Error | Síntoma | Solución |
|---|---|---|
| Entidad anémica + servicio con todo | El dominio son solo propiedades `{ get; set; }`; las reglas se repiten en varios servicios | Mover las reglas que solo dependen de la entidad a métodos de la entidad |
| Devolver la entidad al controlador | La vista o la API modifican la entidad; se serializan propiedades internas | Devolver DTOs desde Application |
| Atributos de EF Core en el dominio (`[Table]`, `[Key]`) | Domain necesita el paquete de EF Core | API fluida en `IEntityTypeConfiguration<T>` |
| Lógica en el controlador | `if (incidencia.Estado == ...)` en el controlador | Moverla al dominio o al caso de uso |
| Un repositorio genérico `IRepository<T>` con 20 métodos | Interfaces enormes que nadie usa enteras | Interfaces pequeñas con los métodos que el caso de uso necesita |
| Una capa por cada patrón (`Dtos`, `Mappers`, `Validators`, `Services`, `Managers`...) | Diez proyectos para un CRUD | Empezar con 4 proyectos y crecer solo si duele |
| Referencia de Application a Infrastructure "solo para una cosa" | Se rompe la regla de dependencia y deja de compilar el aislamiento | Definir una interfaz en Application |

## 2.13 ¿Cuándo NO usar Clean Architecture?

Clean Architecture tiene un coste: más proyectos, más ficheros, más "saltos" para seguir una petición. **No compensa** en:

- Prototipos y pruebas de concepto.
- Aplicaciones CRUD puras, sin reglas de negocio (formularios que guardan lo que se escribe).
- Utilidades internas pequeñas con vida corta.
- Microservicios muy pequeños que hacen una sola cosa.

En el capítulo 3 la comparamos con Hexagonal y Vertical Slice para ver cuándo conviene cada una.

## Preguntas de repaso

1. ¿Qué proyecto de la solución no tiene ninguna referencia? ¿Por qué es importante?
2. ¿Por qué `IIncidenciaRepository` está en Application y no en Infrastructure?
3. Si mañana cambiamos EF Core InMemory por SQL Server, ¿qué proyectos hay que tocar?
4. ¿Por qué el controlador recibe un `IncidenciaDto` y no una `Incidencia`?
5. La vista oculta el botón "Cerrar" si la incidencia no está resuelta. ¿Es suficiente protección? ¿Qué pasa si alguien envía el POST a mano?

## Referencias

> Enlaces comprobados el 6 de octubre de 2026.

**Documentación oficial**

- [Arquitecturas de aplicaciones web comunes — Arquitectura limpia](https://learn.microsoft.com/es-es/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures#clean-architecture) — Explicación de Microsoft con la misma organización Core / Infrastructure / UI.
- [Diseño de un microservicio orientado a DDD](https://learn.microsoft.com/es-es/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice) — Capas Domain / Application / Infrastructure y la regla de dependencia.
- [Diseño de un modelo de dominio de microservicio](https://learn.microsoft.com/es-es/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model) — Entidades ricas frente a anémicas.
- [Creación de un modelo y configuración con API fluida (EF Core)](https://learn.microsoft.com/es-es/ef/core/modeling/) — `IEntityTypeConfiguration<T>` y `ApplyConfigurationsFromAssembly`.
- [Personalización de la compilación por carpeta](https://learn.microsoft.com/es-es/visualstudio/msbuild/customize-by-directory) — `Directory.Build.props`.

**Fuentes originales y plantillas**

- [The Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html) (inglés) — Artículo original de Robert C. Martin (2012).
- [jasontaylordev/CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture) (inglés) — Plantilla de Clean Architecture para ASP.NET Core muy extendida.
- [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture) (inglés) — Otra plantilla de referencia, de Steve Smith (autor del libro de Microsoft).
- [dotnet-architecture/eShopOnWeb](https://github.com/dotnet-architecture/eShopOnWeb) (inglés) — Aplicación de ejemplo de Microsoft que acompaña al libro electrónico.
