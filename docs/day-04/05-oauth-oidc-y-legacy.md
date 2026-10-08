# 5. OAuth2, OpenID Connect y migración de la autenticación legacy

Identity guarda los usuarios **dentro de la aplicación**. En una organización con muchas aplicaciones, lo normal es lo contrario: los usuarios están en un **directorio central** (Active Directory, Entra ID) y cada aplicación **delega** el inicio de sesión. Este capítulo explica cómo, y qué hacer con la autenticación de SIREI y Noticom.

No hay código en el proyecto para este capítulo: OIDC necesita un proveedor de identidad real (Entra ID, Keycloak...) y credenciales de la organización. Los fragmentos son los que se usarían.

## 5.1 Tres formas de identificar al usuario

| | Usuarios locales (**Identity**) | **Windows** (Kerberos/NTLM) | **OIDC** (Entra ID, Keycloak, ADFS...) |
|---|---|---|---|
| Dónde están los usuarios | Tablas `AspNet*` de la aplicación | Active Directory | Proveedor de identidad (IdP) |
| Quién comprueba la contraseña | La aplicación | Windows / IIS | El proveedor |
| Inicio de sesión único (SSO) | No | Sí, en la intranet | Sí, entre todas las aplicaciones del proveedor |
| MFA, políticas de contraseña | Lo programa la aplicación | Las del dominio | Las del proveedor (configuradas una vez) |
| Funciona fuera de la red interna | Sí | Mal (VPN) | Sí |
| Típico en | Aplicaciones con usuarios externos (ciudadanos, proveedores) | Intranets clásicas (¿SIREI?) | Aplicaciones corporativas modernas |
| En ASP.NET Core | `AddIdentity` (capítulo 4) | `AddNegotiate()` o IIS | `AddOpenIdConnect()` / Microsoft.Identity.Web |

> En la Administración es muy habitual que las aplicaciones internas usen **Windows** (o ya **Entra ID**) y que las de cara al ciudadano usen otro sistema (Cl@ve, certificado). Antes de migrar SIREI hay que saber **qué usa hoy y qué debe usar mañana**: es una decisión de la organización, no del equipo de desarrollo.

## 5.2 OAuth 2.0 y OpenID Connect en 5 minutos

**OAuth 2.0** es un protocolo de **autorización delegada**: permite que una aplicación obtenga un **token de acceso** para llamar a una API en nombre de un usuario (o en su propio nombre), sin conocer su contraseña.

**OpenID Connect (OIDC)** es una capa encima de OAuth 2.0 para **autenticación**: añade el **token de identidad** (*ID token*), que dice **quién es** el usuario.

| Pieza | Qué es | En el ejemplo |
|---|---|---|
| **Proveedor de identidad** (IdP, *authorization server*) | Quien autentica y emite tokens | Entra ID |
| **Cliente** | La aplicación que quiere identificar al usuario o llamar a una API | Gestor de Incidencias (web) |
| **API** (*resource server*) | Lo que se protege con tokens de acceso | La API de incidencias |
| **ID token** | JWT con la identidad del usuario. Para el **cliente** | `name`, `email`, `oid`... |
| **Access token** | Token para llamar a una **API** (a menudo un JWT con `aud`, `scp`, `roles`) | `Authorization: Bearer eyJ...` |
| **Scope** | Qué permiso pide el cliente | `api://gestor-incidencias/Incidencias.Leer` |

### El flujo que se usa en aplicaciones web: *Authorization Code* + PKCE

```
 Navegador            Gestor de Incidencias (cliente)          Entra ID (IdP)
     │  GET /Incidencias      │                                      │
     │───────────────────────▶│  no hay cookie → challenge OIDC      │
     │  302 a Entra ID        │                                      │
     │◀───────────────────────│                                      │
     │  login (contraseña + MFA)                                     │
     │──────────────────────────────────────────────────────────────▶│
     │  302 /signin-oidc?code=XYZ                                     │
     │◀──────────────────────────────────────────────────────────────│
     │  GET /signin-oidc?code=XYZ                                     │
     │───────────────────────▶│  canjea el code (servidor a servidor) │
     │                        │──────────────────────────────────────▶│
     │                        │  ID token (+ access token)            │
     │                        │◀──────────────────────────────────────│
     │  302 /Incidencias + cookie de la aplicación                    │
     │◀───────────────────────│                                      │
```

