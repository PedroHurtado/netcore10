# Lab 3 — Resolución de problemas: el taller de averías

**Duración:** 35 min (en parejas) · **Teoría relacionada:** [04 — Problemas comunes](../04-problemas-comunes.md)

## Objetivo

Practicar el **diagnóstico**: provocar a propósito los fallos más frecuentes de una migración, observar el **síntoma** como lo vería un usuario o un compañero, y llegar a la **causa** con las herramientas del capítulo 4 (código HTTP, log, F12, pruebas).

## Cómo se juega

Trabaja sobre una **copia limpia de `src/day-05`** (no sobre tu copia de los labs: así sabes que todo funcionaba antes de romperlo).

- Por parejas: **A** provoca la avería sin que **B** mire (el cambio está en la tabla *Provocar*), y le cuenta solo el síntoma. **B** diagnostica. Después, cambiáis los papeles.
- Para cada avería, antes de abrir la solución, apuntad: **código HTTP**, **dónde habéis mirado** y **causa**.
- **Deshaced** cada avería antes de pasar a la siguiente (`git diff` o `Ctrl+Z`).

Usuarios: `ana@demo.local` (Tecnico), `luis@demo.local` (sin rol), `admin@demo.local` (Administrador); contraseña en `appsettings.Development.json`.

Haced **al menos 5** de las 8. Las marcadas con ⭐ son las más frecuentes en una migración real.

---

## Avería 1 ⭐ — "No arranca la pantalla de expedientes"

**Provocar:** en `GestorIncidencias.Application/DependencyInjection.cs`, comenta la línea `services.AddScoped<IExpedienteService, ExpedienteService>();`

**Síntoma:** la aplicación compila y arranca, el resto funciona, pero `/Sirei` da error.

<details>
<summary>Diagnóstico</summary>

- **Código:** 500. La página de excepción (Development) dice: `InvalidOperationException: Unable to resolve service for type 'GestorIncidencias.Application.Expedientes.IExpedienteService' while attempting to activate 'GestorIncidencias.Web.Areas.Sirei.Controllers.ExpedientesController'`.
- **Por qué no falla al arrancar:** los controladores no se registran como servicios: el contenedor solo descubre que le falta algo cuando tiene que **crear** el controlador.
- **Arreglo:** registrar el servicio. **Lección:** el mensaje dice exactamente qué tipo falta y quién lo pide; léelo entero.

</details>

## Avería 2 ⭐ — "Se pueden cerrar expedientes con trámites pendientes"

**Provocar:** en `GestorIncidencias.Infrastructure/Sirei/EfExpedienteRepository.cs`, borra `.Include(e => e.Tramites)` de `ObtenerPorIdAsync`.

**Síntoma:** un usuario informa de que ha cerrado el expediente **2026/000001**, que tiene trámites pendientes. No hay ningún error en pantalla ni en el log.

<details>
<summary>Diagnóstico</summary>

- **Código:** 302 (el guardado "funciona"). Ningún error: es el peor tipo de fallo.
- **Cómo encontrarlo:** `dotnet test` → falla `CasoPracticoTests.Sirei_CerrarUnExpedienteConTramitesPendientes_MuestraLaReglaDeNegocio`. Las pruebas **unitarias** del dominio siguen en verde: la regla está bien; lo que falla es que el agregado llega **incompleto**.
- **Por qué:** sin `Include`, EF Core no carga los trámites (no hay *lazy loading*), la colección llega **vacía**, `TieneTramitesPendientes` es `false` y la regla deja pasar el cierre.
- **Lección:** cuando una regla depende de los hijos, el repositorio debe cargar el **agregado completo**. Y por eso hacen falta pruebas de integración además de las unitarias.

</details>

## Avería 3 — "Los formularios de SIREI no hacen nada"

**Provocar:** en `GestorIncidencias.Web/Areas/Sirei/Views/_ViewImports.cshtml`, borra la línea `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`.

**Síntoma:** la búsqueda de expedientes se ve, pero los números de expediente no son enlaces y al guardar una ficha no pasa nada útil.

<details>
<summary>Diagnóstico</summary>

