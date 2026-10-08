# 5. Cierre del curso

Cinco días, siete módulos y una aplicación que ha crecido desde una Minimal API con datos en memoria hasta una solución en capas con tres interfaces, seguridad, logs, pruebas y dos aplicaciones legacy migradas. Este capítulo recoge **qué nos llevamos**, **cómo seguir** con SIREI y Noticom, y una **autoevaluación**.

## 5.1 El curso en una tabla

| Día | Módulos | Lo esencial | Dónde está |
|---|---|---|---|
| **1** | M1, M2 (1ª) | .NET 10 LTS; ASP.NET Core es otro modelo (sin `System.Web`); el **pipeline** de middleware; **DI** con tres tiempos de vida; **configuración** por capas y opciones validadas | [src/day-01](../../src/day-01/) |
| **2** | M2 (2ª) | **Clean Architecture**: el dominio no depende de nada; controladores MVC, vistas Razor, Razor Pages y API sobre los **mismos casos de uso**; Post-Redirect-Get; CSP | [src/day-02](../../src/day-02/) |
| **3** | M4, M3 (1ª) | **EF Core**: configuración fluida, relaciones, agregados, migraciones; **leer ≠ escribir** (consultas con proyección / repositorio con seguimiento); paginación y recuentos en SQL; **análisis** de una aplicación legacy y estrategia (completa o *Strangler Fig*) | [src/day-03](../../src/day-03/) |
| **4** | M3 (2ª), M5, M6 | Web Forms → MVC (eventos → acciones, ViewState → URL); estado; MVC 5 → Core; **Identity**, políticas, cookie y token; CSRF, XSS, *open redirect*; **logs estructurados**; pruebas unitarias y de integración | [src/day-04](../../src/day-04/) |
| **5** | M7 | **Caso práctico**: SIREI y Noticom migrados con la misma receta; resolución de problemas | [src/day-05](../../src/day-05/) |

### Diez ideas para quedarse

1. **Cada petición empieza de cero.** Lo que necesita la pantalla va en la ruta, la query string o el formulario; lo demás se vuelve a leer.
2. **Leer con GET, cambiar con POST** (con antiforgery) y redirigir después (PRG).
3. **Las reglas de negocio viven en el dominio** y devuelven un `Resultado`. Ni en eventos, ni en vistas, ni en JavaScript.
4. **Ocultar no es proteger.** Lo que la interfaz esconde, el servidor lo prohíbe: dominio + `[Authorize(Policy = ...)]`.
5. **Seguro por defecto**: `FallbackPolicy`, antiforgery global, CSP estricta, cookies HttpOnly. Lo público se marca explícitamente.
6. **El controlador es fino**: enlaza, llama al caso de uso, elige vista o redirección.
7. **Nunca la tabla entera**: proyección, filtros y paginación en la base de datos; `Include` cuando la regla necesita a los hijos.
8. **La base de datos del legacy manda** mientras convivan: el mapeo se adapta a ella, no al revés.
9. **Log estructurado** con plantillas y EventId; nunca contraseñas ni datos personales.
10. **Sin pruebas, la migración es a ciegas.** Una prueba unitaria por regla, una de integración por pantalla.

## 5.2 Lista de comprobación para migrar cualquier pantalla

Imprimidla y usadla en cada *pull request* de migración (resume el [capítulo 1](01-metodo-migracion.md)):

| ✔ | Comprobación |
|---|---|
| ☐ | Ficha de la pantalla legacy rellena (operaciones, entradas, datos, reglas, seguridad, 🚩) |
| ☐ | Contrato de URLs: GET para leer, POST para cambiar, id en la ruta, filtros en la query string |
| ☐ | Reglas en el dominio con pruebas unitarias (incluidas las que solo estaban en la interfaz) |
| ☐ | Caso de uso con puertos; DTOs proyectados para leer; agregado completo (`Include`) para escribir |
| ☐ | Mapeo sobre el esquema existente; sin migraciones sobre la BD compartida |
| ☐ | Controlador sin lógica; `ModelState` + `Resultado`; PRG con `TempData` |
| ☐ | Vistas con Tag Helpers; nada inline (JS, estilos); JavaScript sin Razor |
| ☐ | Autorización declarada y probada (403); antiforgery en todos los POST |
| ☐ | Logs con EventId en las operaciones relevantes |
| ☐ | Prueba de integración que recorre la pantalla |
| ☐ | Comparación lado a lado con la pantalla vieja, con datos reales |
| ☐ | URLs antiguas redirigidas (si la vieja se apaga) |

