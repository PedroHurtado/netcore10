# 8. Herramientas para pruebas y validación

Una migración sin pruebas es una migración a ciegas: no hay forma de saber si la pantalla nueva hace lo mismo que la vieja, ni si el cambio de mañana rompe lo de hoy. Este capítulo presenta los dos proyectos de pruebas que hemos añadido a la solución y las herramientas que usan.

## 8.1 La pirámide de pruebas

```
                 ▲  pocas, lentas, frágiles
                ╱ ╲
               ╱E2E╲          Navegador real (Playwright): "el usuario pulsa Resolver"
              ╱─────╲
             ╱ Inte- ╲        La aplicación completa en memoria (WebApplicationFactory):
            ╱ gración ╲       HTTP → middleware → seguridad → controlador → EF Core
           ╱───────────╲
          ╱  Unitarias  ╲     Una clase aislada: Dominio y casos de uso, con dobles
         ╱───────────────╲
                 ▼  muchas, rápidas (milisegundos), estables
```

| Tipo | Qué prueba | En el proyecto | Tiempo |
|---|---|---|---|
| **Unitarias** | Reglas del dominio, decisiones de los casos de uso | `tests/GestorIncidencias.UnitTests` (14) | ~1 s |
| **Integración** | Que las piezas encajan: rutas, autenticación, autorización, antiforgery, JSON, EF Core | `tests/GestorIncidencias.IntegrationTests` (13) | ~4 s |
| **Extremo a extremo** | La interfaz en un navegador real | — (se mencionan en 8.6) | Minutos |

> **Por qué ahora es fácil:** en SIREI, la regla "no cerrar con trámites pendientes" estaba en un `btnGuardar_Click` que depende de `Page`, `Session` y SQL Server: imposible de probar sin levantarlo todo. En nuestra solución, la misma clase de regla está en `Incidencia`, que no depende de nada. **La arquitectura del día 2 es lo que hace posibles estas pruebas.**

## 8.2 La estructura

```
src/day-04/
├── global.json                              ← activa Microsoft.Testing.Platform en "dotnet test"
├── GestorIncidencias.slnx                   ← carpeta /tests/ con los dos proyectos
└── tests/
    ├── GestorIncidencias.UnitTests/         → referencia a Application (y, a través de ella, a Domain)
    │   ├── Dominio/IncidenciaTests.cs
    │   └── Aplicacion/IncidenciaServiceTests.cs
    └── GestorIncidencias.IntegrationTests/  → referencia a Web
        ├── AplicacionFixture.cs             ← WebApplicationFactory<Program> compartida
        ├── SeguridadTests.cs
        └── ApiIncidenciasTests.cs
```

Para ejecutarlas:

```bash
cd src/day-04
dotnet test
```

```
Test run summary: Passed!
  total: 27
  failed: 0
  succeeded: 27
```