- **Código:** 200 en la página; si se envía un formulario, puede ser un 400.
- **Dónde mirar:** F12 → Elementos (o *Ver código fuente*): aparecen `<a asp-action="Detalle" asp-route-id="1">` y `<form asp-action=...>` **literales**. El navegador no conoce esos atributos: el enlace no tiene `href`, el formulario no tiene `action` ni token antiforgery.
- **Por qué:** los Tag Helpers se activan con `@addTagHelper`, y las vistas de un **área** no heredan el `_ViewImports.cshtml` de la carpeta `Views` raíz.
- **Lección:** cada área necesita su `_ViewImports.cshtml` y su `_ViewStart.cshtml`.

</details>

## Avería 4 ⭐ — "La URL de expedientes da 404"

**Provocar:** en `Areas/Sirei/Controllers/ExpedientesController.cs`, borra el atributo `[Area("Sirei")]`.

**Síntoma:** `/Sirei/Expedientes` responde 404. Un compañero dice que "en `/Expedientes` sí hace algo, pero da error".

<details>
<summary>Diagnóstico</summary>

- **`/Sirei/Expedientes` → 404:** la ruta del área exige `area = Sirei`, y el controlador ya no pertenece a ningún área.
- **`/Expedientes` → 500:** ahora lo atiende la ruta `default`, pero MVC busca las vistas fuera del área. El log lo dice todo: `The view 'Index' was not found. The following locations were searched: /Views/Expedientes/Index.cshtml, /Views/Shared/Index.cshtml, /Pages/Shared/Index.cshtml`.
- **Lección:** la **lista de rutas buscadas** del error de vistas es la pista: si no aparece `/Areas/Sirei/Views/...`, el controlador no está en el área.

</details>

## Avería 5 — "Desde SIREI no puedo volver a Incidencias"

**Provocar:** en `Views/Shared/_Layout.cshtml`, quita `asp-area=""` del enlace **Incidencias (MVC)**.

**Síntoma:** desde la portada el enlace funciona; desde cualquier página de SIREI o de Noticom, no hace nada.

<details>
<summary>Diagnóstico</summary>

- **Dónde mirar:** F12 → Elementos sobre el enlace: `href=""` (o sin `href`).
- **Por qué:** el Tag Helper **hereda el área** de la página actual. Desde `/Sirei/...` busca un controlador `Incidencias` en el área `Sirei`; no existe, así que no puede generar la URL.
- **Lección:** en un layout compartido por áreas, los enlaces "de fuera" llevan `asp-area=""`.

</details>

## Avería 6 ⭐ — "Al validar un lote sale un error 400"

**Provocar:** en `Areas/Noticom/Views/Lotes/_TablaLotes.cshtml`, cambia el formulario de **Validar** por uno escrito "a mano":

```cshtml
<form action="/Noticom/Lotes/Validar/@lote.Id" method="post">
```

**Síntoma:** como **ana**, en la pestaña Validacion, pulsar **Validar** da una página en blanco con **400 Bad Request**.

<details>
<summary>Diagnóstico</summary>

- **Código:** 400. En el log solo se ve la línea del HTTP logging (`StatusCode: 400`). Para ver el motivo, sube el nivel en `appsettings.Development.json`: `"Microsoft.AspNetCore": "Information"`. Aparecerá *Antiforgery token validation failed. The required antiforgery request token "__RequestVerificationToken" is not present*.
- **Dónde más:** F12 → Red → la petición POST → *Carga útil*: no hay campo `__RequestVerificationToken`.
- **Por qué:** el Tag Helper `<form>` solo añade el token cuando **él** genera la URL (`asp-action`, `asp-controller`...). Con un `action="..."` explícito, no lo añade, y el filtro global `AutoValidateAntiforgeryToken` (día 4) rechaza el POST.
- **Lección:** formularios con `asp-*`. Si un formulario tiene que llevar `action` a mano, `@Html.AntiForgeryToken()` dentro.

</details>

## Avería 7 — "En el servidor de pruebas no funcionan los botones de lotes"

**Provocar:** en `Areas/Noticom/Views/Lotes/Index.cshtml`, añade justo antes de `@section Scripts` un script "como los de Noticom":

```cshtml
<script>
    var modo = '@Model.Modo';
    console.log('Modo de la pantalla: ' + modo);
</script>
```

**Síntoma:** el compañero que lo ha escrito dice que "el `console.log` no sale".

<details>
<summary>Diagnóstico</summary>

