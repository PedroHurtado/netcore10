# 2. Diferencias clave entre ASP.NET Web Forms y ASP.NET Core

## 2.1 Dos filosofías distintas

**Web Forms (2002)** intentó que programar para la web se pareciera a programar escritorio (Windows Forms): controles con estado, eventos de servidor (`Button_Click`) y un diseñador visual. El HTTP quedaba "escondido".

**ASP.NET Core** hace lo contrario: **abraza HTTP**. Trabajas con peticiones, respuestas, rutas, verbos y códigos de estado, con control total del HTML y JavaScript generado.

## 2.2 Tabla comparativa

| Aspecto | ASP.NET Web Forms | ASP.NET Core |
|---|---|---|
| Plataforma | .NET Framework, solo Windows | .NET 10, multiplataforma |
| Servidor | IIS (integrado con `System.Web`) | Kestrel (+ IIS, Nginx, Apache o YARP como proxy inverso) |
| Punto de entrada | `Global.asax`, `web.config` | `Program.cs` |
| Modelo de programación | Páginas `.aspx` + *code-behind* con eventos | MVC, Razor Pages, Minimal APIs, Blazor |
| Ciclo de vida | Ciclo de vida de la página (`Page_Init`, `Page_Load`, eventos, `Page_PreRender`...) | Pipeline de **middleware** + endpoints |
| Estado | **ViewState** (campo oculto), Session, Application | Sin ViewState. Modelo sin estado; Session opcional, TempData, caché, cookies |
| Envío de formularios | **PostBack** a la misma página | Formularios HTML a acciones/handlers concretos (patrón PRG) |
| HTML generado | Controles de servidor (`asp:GridView`) que generan HTML difícil de controlar | HTML explícito + **Tag Helpers** |
| URLs | `/Clientes/Editar.aspx?id=5` (fichero físico) | `/clientes/5/editar` (enrutamiento) |
| Configuración | `web.config` (XML) | `appsettings.json`, variables de entorno, secretos, Key Vault... |
| Dependencias | Instanciación manual (`new`), *singletons* estáticos | **Inyección de dependencias** integrada |
| Logging | Log4net / NLog / Enterprise Library manuales | `ILogger<T>` integrado |
| Autenticación | Forms Authentication, Membership | ASP.NET Core Identity, cookies, JWT, OpenID Connect |
| Pruebas | Muy difíciles (code-behind acoplado a `HttpContext`) | Diseñado para ser testeable (DI, `WebApplicationFactory`) |
| Despliegue | Copia a IIS | Ejecutable autocontenido, contenedor Docker, nube |

## 2.3 El mismo caso, dos mundos

Requisito: un formulario con un campo "Título" y un botón que da de alta una incidencia.

### Web Forms

```aspx
<%-- AltaIncidencia.aspx --%>
<asp:TextBox ID="txtTitulo" runat="server" />
<asp:RequiredFieldValidator ControlToValidate="txtTitulo" runat="server" ErrorMessage="Obligatorio" />
<asp:Button ID="btnGuardar" runat="server" Text="Guardar" OnClick="btnGuardar_Click" />
<asp:Label ID="lblMensaje" runat="server" />
```

```csharp
// AltaIncidencia.aspx.cs (code-behind)
protected void btnGuardar_Click(object sender, EventArgs e)
{
    if (!Page.IsValid) return;

    // Lógica de negocio y acceso a datos mezclados con la interfaz
    using (var cn = new SqlConnection(ConfigurationManager.ConnectionStrings["Bd"].ConnectionString))
    using (var cmd = new SqlCommand("INSERT INTO Incidencias (Titulo) VALUES (@t)", cn))
    {
        cmd.Parameters.AddWithValue("@t", txtTitulo.Text);
        cn.Open();
        cmd.ExecuteNonQuery();
    }
    lblMensaje.Text = "Incidencia creada";
}
```

Problemas típicos: lógica de negocio dentro del evento del botón, `new` de todo, configuración estática, imposible de probar sin un servidor web.

### ASP.NET Core (lo que construimos hoy)

```csharp
// Endpoint: solo traduce HTTP ↔ llamada al servicio
grupo.MapPost("/", async (CrearIncidenciaRequest request, IIncidenciaService servicio, CancellationToken ct) =>
{
    var resultado = await servicio.CrearAsync(request, ct);
    return resultado.Exito
        ? Results.CreatedAtRoute("ObtenerIncidencia", new { id = resultado.Valor!.Id }, ...)
        : Results.Problem(resultado.Error, statusCode: 409);
});

// Servicio: lógica de negocio, sin saber nada de HTTP; dependencias inyectadas
public class IncidenciaService(IIncidenciaRepository repositorio, TimeProvider reloj,
    IOptionsSnapshot<IncidenciasOptions> opciones, ILogger<IncidenciaService> logger) : IIncidenciaService
```

Cada pieza tiene una responsabilidad, se puede sustituir (el repositorio) y se puede probar de forma aislada.

## 2.4 Correspondencia de conceptos (avance del Módulo 3)

| Web Forms | Equivalente en ASP.NET Core |
|---|---|
| `Global.asax` → `Application_Start` | `Program.cs` (registro de servicios) / `IHostedService` |
| `Global.asax` → `Application_BeginRequest` | Middleware |
| `HttpModule` | Middleware |
| `HttpHandler` (`.ashx`) | Endpoint (`app.MapGet(...)`) o controlador API |
| `web.config` → `<appSettings>` | `appsettings.json` + `IOptions<T>` |
| `web.config` → `<connectionStrings>` | `ConnectionStrings` en `appsettings.json` / secretos |
| Master Page | Layout (`_Layout.cshtml`) |
| User Control (`.ascx`) | Partial View, View Component, Tag Helper, componente Razor |
| `Page_Load` | Acción de controlador (`GET`) / `OnGet` en Razor Pages |
| `Button_Click` | Acción `POST` / `OnPost` en Razor Pages |
| ViewState | Campos del modelo en el formulario, TempData, recarga desde BD |
| `Session["x"]` | `ISession` (opcional, distribuida), caché, claims |
| `HttpContext.Current` (estático) | `HttpContext` inyectado / `IHttpContextAccessor` (evitar en lógica de negocio) |
| `Response.Redirect` | `RedirectToAction`, `Results.Redirect` |
| `GridView` + `SqlDataSource` | Tabla HTML en Razor + servicio + EF Core |

## 2.5 Lo que NO se puede llevar tal cual

- Ficheros `.aspx`, `.ascx`, `.master` y controles `asp:*` → **se reescriben**.
- `System.Web` (`HttpContext.Current`, `HttpRuntime.Cache`...) → no existe; hay adaptadores (`Microsoft.AspNetCore.SystemWebAdapters`) para migraciones incrementales.
- WCF servidor → CoreWCF (proyecto comunitario), gRPC o API REST.
- Configuración de `web.config` (`<system.web>`) → se traslada a código y JSON.

**Lo que sí suele reaprovecharse:** entidades, reglas de negocio, validaciones y acceso a datos, especialmente si ya estaban en bibliotecas de clases separadas de la interfaz.

## Preguntas de repaso

1. ¿Qué problema resolvía el ViewState y por qué ASP.NET Core no lo necesita?
2. ¿Dónde pondrías en ASP.NET Core el código que en Web Forms estaba en `Application_BeginRequest`?
3. En el ejemplo de Web Forms, ¿qué partes del código serían reaprovechables en una migración?
