# 2. Caso práctico SIREI: de Web Forms a MVC

**Legacy:** [legacy/sirei](legacy/sirei/) · **Solución:** [src/day-05 → Areas/Sirei](../../src/day-05/GestorIncidencias.Web/Areas/Sirei/) · **Laboratorio:** [Lab 1](labs/lab-01-sirei-expedientes.md)

SIREI es una aplicación Web Forms de gestión de expedientes. Migramos sus dos pantallas centrales: la **búsqueda** (`BuscarExpedientes.aspx`) y la **ficha** (`ExpedienteDetalle.aspx`, la del análisis del día 3). Según la matriz del día 3, son de **valor alto** y **coste medio**: *refactorizar*.

## 2.1 Lo que encontramos al leer el legacy (paso 1)

| Pieza | Dónde | 🚩 | Decisión |
|---|---|---|---|
| `Session["Usuario"] == null → Redirect` en cada `Page_Load` | Las dos páginas | Seguridad copiada a mano | `FallbackPolicy` (ya existe): nada que escribir |
| SQL concatenado con `txtBuscar.Text` | `CargarExpedientes` | **Inyección SQL** | LINQ → `LIKE` parametrizado |
| `SELECT *` + `GridView AllowPaging` | Búsqueda | Todas las filas en cada clic | Proyección a `ExpedienteDto` + `Skip/Take` |
| `Session["BusquedaExpedientes"]` | Búsqueda | Estado en sesión | **Se elimina**: la búsqueda va en la URL |
| `RowDataBound` con `case 4: "Cerrado"` | Búsqueda | Número mágico en la interfaz | `enum EstadoExpediente` + vista parcial `_EstadoExpediente` |
| `ViewState["IdExpediente"]` | Ficha | Estado oculto | El id en la **ruta** |
| `Session["ExpedienteActual"] = ds` | Ficha | `DataSet` entero en memoria por usuario | Se vuelve a cargar en cada petición |
| "No cerrar con trámites pendientes" en `btnGuardar_Click` | Ficha | **Regla de negocio en un evento** | `Expediente.Actualizar(...)` |
| `btnGuardar.Visible = !cerrado` | Ficha | **Regla solo en la interfaz** | `Expediente`: "un expediente cerrado no admite cambios" |
| `SqlCommandBuilder` + `da.Update` | Ficha | El último que guarda gana | **Concurrencia optimista** con `Version` |
| `UPDATE Tramites ... WHERE Id = ` sin comprobar el expediente | `RowCommand` | Se puede completar un trámite de **otro** expediente | `Expediente.CompletarTramite(tramiteId)` busca solo en **sus** trámites |
| `INSERT ... '" + txtNuevoTramite.Text + "'` | Añadir trámite | Inyección SQL; falla con `D'Ors` | `Expediente.AgregarTramite(...)` |
| `DateTime.Now` | Guardar | Hora local, imposible de probar | `TimeProvider` (UTC) |
| `OnClientClick="return confirm(...)"` | Ficha | JS inline (CSP) | Se elimina (o un `.js` con `data-confirmar`, como en Noticom) |
| `customErrors mode="Off"`, `debug="true"` | `Web.config` | Trazas a los usuarios | `UseExceptionHandler` fuera de Development (día 4) |
| Contraseña en `connectionStrings` | `Web.config` | Secreto en claro | Secretos de usuario / variables de entorno / Key Vault |

## 2.2 Recorrido por la solución

```
src/day-05/
├── GestorIncidencias.Domain/Expedientes/
│   ├── Expediente.cs           ← Actualizar, AgregarTramite, CompletarTramite (las reglas)
│   ├── Tramite.cs
│   └── EstadoExpediente.cs     ← Abierto = 1 ... Cerrado = 4 (los valores de la columna IdEstado)
├── GestorIncidencias.Application/
│   ├── Abstracciones/IExpedienteRepository.cs, IExpedienteConsultas.cs
│   └── Expedientes/ExpedienteService.cs, ExpedienteDto.cs, ExpedienteLog.cs (EventId 2001-2004)
├── GestorIncidencias.Infrastructure/Sirei/
│   ├── SireiDbContext.cs       ← OTRA base de datos: la de SIREI
│   ├── Configuraciones/        ← ToTable, HasColumnName("IdEstado"), FK "IdExpediente", IsConcurrencyToken
│   ├── EfExpedienteRepository.cs, EfExpedienteConsultas.cs
│   └── InicializadorSirei.cs   ← 45 expedientes de demostración
└── GestorIncidencias.Web/Areas/Sirei/
    ├── Controllers/ExpedientesController.cs   ← Index, Detalle (GET/POST), trámites, URLs .aspx
    ├── Models/ExpedientesViewModels.cs
    └── Views/                                  ← _ViewImports y _ViewStart PROPIOS del área
```

