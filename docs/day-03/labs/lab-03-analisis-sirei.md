# Lab 3 — Primer análisis de SIREI

**Duración:** 22 min (en parejas o grupos de 3) · **Teoría relacionada:** [05 — De DataSet a EF Core](../05-dataset-a-ef-core.md), [07 — Análisis de aplicaciones legacy](../07-analisis-legacy.md)

## Objetivo

Empezar el análisis de **SIREI** (Web Forms) con una ficha común para todo el grupo. No se trata de terminarlo hoy, sino de:

1. Aprender a **mirar** una aplicación legacy con los ojos de quien la va a migrar.
2. Detectar pronto las **señales de alarma**.
3. Tener una **primera propuesta** de qué migrar, en qué orden y con qué estrategia, que retomaremos en el caso práctico del día 5.

El formador os indicará qué parte del código y de las pantallas de SIREI analiza cada grupo. Si todavía no tenéis acceso al código, empezad con el **fragmento de ejemplo** del paso 1: es representativo de lo que suele aparecer.

No hay que escribir código. Trabajad sobre una copia de esta ficha (un `.md`, un documento o papel).

## Paso 1 — Calentamiento: analizar una página (6 min)

Este *code-behind* es típico de una aplicación Web Forms de gestión. Leedlo y anotad **todo** lo que habría que tener en cuenta para migrarlo (usad la lista del [capítulo 7.1](../07-analisis-legacy.md#71-inventario-qué-hay-que-mirar)).

```csharp
// ExpedienteDetalle.aspx.cs
public partial class ExpedienteDetalle : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        if (Session["Usuario"] == null) Response.Redirect("~/Login.aspx");

        if (!IsPostBack)
        {
            var id = int.Parse(Request.QueryString["id"]);
            ViewState["IdExpediente"] = id;

            var ds = new DataSet();
            using (var cn = new SqlConnection(ConfigurationManager.ConnectionStrings["SIREI"].ConnectionString))
            {
                var da = new SqlDataAdapter("SELECT * FROM Expedientes WHERE Id = " + id, cn);
                da.Fill(ds, "Expediente");
                da.SelectCommand.CommandText = "SELECT * FROM Tramites WHERE IdExpediente = " + id;
                da.Fill(ds, "Tramites");
            }
            Session["ExpedienteActual"] = ds;

            var fila = ds.Tables["Expediente"].Rows[0];
            lblNumero.Text = fila["Numero"].ToString();
            txtObservaciones.Text = fila["Observaciones"].ToString();
            ddlEstado.SelectedValue = fila["IdEstado"].ToString();

            gvTramites.DataSource = ds.Tables["Tramites"];
            gvTramites.DataBind();
        }
    }

    protected void btnGuardar_Click(object sender, EventArgs e)
    {
        var ds = (DataSet)Session["ExpedienteActual"];
        var fila = ds.Tables["Expediente"].Rows[0];

        if (ddlEstado.SelectedValue == "4" && ds.Tables["Tramites"].Select("Pendiente = 1").Length > 0)
        {
            lblError.Text = "No se puede cerrar un expediente con trámites pendientes.";
            return;
        }

        fila["Observaciones"] = txtObservaciones.Text;
        fila["IdEstado"] = int.Parse(ddlEstado.SelectedValue);
        fila["FechaModificacion"] = DateTime.Now;

        using (var cn = new SqlConnection(ConfigurationManager.ConnectionStrings["SIREI"].ConnectionString))
        {
            var da = new SqlDataAdapter("SELECT * FROM Expedientes WHERE Id = " + ViewState["IdExpediente"], cn);
            new SqlCommandBuilder(da);
            da.Update(ds, "Expediente");
        }
        lblMensaje.Text = "Guardado";
    }
}
```

<details>
<summary>Lo que deberíais haber encontrado</summary>

| Hallazgo | Categoría | Qué haríamos en ASP.NET Core |
|---|---|---|
| `Session["Usuario"]` + `Response.Redirect` para proteger la página | Seguridad | `[Authorize]` y autenticación real (día 4) |
| `"... WHERE Id = " + id` (concatenación) | **Seguridad: inyección SQL** 🚩 | Consultas parametrizadas / EF Core |
| `SELECT *` | Rendimiento | Proyección con las columnas necesarias |
| `ViewState["IdExpediente"]` | Estado | El `id` viaja en la ruta (`/Expedientes/Detalle/5`) |
| `Session["ExpedienteActual"] = ds` (un `DataSet` entero en sesión) | Estado 🚩 | No se guarda en sesión: se vuelve a cargar en el POST |
| Regla "no cerrar con trámites pendientes" en `btnGuardar_Click` | **Lógica de negocio en la interfaz** 🚩 | Método `Expediente.Cerrar()` que devuelve `Resultado` (dominio) |
| Estado `"4"` como número mágico | Mantenibilidad | Enum `EstadoExpediente.Cerrado` |
| `DateTime.Now` | Pruebas / zonas horarias | `TimeProvider` inyectado, UTC (como `reloj.GetUtcNow()`) |
| `SqlCommandBuilder` + `da.Update` | Acceso a datos | Repositorio + `SaveChanges` |
| Último que guarda gana (sin control de concurrencia) | Datos | Token de concurrencia (`rowversion`) |
| `GridView` con `DataBind` | Interfaz | Tabla Razor con `@foreach` |
| `ConfigurationManager` | Configuración | Cadena de conexión en `appsettings` + DI |
| Sin `try/catch` ni log | Operación | Middleware de errores + `ILogger` (día 4) |
| Sin pruebas posibles (todo depende de `Page`, `Session`, SQL Server) | Pruebas | La regla en el dominio se prueba sin nada más |

**Clasificación razonable:** *Refactorizar*. Es una pantalla central (valor alto) con una regla de negocio escondida (riesgo medio): la regla se lleva al dominio y el resto se reescribe como un controlador + vista + caso de uso.

</details>

## Paso 2 — Inventario de SIREI (6 min)

Para la parte de SIREI que os ha tocado, rellenad una fila por pantalla (o por módulo, si son muchas):

| Pantalla / módulo | ¿Quién la usa y cuánto? | ¿Dónde está la lógica? | ¿Cómo accede a datos? | Estado (Session / ViewState) | Controles especiales | Señales de alarma 🚩 |
|---|---|---|---|---|---|---|
| | | | | | | |
| | | | | | | |
| | | | | | | |

Pistas para buscar en el código (Visual Studio: `Ctrl+Shift+F`, o `grep -r` en Git Bash):

| Buscar | Para encontrar |
|---|---|
| `SqlConnection`, `SqlDataAdapter`, `DataSet`, `.xsd`, `SqlDataSource` | Acceso a datos |
| `Session[`, `ViewState[`, `Application[`, `Cache[` | Estado |
| `HttpContext.Current` | Dependencias de `System.Web` fuera de las páginas |
| `<asp:UpdatePanel`, `<ajaxToolkit:`, `<telerik:`, `CrystalReportViewer`, `ReportViewer` | Controles y componentes problemáticos |
| `CommandType.StoredProcedure`, `EXEC ` | Procedimientos almacenados |
| `<authentication`, `<authorization`, `Membership`, `Roles.` | Seguridad |
| `.asmx`, `.svc`, `ServiceReference` | Servicios web (ASMX / WCF) |
| `Global.asax`, `IHttpModule`, `IHttpHandler`, `.ashx` | Infraestructura de `System.Web` |

## Paso 3 — Clasificar (5 min)

Colocad cada pantalla o módulo del inventario en la matriz del [capítulo 7.2](../07-analisis-legacy.md#72-decidir-qué-hacer-con-cada-pieza):

| Pantalla / módulo | Valor (alto/bajo) | Coste-riesgo (alto/bajo) | Decisión: retirar · migrar tal cual · refactorizar · reescribir · mantener | Justificación (una línea) |
|---|---|---|---|---|
| | | | | |
| | | | | |

## Paso 4 — Primera propuesta de estrategia (5 min)

Responded en grupo, en pocas líneas:

1. **¿Migración completa o incremental?** ¿Por qué? (Usad la tabla "¿Cuál elegir?" del [capítulo 7.3](../07-analisis-legacy.md#cuál-elegir).)
2. **¿Qué tendrían que compartir** SIREI y la aplicación nueva mientras convivan (BD, autenticación, sesión, lógica)?
3. **¿Por dónde empezaríais?** Proponed las **2 o 3 primeras** pantallas a migrar y el motivo (valor, sencillez, independencia del resto).
4. **Acceso a datos**: ¿reescribir con EF Core desde el principio, o llevar el ADO.NET tal cual detrás de una interfaz y reescribirlo después? ([capítulo 5.5](../05-dataset-a-ef-core.md#55-estrategia-para-migrar-el-acceso-a-datos-de-una-aplicación-legacy))
5. **Riesgos**: las 3 señales de alarma que más os preocupan y qué haríais con cada una.

## Puesta en común (al final del bloque o al inicio del día 4)

Cada grupo cuenta en 2 minutos: su estrategia, sus 3 primeras pantallas y su mayor riesgo. El formador consolida las fichas en un único análisis de SIREI, que será la base del **caso práctico del día 5**.

## Referencias

> Enlaces comprobados el 7 de octubre de 2026.

- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0)
- [Tecnologías de .NET Framework no disponibles en .NET 6+](https://learn.microsoft.com/es-es/dotnet/core/porting/net-framework-tech-unavailable)
- [Consultas SQL](https://learn.microsoft.com/es-es/ef/core/querying/sql-queries) — Sección sobre inyección SQL.
