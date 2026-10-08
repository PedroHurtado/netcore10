# 4. Autenticación y autorización con ASP.NET Core Identity

Hasta ayer, cualquiera podía hacer cualquier cosa en el Gestor de Incidencias y el autor de un comentario era un campo de texto libre. Hoy la aplicación **sabe quién es el usuario** y **decide qué puede hacer**.

## 4.1 Dos preguntas distintas

| | Autenticación (*authentication*, **AuthN**) | Autorización (*authorization*, **AuthZ**) |
|---|---|---|
| Pregunta | **¿Quién eres?** | **¿Puedes hacer esto?** |
| Resultado | Un usuario (`ClaimsPrincipal`) con sus **claims** | Sí / no |
| Si falla | **401** → "identifícate" (en web: redirección al login) | **403** → "no tienes permiso" (en web: página de acceso denegado) |
| Middleware | `UseAuthentication()` | `UseAuthorization()` |
| Se declara con | Esquemas: cookie, token, Windows, OIDC... | `[Authorize]`, roles, políticas |

### Claims: la "tarjeta de identidad"

Tras autenticarse, `HttpContext.User` es un `ClaimsPrincipal`: una lista de **afirmaciones** sobre el usuario.

```
 User (ClaimsPrincipal)
 ├── name               = ana@demo.local
 ├── nameidentifier     = 3f2a...            (Id en AspNetUsers)
 ├── role               = Tecnico
 ├── nombre_completo    = Ana García         (claim propio: TiposClaim.NombreCompleto)
 └── AspNet.Identity.SecurityStamp = ...
```

Los **roles son claims** de tipo `role`. Por eso `User.IsInRole("Tecnico")` y `[Authorize(Roles = "Tecnico")]` funcionan igual venga el usuario de Identity, de Windows o de Entra ID.

> **Equivalencia con Web Forms / MVC 5:** `User.Identity.Name` y `User.IsInRole(...)` se escriben **igual**. Lo que cambia es de dónde salen (Forms Authentication + Membership/Roles, o OWIN + Identity 2) y cómo se configura.

## 4.2 ASP.NET Core Identity

**Identity** es la biblioteca de Microsoft para gestionar **usuarios locales** (los que guarda la propia aplicación):

| Identity te da | Clase |
|---|---|
| Usuarios, con la contraseña guardada como **hash** (PBKDF2 con sal; nunca la contraseña) | `UserManager<IdentityUser>` |
| Roles | `RoleManager<IdentityRole>` |
| Iniciar y cerrar sesión, comprobar la contraseña, **bloqueo** tras N fallos | `SignInManager<IdentityUser>` |
| Claims por usuario y por rol | `UserManager.AddClaimAsync` |
| Tokens para confirmar correo, restablecer contraseña, 2FA | `AddDefaultTokenProviders()` |
| Las tablas (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`...) | `IdentityDbContext` (EF Core) |

Identity **no** da: la gestión de usuarios de una organización (eso es un directorio: Active Directory, Entra ID), ni inicio de sesión único entre aplicaciones (eso es OIDC, capítulo 5).

### Dónde está en la arquitectura

```
GestorIncidencias.Application
├── Abstracciones/IUsuarioActual.cs      ← "¿quién hace la petición?" (puerto)
└── Seguridad/Roles.cs                   ← Roles.Tecnico, Roles.Administrador, TiposClaim.NombreCompleto

GestorIncidencias.Infrastructure
└── Identidad/
    ├── IdentidadDbContext.cs            ← tablas AspNet* (un DbContext SEPARADO del de incidencias)
    └── InicializadorIdentidad.cs        ← roles y usuarios de demostración
    (+ AddIdentidad() en DependencyInjection.cs: reglas de contraseña y bloqueo)

GestorIncidencias.Web
├── Program.cs                           ← cookie, token, FallbackPolicy, orden del pipeline
├── Seguridad/UsuarioActualHttp.cs       ← implementación de IUsuarioActual (lee HttpContext.User)
├── Controllers/CuentaController.cs      ← login / logout / acceso denegado
├── Controllers/Api/CuentaApiController.cs ← token para la API
└── Controllers/UsuariosController.cs    ← [Authorize(Roles = Administrador)]
```

**Domain no sabe nada de usuarios** y **Application solo conoce `IUsuarioActual`**. Identity es un detalle técnico: si mañana los usuarios vienen de Entra ID, cambian Infrastructure y Program.cs, no los casos de uso.

### La configuración

En [Infrastructure/DependencyInjection.cs](../../src/day-04/GestorIncidencias.Infrastructure/DependencyInjection.cs):

```csharp
services.AddDbContext<IdentidadDbContext>(opt => opt.UseInMemoryDatabase("GestorIncidenciasIdentidad"));

services.AddIdentity<IdentityUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 10;
        o.Lockout.MaxFailedAccessAttempts = 5;                       // 5 fallos seguidos...
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);  // ...bloquean la cuenta 5 minutos
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<IdentidadDbContext>()
    .AddDefaultTokenProviders();