## 5.3 Hoja de ruta para SIREI y Noticom

Lo que hemos hecho hoy es una **prueba de concepto** de la estrategia. Así seguiría el proyecto real:

| Fase | SIREI (Web Forms) | Noticom (MVC 5) |
|---|---|---|
| **0. Preparar** | Pruebas de caracterización (capturar lo que hace hoy); inventario completo (Lab 3 día 3) | Inventario; dependencias NuGet sin versión moderna; EDMX |
| **1. Convivir** | Aplicación ASP.NET Core con **YARP** delante: todo va al legacy | Igual, o migración completa si se puede congelar (es MVC y pequeña) |
| **2. Compartir** | **System.Web adapters**: autenticación y sesión compartidas; BD compartida | Identity 2 → ASP.NET Core Identity con la misma tabla de usuarios (día 4, cap. 5) |
| **3. Migrar pantallas** | Por valor: búsqueda y ficha de expedientes (hecho hoy), después el resto | Lotes (hecho hoy), remesas, notificaciones, documentos |
| **4. Datos** | ADO.NET → EF Core detrás de los puertos; conversión de fechas | EF6 → EF Core (o mantener EF 6.3+ un tiempo en .NET 10) |
| **5. Apagar** | Cuando YARP ya no reenvía nada: redirecciones 301 de las `.aspx` y fuera | Igual |

**Riesgos a vigilar:** informes (Crystal/RDLC), controles de terceros, ficheros en carpetas compartidas, la sesión usada como almacén, y la tentación de **rediseñar** mientras se migra (duplica el riesgo: primero migrar, después mejorar).

## 5.4 Cómo seguir aprendiendo

| Tema | Por qué | Por dónde empezar |
|---|---|---|
| **SQL Server + migraciones reales** | Todo el curso usa InMemory | `Microsoft.EntityFrameworkCore.SqlServer`, `dotnet ef migrations add` (día 3, cap. 3) |
| **Pruebas contra la BD real** | InMemory no detecta errores de SQL | Testcontainers para .NET, SQL Server LocalDB |
| **Publicación** | IIS, contenedores, Azure App Service | Documentación "Hospedaje e implementación" |
| **Observabilidad** | Del log en consola a trazas y métricas | OpenTelemetry + .NET Aspire (panel de desarrollo) |
| **Migración incremental** | El patrón real para SIREI | YARP + System.Web adapters (ejemplos oficiales) |
| **Blazor** | La alternativa de componentes para equipos Web Forms | Libro gratuito *Blazor para desarrolladores de ASP.NET Web Forms* |
| **GitHub Copilot upgrade** | La herramienta que recomienda Microsoft para lo mecánico | Documentación de *GitHub Copilot app modernization* |

## 5.5 Autoevaluación

Diez preguntas para comprobar lo esencial. Respondedlas antes de abrir las soluciones.

1. En Web Forms, un `DropDownList` con `AutoPostBack` filtra un `GridView`. ¿Qué es en ASP.NET Core y dónde queda el valor elegido?
2. ¿Por qué `btnGuardar.Visible = false` no basta para impedir que se guarde un expediente cerrado? ¿Dónde va la regla?
3. Una acción nueva en `IncidenciasController`, sin ningún atributo: ¿exige haber iniciado sesión? ¿Por qué?
4. ¿Qué diferencia hay entre un 401 y un 403? ¿Qué hace la cookie de autenticación con cada uno en una petición normal y en una AJAX?
5. ¿Por qué el repositorio carga el expediente con `Include(e => e.Tramites)` y las consultas del listado no?
6. Dos usuarios editan el mismo expediente. ¿Qué pasaba en SIREI y qué pasa ahora? ¿Qué dos mecanismos intervienen?
7. ¿Por qué la vista de lotes ya no oculta columnas con JavaScript? ¿Qué problema concreto tenía `ocultarColumnaGridview(12, ...)`?
8. Una vista de un área muestra `asp-action="Index"` literal en el HTML. ¿Qué falta?
9. ¿Por qué no generamos migraciones de EF Core sobre la base de datos de SIREI?
10. Nombra tres pruebas que escribirías antes de dar por migrada la ficha de expedientes, una de cada nivel.

