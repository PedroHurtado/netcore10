# Lab 3 — Pruebas unitarias y de integración

**Duración:** 22 min · **Teoría relacionada:** [08 — Pruebas](../08-pruebas.md)

## Objetivo

Escribir pruebas en los **tres** niveles que tiene la solución, para funcionalidades que todavía no las tienen:

1. **Dominio:** `Reabrir` y `CambiarCategoria` de la entidad `Incidencia`.
2. **Aplicación:** `CambiarCategoriaAsync` con una categoría que no existe, usando dobles (NSubstitute).
3. **Integración:** la política del [Lab 2](lab-02-politica-tecnicos.md), comprobada de punta a punta por la API.

Trabaja sobre tu copia del Lab 2. Si no terminaste el Lab 2, haz igualmente los pasos 1 y 2; en el paso 3 verás una prueba en **rojo**, que es justo lo que tiene que pasar cuando falta la funcionalidad.

## Paso 0 — Ejecutar las pruebas que ya hay (2 min)

```bash
cd mi-copia-day-04
dotnet test
```

✅ **Comprueba:** `total: 27`, `succeeded: 27`.

> Si ves `Testing with VSTest target is no longer supported by Microsoft.Testing.Platform...`, te falta el `global.json` en la carpeta de la solución (cópialo de `src/day-04`).

## Paso 1 — Dominio: `Reabrir` y `CambiarCategoria` (7 min)

Abre `tests/GestorIncidencias.UnitTests/Dominio/IncidenciaTests.cs` y añade estas pruebas **encima** de `AgregarComentario_AUnaAbierta_LoAnade`:

```csharp
    [Fact]
    public void Reabrir_UnaResuelta_VuelveAAbiertaYBorraLaFechaDeResolucion()
    {
        var incidencia = NuevaIncidencia();
        incidencia.Resolver(Ahora);

        var resultado = incidencia.Reabrir();

        Assert.True(resultado.Exito);
        Assert.Equal(EstadoIncidencia.Abierta, incidencia.Estado);
        Assert.Null(incidencia.FechaResolucion);
    }

    [Fact]
    public void Reabrir_UnaAbierta_DaConflicto()
    {
        var incidencia = NuevaIncidencia();

        var resultado = incidencia.Reabrir();

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Conflicto, resultado.Tipo);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(null)]          // null = quitar la categoría
    public void CambiarCategoria_DeUnaAbierta_LaCambia(int? categoriaId)
    {
        var incidencia = Incidencia.Crear("La impresora no imprime", null, Prioridad.Media, Ahora, categoriaId: 1).Valor!;

        var resultado = incidencia.CambiarCategoria(categoriaId);

        Assert.True(resultado.Exito);
        Assert.Equal(categoriaId, incidencia.CategoriaId);
    }
```

Y ahora **escribe tú** la última, siguiendo el mismo patrón:

> `CambiarCategoria_DeUnaCerrada_DaConflictoYConservaLaCategoria`: crea una incidencia con categoría 1, resuélvela, ciérrala, intenta cambiarla a la 2 y comprueba que falla con `Conflicto` y que la categoría **sigue siendo 1**.

<details>
<summary>Solución</summary>

```csharp
    [Fact]
    public void CambiarCategoria_DeUnaCerrada_DaConflictoYConservaLaCategoria()
    {
        var incidencia = Incidencia.Crear("La impresora no imprime", null, Prioridad.Media, Ahora, categoriaId: 1).Valor!;
        incidencia.Resolver(Ahora);
        incidencia.Cerrar();

        var resultado = incidencia.CambiarCategoria(2);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Conflicto, resultado.Tipo);
        Assert.Equal(1, incidencia.CategoriaId);
    }
```

</details>

✅ **Comprueba:** `dotnet test` → `total: 32`, todas en verde. (La `[Theory]` cuenta como **dos** pruebas.)