```

`AddIdentity` registra la autenticación por **cookie** (`Identity.Application`) como esquema por defecto. Lo que depende de la web se ajusta en [Program.cs](../../src/day-04/GestorIncidencias.Web/Program.cs):

```csharp
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = ".GestorIncidencias.Auth";
    o.LoginPath = "/Cuenta/Login";                    // sin sesión → 302 aquí (con ?ReturnUrl=...)
    o.AccessDeniedPath = "/Cuenta/AccesoDenegado";    // con sesión pero sin permiso → 302 aquí
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.SlidingExpiration = true;
});
```

> **¿Y con SQL Server?** `IdentidadDbContext` tendría sus propias **migraciones** (día 3): `dotnet ef migrations add Inicial --context IdentidadDbContext ...`. Es una de las razones de tenerlo separado.

### Usuarios de demostración

[InicializadorIdentidad](../../src/day-04/GestorIncidencias.Infrastructure/Identidad/InicializadorIdentidad.cs) crea los roles siempre y, en Development, tres usuarios con la contraseña de `Incidencias:ClaveUsuariosDemo` (`appsettings.Development.json`):

| Usuario | Nombre (claim) | Rol | Qué puede hacer hoy |
|---|---|---|---|
| `ana@demo.local` | Ana García | `Tecnico` | Todo lo de las incidencias |
| `luis@demo.local` | Luis Pérez | — | Todo lo de las incidencias (el [Lab 2](labs/lab-02-politica-tecnicos.md) se lo restringe) |
| `admin@demo.local` | Marta Ruiz | `Administrador` | Además, ver `/Usuarios` |

Se crean con `UserManager.CreateAsync(usuario, clave)`: así se aplican las reglas de contraseña y se guarda el **hash**. Nunca se insertan usuarios escribiendo en las tablas.

## 4.3 El inicio de sesión

[CuentaController](../../src/day-04/GestorIncidencias.Web/Controllers/CuentaController.cs):

```csharp
var resultado = await signInManager.PasswordSignInAsync(
    formulario.Correo, formulario.Clave, formulario.Recordarme, lockoutOnFailure: true);

if (resultado.Succeeded)
    return LocalRedirect(Url.IsLocalUrl(formulario.ReturnUrl) ? formulario.ReturnUrl : "/");

if (resultado.IsLockedOut)
    ModelState.AddModelError(string.Empty, "La cuenta está bloqueada temporalmente...");
else
    ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
```

Cinco detalles de seguridad que conviene copiar siempre:

| Detalle | Por qué |
|---|---|
| `lockoutOnFailure: true` | Frena los ataques de fuerza bruta (5 fallos → 5 minutos bloqueada) |
| El **mismo mensaje** si el usuario no existe o la contraseña es incorrecta | No revelar qué correos están dados de alta |
| `LocalRedirect` + `Url.IsLocalUrl` | Evita el *open redirect*: `?ReturnUrl=https://sitio-falso.example` |
| Logout por **POST** con antiforgery | Un GET se podría disparar desde un `<img>` de otra web |
| Log de inicios de sesión correctos y fallidos, **sin la contraseña** | Auditoría y detección de ataques (capítulo 7) |

La cookie que emite es **HttpOnly** (JavaScript no la puede leer: un XSS no la puede robar) y **SameSite=Lax** (el navegador no la envía en un POST desde otra web: primera barrera contra CSRF, capítulo 6). Está **cifrada y firmada** con Data Protection: el usuario no puede cambiar sus roles editándola.

## 4.4 Autorización: seguro por defecto

### La política por defecto (*FallbackPolicy*)

```csharp
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
```

La *FallbackPolicy* se aplica a todo endpoint que **no** tenga ni `[Authorize]` ni `[AllowAnonymous]`. Resultado: **todo exige haber iniciado sesión** salvo lo que se marque como público. Una acción nueva **nace protegida**: el error de olvidar un atributo deja algo cerrado, no abierto.

Lo público en el proyecto, marcado explícitamente:

| Qué | Cómo |
|---|---|
| Portada (`HomeController`, incluida la página de error) | `[AllowAnonymous]` en la clase |
| Login y acceso denegado (`CuentaController`) | `[AllowAnonymous]` |
| Token de la API (`CuentaApiController`) | `[AllowAnonymous]` |
| CSS (`MapStaticAssets`) | `.AllowAnonymous()` — ⚠️ sin esto, la página de login sale **sin estilos**: los estáticos también son endpoints |
| `/salud` | `.AllowAnonymous()` |