La aplicación **nunca ve la contraseña**. Tras el canje, crea **su propia cookie** (como con Identity) y a partir de ahí todo es igual: `User`, claims, `[Authorize]`, políticas.

| Flujo | Para qué |
|---|---|
| **Authorization Code + PKCE** | Aplicaciones web y SPA con usuario. El recomendado |
| **Client Credentials** | Servicio que llama a otro servicio **sin usuario** (un proceso nocturno, una integración) |
| ~~Implicit~~, ~~Resource Owner Password~~ | **Desaconsejados**: exponen tokens o contraseñas |

### Con ASP.NET Core

Aplicación web que inicia sesión con un proveedor OIDC (genérico):

```csharp
builder.Services.AddAuthentication(o =>
    {
        o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;          // la sesión: cookie propia
        o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;        // el login: OIDC
    })
    .AddCookie()
    .AddOpenIdConnect(o =>
    {
        o.Authority = "https://login.microsoftonline.com/{tenant}/v2.0";             // el proveedor
        o.ClientId = builder.Configuration["Oidc:ClientId"];
        o.ClientSecret = builder.Configuration["Oidc:ClientSecret"];                 // ¡secreto: nunca en appsettings.json!
        o.ResponseType = "code";                                                       // Authorization Code (+ PKCE por defecto)
        o.MapInboundClaims = false;                                                    // claims con sus nombres OIDC (name, email...)
        o.TokenValidationParameters.NameClaimType = "name";
        o.TokenValidationParameters.RoleClaimType = "roles";
    });
```

Con **Entra ID**, la biblioteca **Microsoft.Identity.Web** lo reduce a una línea y una sección de configuración:

```csharp
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
```

Una **API** que acepta *access tokens* JWT del proveedor:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = "https://login.microsoftonline.com/{tenant}/v2.0";
        o.Audience = "api://gestor-incidencias";      // el token tiene que ser PARA esta API
    });
```

> **Qué cambiaría en nuestro proyecto:** Program.cs (los esquemas) e Infrastructure (desaparece `IdentidadDbContext`). Los roles vendrían como claims del proveedor (roles de aplicación de Entra ID). `IUsuarioActual`, los casos de uso, las políticas y los `[Authorize]` **no cambiarían**: esa es la ventaja de haber separado "quién es" de "qué puede hacer".

### Token de Identity frente a JWT de un proveedor

| | Token de Identity (`/api/cuenta/token`, capítulo 4) | Access token JWT de un IdP |
|---|---|---|
| Formato | Opaco (cifrado con Data Protection) | JWT firmado (cabecera.cuerpo.firma) |
| Quién lo valida | **Solo** la aplicación que lo emitió | Cualquier API que confíe en el proveedor |
| Revocación, MFA, SSO | No | Sí (gestionado por el proveedor) |
| Para | Una API propia con usuarios propios | Ecosistema de aplicaciones y APIs |

## 5.3 Windows Authentication (intranets)

Si SIREI usa autenticación de Windows (`<authentication mode="Windows" />` en `Web.config`), en ASP.NET Core:

```csharp
// Detrás de IIS (lo habitual): IIS hace la autenticación; basta con activarla en IIS y no añadir esquemas.
// Con Kestrel, o para que funcione igual en desarrollo:
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
```

`User.Identity.Name` es `DOMINIO\usuario` y los **grupos de AD** llegan como roles: `[Authorize(Roles = @"DOMINIO\SIREI-Tecnicos")]`. Mejor aún: una **política** con ese nombre de grupo, para no repetirlo por todo el código.

## 5.4 Migrar la autenticación legacy

| Legacy | Qué hay | Cómo se migra |
|---|---|---|
| **Forms Authentication** + `<authentication mode="Forms">` | Cookie `.ASPXAUTH`, `FormsAuthentication.SetAuthCookie`, `Login.aspx` | Cookie de Identity (o de `AddCookie`) + controlador de login |
| **Membership / Roles** (`SqlMembershipProvider`) | Tablas `aspnet_Users`, `aspnet_Membership`, `aspnet_Roles`; contraseñas con hash SHA1 (o, peor, en claro o cifradas) | Script que copia usuarios y roles a `AspNetUsers`/`AspNetRoles` + un `IPasswordHasher` que entiende el hash antiguo (ver abajo) |
| **ASP.NET Identity 2** (MVC 5, OWIN) — probablemente Noticom | Tablas `AspNetUsers`... casi iguales | Script de esquema (columnas nuevas: `NormalizedUserName`, `NormalizedEmail`, `ConcurrencyStamp`, `LockoutEnd`). **Los hashes de Identity 2 se verifican sin cambios** y se rehacen al formato nuevo en el siguiente inicio de sesión |
| **Windows** | `<authentication mode="Windows">` | `AddNegotiate` / IIS (5.3) |
| Login "casero" (`Session["Usuario"] = ...`, como en el Lab 3 de ayer) 🚩 | Tabla propia de usuarios, comprobación a mano, sesión | Identity o un IdP. La tabla propia se migra como en Membership |

### Contraseñas antiguas: rehacer el hash sin pedir a nadie que la cambie

No se puede "convertir" un hash (es de un solo sentido). La técnica es verificar con el algoritmo **antiguo** la primera vez y guardar el **nuevo** en ese momento:

```csharp
public class PasswordHasherLegacy(IOptions<PasswordHasherOptions> opciones) : PasswordHasher<IdentityUser>(opciones)
{
    public override PasswordVerificationResult VerifyHashedPassword(IdentityUser usuario, string hash, string clave)
    {
        if (!hash.StartsWith("LEGACY:"))                                      // ya está en formato Identity
            return base.VerifyHashedPassword(usuario, hash, clave);

        return HashMembership.Verificar(hash["LEGACY:".Length..], clave)     // algoritmo de SqlMembershipProvider
            ? PasswordVerificationResult.SuccessRehashNeeded                  // ← Identity guardará el hash NUEVO
            : PasswordVerificationResult.Failed;
    }
}