También desde el **Explorador de pruebas** de Visual Studio o de VS Code (C# Dev Kit).

### Las herramientas

| Paquete | Para qué |
|---|---|
| **xUnit v3** (`xunit.v3`) | El framework: `[Fact]`, `[Theory]`, `Assert`. Genera un ejecutable que usa **Microsoft.Testing.Platform** (por eso el `global.json`) |
| **NSubstitute** | Crear **dobles** de interfaces (repositorio, consultas, usuario actual) |
| `Microsoft.Extensions.TimeProvider.Testing` | `FakeTimeProvider`: un reloj que controla la prueba |
| `Microsoft.AspNetCore.Mvc.Testing` | `WebApplicationFactory<Program>`: la aplicación completa en memoria |

> MSTest y NUnit son igual de válidos; xUnit es el más usado en los proyectos de ASP.NET Core y en la documentación de Microsoft sobre pruebas de integración. Lo importante son los conceptos, que son los mismos.

## 8.3 Pruebas unitarias del dominio

[Dominio/IncidenciaTests.cs](../../src/day-04/tests/GestorIncidencias.UnitTests/Dominio/IncidenciaTests.cs):

```csharp
[Fact]
public void Cerrar_UnaAbierta_DaConflictoYNoCambiaElEstado()
{
    // Arrange (preparar)
    var incidencia = NuevaIncidencia();

    // Act (actuar)
    var resultado = incidencia.Cerrar();

    // Assert (comprobar)
    Assert.False(resultado.Exito);
    Assert.Equal(TipoError.Conflicto, resultado.Tipo);
    Assert.Equal(EstadoIncidencia.Abierta, incidencia.Estado);
}
```

| Convención | Ejemplo |
|---|---|
| Nombre `Metodo_Situacion_Resultado` | `Cerrar_UnaAbierta_DaConflictoYNoCambiaElEstado` — el informe se lee como una especificación |
| Patrón **AAA**: Arrange, Act, Assert | Tres bloques separados por una línea en blanco |
| Una cosa por prueba | Si falla, el nombre dice qué se ha roto |
| `[Theory]` + `[InlineData]` para varios datos | Título `""`, `"    "` y `"abc"`: tres pruebas con el mismo código |

## 8.4 Pruebas unitarias de los casos de uso: dobles

`IncidenciaService` depende de **interfaces**. En la prueba se le pasan **dobles** creados con NSubstitute ([Aplicacion/IncidenciaServiceTests.cs](../../src/day-04/tests/GestorIncidencias.UnitTests/Aplicacion/IncidenciaServiceTests.cs)):

```csharp
private readonly IIncidenciaRepository _repositorio = Substitute.For<IIncidenciaRepository>();
private readonly FakeTimeProvider _reloj = new(new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero));

[Fact]
public async Task CrearAsync_ConElLimiteDeAbiertasAlcanzado_DaConflictoYNoGuarda()
{
    _repositorio.ContarAbiertasAsync(Arg.Any<CancellationToken>()).Returns(10);   // "cuando te pregunten, di 10"
    var servicio = CrearServicio();                                               // MaxIncidenciasAbiertas = 10

    var resultado = await servicio.CrearAsync(new CrearIncidenciaComando("Nueva incidencia de prueba", null, null), Ct);

    Assert.False(resultado.Exito);
    Assert.Equal(TipoError.Conflicto, resultado.Tipo);
    await _repositorio.DidNotReceive().AgregarAsync(Arg.Any<Incidencia>(), Arg.Any<CancellationToken>());   // "no te han llamado"
}
```

| NSubstitute | Significa |
|---|---|
| `Substitute.For<IInterfaz>()` | Crea el doble |
| `doble.Metodo(args).Returns(valor)` | Cuando lo llamen así, devuelve esto |
| `Arg.Any<T>()` | Con cualquier valor |
| `Arg.Do<T>(x => ...)` | Captura el argumento para examinarlo después |
| `await doble.Received(1).Metodo(...)` | Comprueba que se llamó exactamente una vez |
| `await doble.DidNotReceive().Metodo(...)` | Comprueba que **no** se llamó |

Dos detalles de diseño que se ven en estas pruebas:

- **`TimeProvider` en lugar de `DateTime.Now`**: con `FakeTimeProvider`, la prueba sabe qué hora es y puede comprobar `FechaAlta` exacta. Con `DateTime.Now`, imposible.
- **`IUsuarioActual` en lugar de `HttpContext.Current`**: la prueba dice "el usuario es Ana García" con una línea (`_usuario.Nombre.Returns("Ana García")`) y comprueba que el comentario lleva ese autor.

> **¿Por qué no probar los casos de uso con EF Core InMemory?** Se puede, pero Microsoft lo **desaconseja**: InMemory no se comporta como una base de datos real (día 3). Para probar el **acceso a datos** de verdad, lo correcto es una base de datos real (SQL Server en un contenedor con Testcontainers, o LocalDB). Para probar la **lógica**, dobles.

## 8.5 Pruebas de integración: la aplicación completa en memoria

`WebApplicationFactory<Program>` arranca el `Program.cs` **real** (en el entorno Development, con los datos y usuarios de demostración) sobre un servidor en memoria, y da un `HttpClient` para hacerle peticiones. Sin puertos, sin navegador.

```csharp
// AplicacionFixture.cs — una sola instancia para todas las pruebas de la colección "Aplicacion"
public class AplicacionFixture : WebApplicationFactory<Program> { ... }

// SeguridadTests.cs
[Collection("Aplicacion")]
public class SeguridadTests(AplicacionFixture aplicacion)
{
    [Theory]
    [InlineData("/Incidencias")]
    [InlineData("/Paginas/Incidencias")]
    [InlineData("/Usuarios")]
    public async Task PaginasPrivadas_SinIniciarSesion_RedirigenAlLogin(string url)
    {
        var cliente = aplicacion.ClienteSinRedirecciones();

        var respuesta = await cliente.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.StartsWith("/Cuenta/Login", respuesta.Headers.Location!.PathAndQuery);
    }
}
```

Las pruebas de integración del proyecto comprueban precisamente lo que **no** se ve en una prueba unitaria:

| Prueba | Qué protege |
|---|---|
| `Portada_SinIniciarSesion_EsPublica` | El `[AllowAnonymous]` de `HomeController` |
| `PaginasPrivadas_SinIniciarSesion_RedirigenAlLogin` | La *FallbackPolicy* y el orden de los middleware |
| `Api_SinToken_Devuelve401` | Que la API exige token y no redirige |
| `FormularioWeb_SinTokenAntiforgery_Devuelve400` | El antiforgery (CSRF, capítulo 6) |
| `Comentar_PoneComoAutorAlUsuarioDelToken` | Toda la cadena: token → claims → `IUsuarioActual` → caso de uso → EF Core → JSON |
| `Salud_RespondeHealthy` | El *health check* |

Para la API, [AplicacionFixture.ClienteApiAsync](../../src/day-04/tests/GestorIncidencias.IntegrationTests/AplicacionFixture.cs) pide un **token real** a `/api/cuenta/token` con uno de los usuarios de demostración y lo añade a la cabecera `Authorization`.

Cosas a tener en cuenta:

| Cuidado | Por qué | Cómo lo resolvemos |
|---|---|---|
| `Program` tiene que ser accesible | La prueba hace `WebApplicationFactory<Program>` | `public partial class Program;` al final de Program.cs |
| La base de datos es **compartida** | La BD InMemory con el mismo nombre la ven todas las pruebas del proceso | El inicializador es idempotente; las pruebas no dependen del estado que deje otra |
| Arrancar la aplicación cuesta | Un par de segundos | Una sola instancia para la colección (`ICollectionFixture`) |
| El cliente sigue las redirecciones por defecto | No veríamos el 302 | `AllowAutoRedirect = false` |
| Cancelación | El analizador de xUnit avisa (xUnit1051) si no se pasa | `TestContext.Current.CancellationToken` |

> Para sustituir piezas en una prueba de integración (otra base de datos, un servicio externo falso, un esquema de autenticación de pruebas) se usa `WithWebHostBuilder(b => b.ConfigureTestServices(...))`. Está en la documentación oficial (referencias).

## 8.6 Otras herramientas de validación

| Herramienta | Para qué | En el curso |
|---|---|---|
| Ficheros **`.http`** | Probar la API a mano, versionado con el código | `GestorIncidencias.Web.http` (con token) |
| **OpenAPI** (`/openapi/v1.json`) | Contrato de la API; base para clientes generados y herramientas | Desde el día 2 |
| **Playwright** | Pruebas E2E en navegador real (Chromium, Firefox, WebKit) desde C# | — |
| **Testcontainers** | SQL Server (u otra dependencia) en un contenedor Docker para las pruebas | — |
| Analizadores (`dotnet build`) | Errores y avisos de calidad y seguridad al compilar (CA2254, xUnit1051...) | Siempre |
| **Integración continua** | `dotnet build` + `dotnet test` en cada *push* (GitHub Actions, Azure DevOps) | — |

### Pruebas como red de seguridad de la migración

Antes de migrar una pantalla de SIREI, una buena práctica es escribir **pruebas de caracterización**: pruebas de integración que fijan lo que hace **hoy** la aplicación (qué devuelve para estos datos, qué error da en este caso). Después de migrar, las mismas comprobaciones contra la aplicación nueva dicen si se ha cambiado algo sin querer.

## Referencias

> Enlaces comprobados el 8 de octubre de 2026.

- [Pruebas de integración en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/test/integration-tests?view=aspnetcore-10.0) — `WebApplicationFactory`, `ConfigureTestServices`, autenticación en pruebas.
- [Procedimientos recomendados para pruebas unitarias](https://learn.microsoft.com/es-es/dotnet/core/testing/unit-testing-best-practices)
- [Introducción a Microsoft.Testing.Platform](https://learn.microsoft.com/es-es/dotnet/core/testing/microsoft-testing-platform-intro)
- [Pruebas con dotnet test](https://learn.microsoft.com/es-es/dotnet/core/testing/unit-testing-with-dotnet-test) — Modos VSTest y Microsoft.Testing.Platform y el `global.json`.
- [Introducción a xUnit v3](https://xunit.net/docs/getting-started/v3/getting-started)
- [NSubstitute: primeros pasos](https://nsubstitute.github.io/help/getting-started/)
- [¿Qué es TimeProvider?](https://learn.microsoft.com/es-es/dotnet/standard/datetime/timeprovider-overview) — Incluye `FakeTimeProvider`.
- [Pruebas de código que usa EF Core](https://learn.microsoft.com/es-es/ef/core/testing/) — Por qué no usar InMemory y qué hacer en su lugar.
- [Playwright para .NET](https://playwright.dev/dotnet/) y [Testcontainers](https://testcontainers.com/)