> **Equivalencia con Web Forms:** es el `<authorization><deny users="?" /></authorization>` del `Web.config` raíz, con `<location path="Login.aspx">` para abrir excepciones. Pero ahora está en código, junto al endpoint, y no depende de la ruta física del fichero.

### Por rol

```csharp
[Authorize(Roles = Roles.Administrador)]
public class UsuariosController(UserManager<IdentityUser> gestorUsuarios) : Controller
```

`[Authorize(Roles = "A,B")]` = **A o B**. Dos atributos `[Authorize(Roles = "A")]` y `[Authorize(Roles = "B")]` = **A y B**.

### Por política

Una **política** es un nombre que agrupa requisitos. Se registra una vez y se usa en muchos sitios:

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("GestionarIncidencias", p => p.RequireRole(Roles.Tecnico, Roles.Administrador));

[Authorize(Policy = "GestionarIncidencias")]
public async Task<IActionResult> Resolver(int id, CancellationToken ct) => ...
```

| | Roles en el atributo | Política |
|---|---|---|
| Cambiar quién puede hacerlo | Buscar y cambiar **todos** los atributos | Cambiar **una** línea en Program.cs |
| Requisitos | Solo roles | Roles, claims, autenticación, requisitos propios (`IAuthorizationRequirement`) |
| Legibilidad | `Roles = "Tecnico,Administrador"` | `Policy = "GestionarIncidencias"`: dice **qué** se protege, no **quién** |

Es lo que haréis en el [Lab 2](labs/lab-02-politica-tecnicos.md).

### En las vistas: mostrar u ocultar

```cshtml
@if (User.IsInRole(Roles.Administrador))
{
    <a asp-controller="Usuarios" asp-action="Index">Usuarios</a>
}
```

O, con políticas, inyectando `IAuthorizationService`:

```cshtml
@inject IAuthorizationService Autorizacion
@if ((await Autorizacion.AuthorizeAsync(User, "GestionarIncidencias")).Succeeded) { ... }
```

> ⚠️ **Ocultar un botón no es proteger.** Cualquiera puede enviar el POST a mano (herramientas del navegador, `curl`). La protección está en el `[Authorize]` del endpoint; la vista solo evita enseñar lo que no se puede usar.

### Razor Pages: cuidado

En Razor Pages, `[Authorize]` se pone en la **clase** `PageModel` (o con convenciones en `AddRazorPages`). **En un método *handler* (`OnPostResolverAsync`) no tiene efecto.** Si cada *handler* de la página necesita un permiso distinto, se comprueba dentro con `IAuthorizationService` y se devuelve `Forbid()` (lo haréis en el Lab 2).

### ¿Y "solo el autor puede editar su comentario"?

Eso depende **del recurso concreto**, no solo del usuario: es **autorización basada en recursos** (`IAuthorizationService.AuthorizeAsync(User, comentario, "EditarComentario")` con un `AuthorizationHandler<Requisito, Comentario>`). Queda fuera del día de hoy; está en las referencias.

## 4.5 El orden del pipeline

```csharp
app.UseRouting();
app.UseHttpLogging();
app.UseAuthentication();    // lee la cookie o el token → HttpContext.User
app.UseAuthorization();     // ¿puede llegar al endpoint? → sigue, 401/403 o redirección
app.UseSession();
app.UseOutputCache();       // DESPUÉS de autorizar: una página en caché nunca se salta la autorización
// ...
app.MapControllerRoute(...);
```

| Si... | Pasa... |
|---|---|
| `UseAuthorization` va **antes** de `UseRouting` | No sabe qué endpoint es → no ve su `[Authorize]`. ASP.NET Core lo detecta y lanza una excepción en la primera petición ("...contains authorization metadata, but a middleware was not found that supports authorization") |
| `UseAuthentication` va **después** de `UseAuthorization` | `User` siempre está vacío → todo redirige al login |
| `UseOutputCache` va **antes** de `UseAuthorization` | Una página guardada en caché se serviría **sin comprobar permisos** |

> Además, la política de Output Cache del día 2 ([CacheHtmlPolicy](../../src/day-04/GestorIncidencias.Web/Cache/CacheHtmlPolicy.cs)) **nunca guarda ni sirve** páginas de usuarios autenticados (tienen su nombre, sus botones...). Con la *FallbackPolicy*, hoy solo se cachea la portada para los anónimos.

## 4.6 La API: tokens en lugar de cookies

El navegador usa la **cookie**. Pero un cliente de la API (el fichero `.http`, un script, otra aplicación) no rellena formularios de login. Para ellos, [CuentaApiController](../../src/day-04/GestorIncidencias.Web/Controllers/Api/CuentaApiController.cs) emite un **token**:

```http
POST /api/cuenta/token
Content-Type: application/json