- **Dónde mirar:** F12 → Consola: *Refused to execute inline script because it violates the following Content Security Policy directive: "script-src 'self'"*.
- **Por qué:** la CSP del día 2 (`appsettings.json`, `ContentSecurityPolicy`) solo permite scripts de **ficheros** del propio sitio. Razor no da ningún error: es el **navegador** el que lo bloquea.
- **Arreglo:** el dato, en un `data-*` (`<div id="lotes" data-modo="@Model.Modo">`) y el código, en `lotes.js`. No se "arregla" relajando la CSP con `'unsafe-inline'`: eso desactiva la protección contra XSS.
- **Lección:** después de migrar una vista con JavaScript, abrid **siempre** la consola.

</details>

## Avería 8 ⭐ — "Al volver de comer, la tabla de lotes muestra la página de login"

**Provocar:** en `wwwroot/js/noticom/lotes.js`, comenta la línea `headers: { "X-Requested-With": "XMLHttpRequest" }`. Abre `/Noticom` como **ana**, cierra la sesión en **otra pestaña** y, en la primera, pulsa **Buscar** (recarga antes con `Ctrl+F5` para tener el `.js` nuevo).

**Síntoma:** dentro del recuadro de la tabla aparece el formulario de inicio de sesión.

<details>
<summary>Diagnóstico</summary>

- **Dónde mirar:** F12 → Red: la petición `Tabla?...` responde **302** a `/Cuenta/Login?ReturnUrl=...`, y después hay un GET al login con **200**.
- **Por qué:** sin la cabecera, la cookie de autenticación trata la petición como una navegación normal y redirige. `fetch` sigue la redirección sin avisar y el código pinta el HTML recibido (el login). Con la cabecera, responde **401** y `lotes.js` lleva al usuario al login.
- **Lección:** jQuery enviaba `X-Requested-With` sin que nadie lo supiera; `fetch` no. Es la causa del famoso `"_Logon_"` del legacy.

</details>

---

## Para pensar (si sobra tiempo): concurrencia

No es una avería, sino un comportamiento nuevo. Abre el expediente **2026/000004** en dos pestañas (como ana en una y como luis en otra ventana privada). Cambia las observaciones y guarda en las dos, una detrás de otra.

1. ¿Qué ve el segundo? ¿Qué pasaba en SIREI?
2. Busca en el log el EventId **2003**. ¿Qué nivel tiene y por qué no es un error?
3. ¿Qué pasaría si el campo oculto `Formulario.Version` no se enviara?

<details>
<summary>Respuestas</summary>

1. El segundo recibe "Otra persona ha modificado el expediente..." y ve los datos del primero. En SIREI, el segundo pisaba al primero sin aviso.
2. `Warning`: no es un fallo de la aplicación, pero si se repite mucho indica que varias personas trabajan sobre lo mismo (un problema de organización, o una pantalla que bloquea poco).
3. Llegaría `Version = 0`: **todos** los guardados darían conflicto. Es uno de los errores típicos del Lab 1.

</details>

## Resumen: el método

| Paso | Herramienta | Qué te dice |
|---|---|---|
| 1. ¿Qué código HTTP? | F12 → Red, HTTP logging | En qué capa buscar (tabla del capítulo 4.1) |
| 2. ¿Qué dice el error entero? | Página de excepción, log | Casi siempre, la causa (y a veces la solución) |
| 3. ¿Qué ha llegado al servidor? | F12 → Red → Carga útil / Cabeceras | Nombres de campos, token, cabeceras |
| 4. ¿Qué ha generado el servidor? | F12 → Elementos / código fuente | Tag Helpers sin procesar, `href` vacíos, `data-*` |
| 5. ¿Qué ha bloqueado el navegador? | F12 → Consola | CSP, errores de JavaScript |
| 6. ¿Qué se ha roto sin avisar? | `dotnet test` | Reglas que ya no se cumplen |

## Referencias

> Enlaces comprobados el 9 de octubre de 2026.

- [Control de errores en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
- [Inyección de dependencias: resolución de servicios](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-10.0)
- [Carga de datos relacionados (EF Core)](https://learn.microsoft.com/es-es/ef/core/querying/related-data/)
- [Prevención de ataques CSRF](https://learn.microsoft.com/es-es/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0)
- [Content Security Policy (MDN)](https://developer.mozilla.org/es/docs/Web/HTTP/CSP)