// Program.cs / Infrastructure
services.AddScoped<IPasswordHasher<IdentityUser>, PasswordHasherLegacy>();
```

`SuccessRehashNeeded` le dice a `SignInManager` que la contraseña es correcta **y** que debe volver a calcular el hash con el algoritmo actual. Al cabo de unos meses, los usuarios activos estarán migrados; a los que no hayan entrado se les fuerza a restablecer la contraseña.

### Durante la convivencia

Si SIREI y la aplicación nueva conviven (*Strangler Fig*), el usuario **no puede tener que iniciar sesión dos veces**. Opciones (System.Web adapters, día 3):

| Opción | Cuándo |
|---|---|
| **Autenticación remota** | La aplicación nueva pregunta a la vieja "¿quién es este usuario?" en cada petición. Funciona con cualquier autenticación legacy |
| **Cookie compartida** | Si el legacy ya usa cookies OWIN (MVC 5 con Identity 2): las dos aplicaciones leen la misma cookie con Data Protection compartido |
| **Migrar ambas a OIDC** | Si la organización ya tiene IdP: las dos aplicaciones delegan en él y el SSO resuelve la convivencia |

## Referencias

> Enlaces comprobados el 8 de octubre de 2026.

- [Protocolos OAuth 2.0 y OpenID Connect en la Plataforma de identidad de Microsoft](https://learn.microsoft.com/es-es/entra/identity-platform/v2-protocols)
- [Flujo de código de autorización](https://learn.microsoft.com/es-es/entra/identity-platform/v2-oauth2-auth-code-flow)
- [Configurar la autenticación web de OpenID Connect en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0)
- [Configurar la autenticación JWT Bearer en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
- [Documentación de Microsoft Identity Web](https://learn.microsoft.com/es-es/entra/msidweb/)
- [Autenticación de Windows en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/security/authentication/windowsauth?view=aspnetcore-10.0)
- [Migración de la autenticación y de Identity a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/examples/identity?view=aspnetcore-10.0)
- [Migración de la autenticación de pertenencia (Membership) a ASP.NET Core Identity](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/areas/membership?view=aspnetcore-10.0)
- [Migración de la autenticación (System.Web adapters)](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/areas/authentication?view=aspnetcore-10.0) — Autenticación remota y cookie compartida.