**Para pensar:** rompe la regla a propósito. En `Incidencia.CambiarCategoria`, comenta el `if (Estado == EstadoIncidencia.Cerrada) ...` y vuelve a ejecutar `dotnet test`. ¿Qué prueba falla y qué mensaje da? Deshaz el cambio.

<details>
<summary>Lo que verás</summary>

Falla `CambiarCategoria_DeUnaCerrada_DaConflictoYConservaLaCategoria` con algo como `Assert.False() Failure — Expected: False, Actual: True`. Esa es la utilidad de una prueba: si mañana alguien "simplifica" el método, se entera **al compilar y probar**, no cuando un usuario modifica una incidencia cerrada.

</details>

## Paso 2 — Aplicación: un doble que no se llama (5 min)

Abre `tests/GestorIncidencias.UnitTests/Aplicacion/IncidenciaServiceTests.cs` y añade **encima** de `ComentarAsync_PoneComoAutorAlUsuarioActualYGuarda`:

```csharp
    [Fact]
    public async Task CambiarCategoriaAsync_AUnaCategoriaQueNoExiste_FallaSinCargarNiGuardar()
    {
        // Arrange: el repositorio dirá que la categoría 99 no existe
        _repositorio.ExisteCategoriaAsync(99, Arg.Any<CancellationToken>()).Returns(false);
        var servicio = CrearServicio();

        // Act
        var resultado = await servicio.CambiarCategoriaAsync(1, 99, Ct);

        // Assert: error de validación, y ni se ha cargado la incidencia ni se ha guardado nada
        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.Tipo);
        await _repositorio.DidNotReceive().ObtenerPorIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        await _repositorio.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }
```

✅ **Comprueba:** `dotnet test` → `total: 33`, todas en verde.

**Piensa:** esta prueba no necesita una base de datos, ni categorías, ni incidencias. ¿Qué sería necesario para probar lo mismo en SIREI?

<details>
<summary>Respuesta</summary>

Un SQL Server con la tabla de categorías, datos de prueba, un servidor web (o al menos un `HttpContext` simulado para `Session`) y la página `.aspx`. En la práctica: nadie lo haría. Que `IncidenciaService` dependa de **interfaces** (`IIncidenciaRepository`) es lo que permite sustituirlas por un doble en dos líneas.

</details>

## Paso 3 — Integración: la política del Lab 2, de punta a punta (6 min)

Crea el fichero `tests/GestorIncidencias.IntegrationTests/AutorizacionTests.cs`:

```csharp
using System.Net;

namespace GestorIncidencias.IntegrationTests;

/// <summary>Laboratorio 3: la política GestionarIncidencias del laboratorio 2, comprobada de punta a punta.</summary>
[Collection("Aplicacion")]
public class AutorizacionTests(AplicacionFixture aplicacion)
{
    [Fact]
    public async Task Resolver_ConUnUsuarioSinRol_Devuelve403()
    {
        var cliente = await aplicacion.ClienteApiAsync("luis@demo.local");

        var respuesta = await cliente.PostAsync("/api/incidencias/3/resolver", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Iniciar_ConUnTecnico_Devuelve200()
    {
        var cliente = await aplicacion.ClienteApiAsync("ana@demo.local");

        // La incidencia 1 empieza Abierta. Si otra prueba la iniciara antes, esta daría 409:
        // en una base de datos compartida, cada prueba debería usar SUS datos (ver la ampliación).
        var respuesta = await cliente.PostAsync("/api/incidencias/1/iniciar", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
```

`[Collection("Aplicacion")]` hace que use **la misma** aplicación en memoria que el resto de pruebas de integración (`AplicacionFixture`), y `ClienteApiAsync` pide un token real con el usuario indicado.

✅ **Comprueba:** `dotnet test` → `total: 35`, todas en verde.

> **Si no hiciste el Lab 2:** `Resolver_ConUnUsuarioSinRol_Devuelve403` falla con `Expected: Forbidden, Actual: OK`: Luis **puede** resolver. Acabas de escribir la prueba **antes** que la funcionalidad. Haz el Lab 2 y vuelve a ejecutarla: pasará a verde.