<details>
<summary>Soluciones</summary>

1. Un formulario **GET** con un `<select name="estado">` y un botón (o un `.js` que envíe el formulario al cambiar). El valor queda en la **query string** (`?estado=EnTramite`), no en el ViewState.
2. Porque cualquiera puede enviar el POST sin pasar por la pantalla. La regla va en el **dominio** (`Expediente.Actualizar` devuelve `Conflicto` si está cerrado); la vista solo oculta por comodidad.
3. Sí: la **`FallbackPolicy`** (`RequireAuthenticatedUser`) se aplica a todo endpoint sin `[Authorize]` ni `[AllowAnonymous]`. Seguro por defecto.
4. **401**: no sabemos quién eres; **403**: sabemos quién eres y no puedes. En una petición normal, la cookie los convierte en **302** a `/Cuenta/Login` y a `/Cuenta/AccesoDenegado`; con `X-Requested-With: XMLHttpRequest`, responde **401/403** sin redirigir.
5. Porque para **escribir** se necesita el agregado completo (la regla de cierre mira los trámites). Para **leer**, la consulta proyecta solo lo necesario, con el recuento de pendientes calculado en la BD.
6. En SIREI, el último que guardaba pisaba al primero sin aviso. Ahora el segundo recibe un **conflicto** y ve los datos actuales. Intervienen la comparación de **`Version`** (campo oculto del formulario) en el caso de uso y el **token de concurrencia** de EF Core en el `UPDATE`.
7. Porque cada pantalla pinta **solo sus columnas** a partir del ViewModel. Ocultar por **número** de columna se descoloca en cuanto se añade o reordena una columna (y `crearRemesa()` leía los Id de la columna 2).
8. El **`_ViewImports.cshtml` del área** (con `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`): el de la carpeta `Views` raíz no se aplica a las áreas.
9. Porque la base de datos es **compartida** con la aplicación Web Forms mientras conviven: su esquema no es nuestro. El mapeo se adapta a ella (`ToTable`, `HasColumnName`) y cualquier cambio se pacta y se aplica con un script.
10. Por ejemplo: **unitaria de dominio** `Actualizar_CerrarConTramitesPendientes_DaConflicto`; **unitaria de aplicación** `ActualizarAsync_ConUnaVersionAntigua_DaConflictoYNoGuarda`; **integración** `Sirei_CerrarUnExpedienteConTramitesPendientes_MuestraLaReglaDeNegocio` (o `Sirei_SinIniciarSesion_RedirigeAlLogin`). Todas están en `src/day-05/tests`.

</details>

## 5.6 Evaluación del curso

Gracias por la participación durante estos cinco días.

## Referencias generales

> Enlaces comprobados el 9 de octubre de 2026.

- [Documentación de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/?view=aspnetcore-10.0)
- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0)
- [Hospedaje e implementación de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/host-and-deploy/?view=aspnetcore-10.0)
- [Documentación de Entity Framework Core](https://learn.microsoft.com/es-es/ef/core/)
- [Introducción a .NET Aspire](https://learn.microsoft.com/es-es/dotnet/aspire/get-started/aspire-overview)
- [Información general sobre la actualización de GitHub Copilot](https://learn.microsoft.com/es-es/dotnet/core/porting/github-copilot-upgrade/overview)
- [Directiva de soporte técnico de .NET](https://dotnet.microsoft.com/es-es/platform/support/policy/dotnet-core) — .NET 10 LTS, soporte hasta el 14/11/2028.
