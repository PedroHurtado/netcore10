# 9. Resumen del día 4 y avance del día 5

## Lo que hemos aprendido

| Concepto | En una frase | Dónde está en el proyecto |
|---|---|---|
| Web Forms → MVC | Cada evento de servidor se convierte en una acción (GET para leer, POST para cambiar) y la lógica del *code-behind* se reparte por capas | Capítulo 1 y Lab 1 |
| Controles → Razor | `GridView` → `<table>` + `@foreach`; `TextBox` → `<input asp-for>`; *Validators* → atributos | `Views/Incidencias/` |
| Sin ViewState | Lo que identifica la pantalla va en la ruta o la query string; lo demás se vuelve a leer | `Index(estado, texto, pagina)` |
| Session | Se activa a mano, guarda texto/bytes, no se bloquea; poca, por usuario y prescindible | `Estado/HistorialVisitas.cs` |
| `Cache[...]` → HybridCache | Caché en dos niveles con protección contra estampidas | `EfIncidenciaConsultas.ListarCategoriasAsync` |
| MVC 5 → Core | Mismas ideas; cambian el arranque (`Program.cs`), la configuración y todo lo de `System.Web` | Capítulo 3 |
| Herramientas | GitHub Copilot upgrade (recomendada), System.Web adapters y YARP; Upgrade Assistant, obsoleta | Capítulo 3 |
| Autenticación / autorización | ¿Quién eres? (401) / ¿Puedes hacerlo? (403) | `UseAuthentication`, `UseAuthorization` |
| Identity | Usuarios locales con hash, roles, claims y bloqueo | `Infrastructure/Identidad/` |
| *FallbackPolicy* | Todo exige sesión salvo lo marcado con `[AllowAnonymous]`: seguro por defecto | `Program.cs` |
| Roles y políticas | `[Authorize(Roles = ...)]` / `[Authorize(Policy = ...)]`: la política dice **qué** se protege | `UsuariosController`, Lab 2 |
| Cookie y token | Cookie para el navegador (con antiforgery); token *bearer* para la API (sin antiforgery) | `CuentaController`, `CuentaApiController` |
| `IUsuarioActual` | La aplicación sabe quién es el usuario sin conocer `HttpContext` | Autor de los comentarios |
| OAuth2 / OIDC | Delegar el inicio de sesión en un proveedor (Entra ID); la aplicación nunca ve la contraseña | Capítulo 5 |
| Autenticación legacy | Forms/Membership → Identity con `IPasswordHasher` que rehace los hashes; Windows → Negotiate | Capítulo 5 |
| CSRF | Token antiforgery (global), SameSite, nada de cambios por GET, API con token | `AutoValidateAntiforgeryToken` |
| XSS | Razor codifica, nada de `Html.Raw` con datos, CSP, cookie HttpOnly | Vistas + CSP del día 2 |
| *Open redirect* | `LocalRedirect` + `Url.IsLocalUrl` | Login |
| Logging estructurado | Plantillas con propiedades, `[LoggerMessage]` con EventId, JSON en producción | `IncidenciaLog.cs`, `appsettings.Production.json` |
| HTTP logging | Una línea por petición: método, ruta, código y duración (sin cabeceras ni cuerpos) | `AddHttpLogging` |
| Salud | `/salud` para balanceadores y monitorización | `MapHealthChecks` |
| Pruebas unitarias | Dominio sin nada; casos de uso con dobles (NSubstitute) y reloj falso | `tests/GestorIncidencias.UnitTests` |
| Pruebas de integración | La aplicación completa en memoria con `WebApplicationFactory` | `tests/GestorIncidencias.IntegrationTests` |

## Chuleta: proteger una funcionalidad nueva

1. **¿Quién puede?** Si es "cualquier usuario autenticado": nada (la *FallbackPolicy* lo cubre). Si es pública: `[AllowAnonymous]`. Si es restringida: una **política** con nombre en `Politicas` y `AddPolicy(...)` en Program.cs.
2. **Atributo en el endpoint**: `[Authorize(Policy = ...)]` en la acción MVC y en la de la API. En Razor Pages: en la clase, o `IAuthorizationService` dentro del *handler*.
3. **Formularios**: `<form method="post">` con Tag Helper (token antiforgery automático); nunca cambiar datos con GET.
4. **Vista**: ocultar con `IAuthorizationService.AuthorizeAsync(User, política)` (comodidad, no seguridad).
5. **Log**: las operaciones relevantes, con plantilla y sin datos sensibles.
6. **Prueba de integración**: un 403 para quien no puede y un 200 para quien sí.

## Chuleta: pruebas

```csharp
// Unitaria (Dominio)
[Fact]
public void Metodo_Situacion_Resultado()
{
    var incidencia = Incidencia.Crear("Título válido", null, Prioridad.Media, ahora).Valor!;   // Arrange
    var resultado = incidencia.Cerrar();                                                      // Act
    Assert.Equal(TipoError.Conflicto, resultado.Tipo);                                        // Assert
}

// Unitaria (Aplicación) con NSubstitute
_repositorio.ExisteCategoriaAsync(99, Arg.Any<CancellationToken>()).Returns(false);
await _repositorio.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());

// Integración
var cliente = await aplicacion.ClienteApiAsync("luis@demo.local");
var respuesta = await cliente.PostAsync("/api/incidencias/3/resolver", null, TestContext.Current.CancellationToken);
Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
```

```bash
dotnet test                                              # todas
dotnet test --project tests/GestorIncidencias.UnitTests  # solo un proyecto
```

## Avance del día 5

Mañana es el **caso práctico** (Módulo 7), que ocupa el día completo:

1. **Migración guiada de SIREI** (Web Forms): partiendo del análisis del día 3, migraremos pantallas reales aplicando lo de hoy (controles → Razor, eventos → acciones, estado, lógica al dominio, acceso a datos con EF Core, seguridad).
2. **Migración guiada de Noticom** (MVC 5): controladores, vistas y servicios con la tabla de equivalencias del capítulo 3.
3. **Ejercicios guiados**: creación de controladores, vistas y servicios.
4. **Resolución de problemas comunes**: los errores que aparecen al migrar y cómo diagnosticarlos (logs, pruebas, herramientas del navegador).

Traed el análisis de SIREI del día 3 y, si es posible, acceso al código de las dos aplicaciones.

## Referencias generales

> Enlaces comprobados el 8 de octubre de 2026.

- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0) — Base del caso práctico de mañana.
- [Introducción a la autorización](https://learn.microsoft.com/es-es/aspnet/core/security/authorization/introduction?view=aspnetcore-10.0)
- [Registro en .NET y ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/logging/?view=aspnetcore-10.0)
- [Pruebas de integración en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