## Errores típicos

| Síntoma | Causa probable | Solución |
|---|---|---|
| `CS1061 'Incidencia' does not contain a definition for 'CambiarCategoria'` | Estás sobre una copia del día 3 | Usa `src/day-04` (ya incorpora el lab 1 del día 3) |
| `CS0103 The name 'Ct' does not exist` | La prueba está fuera de la clase `IncidenciaServiceTests` | Revisa las llaves `{ }` |
| `CS0103 The name 'Ahora' does not exist` | La prueba de dominio se pegó en el fichero de aplicación (o al revés) | Paso 1 → `IncidenciaTests.cs`; paso 2 → `IncidenciaServiceTests.cs` |
| Aviso `xUnit1051 ... should use TestContext.Current.CancellationToken` | Se llamó a un método `async` sin pasar el token | Pasar `Ct` o `TestContext.Current.CancellationToken` |
| `Iniciar_ConUnTecnico_Devuelve200` falla con `Actual: Conflict` | Otra prueba (tuya) ha iniciado o resuelto la incidencia 1 antes | Usa otra incidencia o crea la tuya (ampliación 1) |
| `Resolver_ConUnUsuarioSinRol_Devuelve403` falla con `Actual: OK` | Falta el `[Authorize(Policy = ...)]` en la acción `Resolver` de la **API** | Lab 2, paso 4 |
| `System.InvalidOperationException: ... EnsureSuccessStatusCode ... 401` al pedir el token | El usuario o la contraseña no coinciden con `appsettings.Development.json` | Revisa `AplicacionFixture.Clave` |
| `error MSB3027: Could not copy ... The file is locked by: "GestorIncidencias.Web"` | La aplicación sigue ejecutándose con `dotnet run` y bloquea las DLL | Párala (`Ctrl+C`). Para las pruebas no hace falta: `WebApplicationFactory` la arranca en memoria |

## Ampliación opcional

1. **Pruebas independientes:** reescribe `Iniciar_ConUnTecnico_Devuelve200` para que **cree su propia incidencia** con `POST /api/incidencias` (lee el `Id` de la respuesta con `ReadFromJsonAsync<IncidenciaDto>(AplicacionFixture.Json)`) y la inicie. Así no depende del estado de la incidencia 1. (Ojo: en Development el límite de abiertas es 10.)
2. **Web con cookie:** escribe una prueba que compruebe que `luis` recibe un 302 a `/Cuenta/AccesoDenegado` al hacer `POST /Incidencias/Resolver/1`. Necesitarás iniciar sesión con el formulario: hacer `GET /Cuenta/Login`, extraer el `__RequestVerificationToken` del HTML y enviarlo en el POST. Es más trabajo que con la API: por eso muchas pruebas de integración de interfaces web usan un **esquema de autenticación de pruebas** (`ConfigureTestServices`; ver referencias del capítulo 8).

## Resumen: qué has hecho

| Nivel | Fichero | Pruebas nuevas | Qué protegen |
|---|---|---|---|
| Unitaria (Dominio) | `IncidenciaTests.cs` | 5 | Reglas de `Reabrir` y `CambiarCategoria` |
| Unitaria (Aplicación) | `IncidenciaServiceTests.cs` | 1 | La regla "la categoría existe" y que no se toca el repositorio |
| Integración | `AutorizacionTests.cs` | 2 | La política del Lab 2 en la API (403 / 200) |

## Referencias

> Enlaces comprobados el 8 de octubre de 2026.

- [Procedimientos recomendados para pruebas unitarias](https://learn.microsoft.com/es-es/dotnet/core/testing/unit-testing-best-practices)
- [Pruebas de integración en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
- [NSubstitute: comprobar llamadas recibidas](https://nsubstitute.github.io/help/getting-started/)