### El evento convertido en método del dominio

```csharp
public Resultado Actualizar(string? observaciones, EstadoExpediente nuevoEstado, DateTimeOffset ahora)
{
    if (Estado == EstadoExpediente.Cerrado)
        return Resultado.Fallo("Un expediente cerrado no admite cambios.", TipoError.Conflicto);
    ...
    // La regla de SIREI: if (ddlEstado.SelectedValue == "4" && ds.Tables["Tramites"].Select("Pendiente = 1").Length > 0)
    if (nuevoEstado == EstadoExpediente.Cerrado && TieneTramitesPendientes)
        return Resultado.Fallo("No se puede cerrar un expediente con trámites pendientes.", TipoError.Conflicto);

    Observaciones = observaciones;
    Estado = nuevoEstado;
    MarcarModificado(ahora);   // FechaModificacion = ahora; Version++
    return Resultado.Ok();
}
```

`TieneTramitesPendientes` necesita los trámites: por eso el repositorio carga el agregado con `Include(e => e.Tramites)`. Sin el `Include`, la regla dejaría pasar el cierre **sin ningún error** (es una de las averías del [Lab 3](labs/lab-03-resolucion-problemas.md)).

### Concurrencia optimista: adiós a "el último que guarda gana"

En SIREI, si Ana y Luis abren el mismo expediente y guardan los dos, el segundo pisa al primero sin aviso. Ahora:

```
 Ana abre la ficha (Version = 3)          Luis abre la ficha (Version = 3)
                                           Luis guarda → Version = 4 ✅
 Ana guarda con Version = 3
   → ExpedienteService: 3 ≠ 4 → Conflicto
   → "Otra persona ha modificado el expediente mientras lo editabas..."
   → se recarga la ficha con los datos de Luis
```

Dos defensas que se complementan:

| Defensa | Dónde | Cubre |
|---|---|---|
| Comparar la `Version` del formulario (campo oculto) con la de la BD | `ExpedienteService.ActualizarAsync` | Los **minutos** que el usuario tiene la ficha abierta |
| `IsConcurrencyToken()` → `UPDATE ... WHERE Version = @original` | `ExpedienteConfiguracion` | Los **milisegundos** entre leer y guardar en la misma petición (lanza `DbUpdateConcurrencyException`) |

### El controlador: un evento, una acción

```csharp
// POST /Sirei/Expedientes/Detalle/5   ← antes: btnGuardar_Click
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Detalle(
    int id, [Bind(Prefix = nameof(FichaExpedienteViewModel.Formulario))] ExpedienteFormulario formulario, CancellationToken ct)
```

`[Bind(Prefix = "Formulario")]`: la vista recibe un ViewModel con dos partes (los datos actuales del expediente y el formulario que se edita) y los campos se llaman `Formulario.Estado`, `Formulario.Observaciones`... El prefijo hace que el *model binding* los encuentre.

Si el guardado falla por una regla, se vuelve a pintar la ficha con **los datos actuales de la BD** y **lo que escribió el usuario** (lo que hacía el ViewState). Si falla por concurrencia, se redirige para cargar la versión nueva.

## 2.3 Qué hemos dejado fuera (a propósito)

- **Alta de expedientes**: el número lo asigna el registro de entrada (otra aplicación). Seguiría en el legacy de momento.
- **Documentos** (`RutaDocumentos` en `Web.config`, una carpeta compartida): necesita su propio análisis (permisos de la cuenta del *pool*, `IFileProvider` o almacenamiento en la nube).
- **Conversión de fechas** hora local ↔ UTC: decidida, pero no implementada con InMemory (ver el comentario en `ExpedienteConfiguracion`).

## Referencias

> Enlaces comprobados el 9 de octubre de 2026.

- [Migración de ASP.NET Framework a ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/migration/fx-to-core/?view=aspnetcore-10.0)
- [Blazor para desarrolladores de ASP.NET Web Forms](https://learn.microsoft.com/es-es/dotnet/architecture/blazor-for-web-forms-developers/) — La alternativa de componentes.
- [Control de conflictos de simultaneidad (EF Core)](https://learn.microsoft.com/es-es/ef/core/saving/concurrency)
- [Enlace de modelos: prefijos](https://learn.microsoft.com/es-es/aspnet/core/mvc/models/model-binding?view=aspnetcore-10.0)
- [Áreas en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/mvc/controllers/areas?view=aspnetcore-10.0)