{ "correo": "ana@demo.local", "clave": "..." }

→ 200 { "tokenType": "Bearer", "accessToken": "CfDJ8...", "expiresIn": 3600, "refreshToken": "..." }
```

y cada petición posterior lo envía en una cabecera:

```http
GET /api/incidencias
Authorization: Bearer CfDJ8...
```

La API **solo acepta el token** (no la cookie):

```csharp
[ApiController]
[Authorize(AuthenticationSchemes = Esquemas.Token)]   // "Identity.Bearer"
[IgnoreAntiforgeryToken]
public class IncidenciasApiController(...) : ControllerBase
```

| | Cookie (web) | Token (API) |
|---|---|---|
| Lo envía | El navegador, **automáticamente** | El cliente, **a mano** (cabecera `Authorization`) |
| Riesgo CSRF | Sí → antiforgery | No → `[IgnoreAntiforgeryToken]` |
| Sin credenciales | 302 al login | **401** |
| Sin permiso | 302 a acceso denegado | **403** |

El token de Identity es **opaco**: está cifrado con Data Protection y **solo lo entiende esta aplicación**. Para que otras aplicaciones confíen en un token hace falta un proveedor de identidad y **OAuth2/OIDC** (capítulo 5).

> **Novedad de .NET 10:** si una API sí aceptara la cookie, los endpoints de API conocidos (`[ApiController]`) ya **no redirigen al login**: devuelven 401/403 directamente. Antes había que configurarlo a mano.

## 4.7 Quién es el usuario en la capa de Aplicación

El autor de un comentario ya no lo escribe el usuario: lo pone el caso de uso.

```csharp
// Application/Incidencias/IncidenciaService.cs
var comentario = incidencia.AgregarComentario(comando.Texto, usuario.Nombre, reloj.GetUtcNow());
```

`usuario` es un `IUsuarioActual` (Application). La implementación web, [UsuarioActualHttp](../../src/day-04/GestorIncidencias.Web/Seguridad/UsuarioActualHttp.cs), lee el claim `nombre_completo` de `HttpContext.User`. En las pruebas (capítulo 8) se sustituye por un doble que dice "soy Ana García".

| Antes (día 3) | Ahora (día 4) |
|---|---|
| `ComentarIncidenciaComando(Texto, Autor)` | `ComentarIncidenciaComando(Texto)` |
| Campo "Autor" en el formulario | "Comentarás como **Ana García**" |
| Cualquiera podía firmar como otro | El autor sale de la identidad autenticada |

> El caso de uso sabe **quién** hace la operación, pero no decide **si puede** hacerla: eso lo declara la capa Web con `[Authorize]`. Si una regla de permisos dependiera de los datos ("solo el técnico asignado puede resolver"), entonces sí entraría en la aplicación o en el dominio.

## Referencias

> Enlaces comprobados el 8 de octubre de 2026.

- [Introducción a la autenticación](https://learn.microsoft.com/es-es/aspnet/core/security/authentication/?view=aspnetcore-10.0) — Esquemas, challenge y forbid.
- [Introducción a Identity](https://learn.microsoft.com/es-es/aspnet/core/security/authentication/identity?view=aspnetcore-10.0)
- [Introducción a la autorización](https://learn.microsoft.com/es-es/aspnet/core/security/authorization/introduction?view=aspnetcore-10.0), [por roles](https://learn.microsoft.com/es-es/aspnet/core/security/authorization/roles?view=aspnetcore-10.0) y [por políticas](https://learn.microsoft.com/es-es/aspnet/core/security/authorization/policies?view=aspnetcore-10.0)
- [Aplicación con datos de usuario protegidos por autorización](https://learn.microsoft.com/es-es/aspnet/core/security/authorization/secure-data?view=aspnetcore-10.0) — Incluye la *FallbackPolicy* "requerir usuarios autenticados".
- [Autorización basada en recursos](https://learn.microsoft.com/es-es/aspnet/core/security/authorization/resource-based?view=aspnetcore-10.0)
- [Convenciones de autorización de Razor Pages](https://learn.microsoft.com/es-es/aspnet/core/razor-pages/security/authorization/conventions?view=aspnetcore-10.0)
- [Uso de Identity para proteger un back-end de API web](https://learn.microsoft.com/es-es/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0) — Los tokens *bearer* de Identity.
- [Cookie: redirecciones de inicio de sesión deshabilitadas para endpoints de API](https://learn.microsoft.com/es-es/aspnet/core/breaking-changes/10/cookie-authentication-api-endpoints?view=aspnetcore-10.0) — El cambio de .NET 10.
- [Autenticación con cookies sin Identity](https://learn.microsoft.com/es-es/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0)
