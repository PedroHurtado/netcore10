# Lab 4 — Configuración, opciones y entornos

**Duración:** 20 min · **Teoría relacionada:** [07 — Configuración y opciones](../07-configuracion-opciones.md)

## Objetivo

Sacar del código los valores de negocio, configurarlos por entorno y validarlos al arrancar.

## Paso 1 — Sección de configuración

En `appsettings.json`:

```json
"Incidencias": {
  "NombreAplicacion": "Gestor de Incidencias",
  "MaxIncidenciasAbiertas": 50,
  "PrioridadPorDefecto": "Media",
  "CargarDatosDemo": false
}
```

En `appsettings.Development.json`:

```json
"Incidencias": {
  "NombreAplicacion": "Gestor de Incidencias [DEV]",
  "MaxIncidenciasAbiertas": 5,
  "CargarDatosDemo": true
}
```

## Paso 2 — Clase de opciones con validación

Crea `Options/IncidenciasOptions.cs` con `[Required]` en `NombreAplicacion` y `[Range(1, 10_000)]` en `MaxIncidenciasAbiertas`, y regístrala:

```csharp
builder.Services
    .AddOptions<IncidenciasOptions>()
    .Bind(builder.Configuration.GetSection(IncidenciasOptions.Seccion))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

## Paso 3 — Usar las opciones en el negocio

1. En `IncidenciaService`, inyecta `IOptionsSnapshot<IncidenciasOptions>`:
   - Si hay `MaxIncidenciasAbiertas` o más incidencias abiertas, `CrearAsync` devuelve error → el endpoint responde `409 Conflict`.
   - Si la petición no trae prioridad, usa `PrioridadPorDefecto`.
2. En `DatosDemoInitializer`, carga los datos **solo** si `CargarDatosDemo` es `true`.
3. En `/info`, devuelve `NombreAplicacion`.

✅ **Comprueba (Development):** hay 3 incidencias de demo; crea 2 más; la sexta devuelve `409`.

## Paso 4 — Validación al arrancar

Pon `"MaxIncidenciasAbiertas": 0` en `appsettings.Development.json` y arranca.

✅ **Comprueba:** la aplicación **no arranca** y el mensaje indica qué propiedad es inválida. Restaura el valor.

## Paso 5 — Entornos

```bash
dotnet run --environment Production
```

✅ **Comprueba:**
- `/info` muestra `Gestor de Incidencias` y `Production`.
- No hay datos de demo.
- `/demo/...` y `/openapi/v1.json` devuelven `404` (solo se mapean en Development).

## Paso 6 — Precedencia de fuentes

Con la aplicación en Development, sobrescribe el máximo con una variable de entorno:

```powershell
# PowerShell
$env:Incidencias__MaxIncidenciasAbiertas = "20"
dotnet run
Remove-Item Env:Incidencias__MaxIncidenciasAbiertas
```

y luego con un argumento:

```bash
dotnet run -- --Incidencias:MaxIncidenciasAbiertas=30
```

✅ **Comprueba** con `GET /demo/configuracion` qué valor gana en cada caso y el listado de fuentes.

## Paso 7 — Recarga en caliente

1. Arranca en Development y llama a `GET /demo/opciones`.
2. Sin parar la aplicación, cambia `MaxIncidenciasAbiertas` a `8` en `appsettings.Development.json` y guarda.
3. Vuelve a llamar.

✅ **Comprueba:** `IOptionsSnapshot` e `IOptionsMonitor` muestran 8; `IOptions` sigue en 5. ¿Por qué?

## Reto (opcional)

1. Añade la fuente opcional `appsettings.Local.json` (ignorada por git) y úsala para cambiar `NombreAplicacion` en tu máquina.
2. Guarda un valor ficticio "secreto" con User Secrets y léelo desde `/demo/configuracion`:

   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "Integraciones:ApiKey" "clave-super-secreta"
   ```

   ¿En qué carpeta de tu equipo se ha guardado? ¿Aparece en el proyecto?

## Para reflexionar

- ¿Qué valores de los `web.config` de vuestras aplicaciones actuales deberían ser opciones tipadas y cuáles secretos?

## Referencias

> Enlaces comprobados el 4 de octubre de 2026.

- [Configuración en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0) — Prioridad de fuentes, variables de entorno con `__` y línea de comandos.
- [Patrón de opciones en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0) — `ValidateDataAnnotations`, `ValidateOnStart` y recarga en caliente.
- [Entornos de tiempo de ejecución](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/environments?view=aspnetcore-10.0)
- [Almacenamiento seguro de secretos en desarrollo (User Secrets)](https://learn.microsoft.com/es-es/aspnet/core/security/app-secrets?view=aspnetcore-10.0) — Respuesta a la pregunta del reto.
