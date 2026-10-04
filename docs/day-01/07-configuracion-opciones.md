# 7. Configuración y opciones (appsettings.json, entornos)

## 7.1 De `web.config` a un sistema de proveedores

En .NET Framework:

```xml
<appSettings>
  <add key="MaxIncidenciasAbiertas" value="50" />
</appSettings>
```
```csharp
int max = int.Parse(ConfigurationManager.AppSettings["MaxIncidenciasAbiertas"]);
```

En ASP.NET Core la configuración es un **diccionario jerárquico clave/valor** alimentado por varias **fuentes** (proveedores). **La última fuente cargada gana**:

```
1. appsettings.json
2. appsettings.{Entorno}.json      (p. ej. appsettings.Development.json)
3. User Secrets                     (solo en Development)
4. Variables de entorno
5. Argumentos de línea de comandos
   (+ las que añadamos: Azure Key Vault, App Configuration, appsettings.Local.json...)
```

En el proyecto, `GET /demo/configuracion` muestra la lista real de fuentes.

## 7.2 `appsettings.json`

```json
{
  "MensajeBienvenida": "Bienvenido al Gestor de Incidencias (configuración base)",
  "Incidencias": {
    "NombreAplicacion": "Gestor de Incidencias",
    "MaxIncidenciasAbiertas": 50,
    "PrioridadPorDefecto": "Media",
    "CargarDatosDemo": false
  }
}
```

Las secciones anidadas se expresan con `:` como separador de claves:

```csharp
string? nombre = config["Incidencias:NombreAplicacion"];
int max        = config.GetValue<int>("Incidencias:MaxIncidenciasAbiertas");
```

En **variables de entorno** el separador es `__` (doble guion bajo), porque `:` no es válido en todos los sistemas:

```powershell
$env:Incidencias__MaxIncidenciasAbiertas = "10"
dotnet run
```

Por línea de comandos:

```bash
dotnet run -- --Incidencias:MaxIncidenciasAbiertas=10
```

## 7.3 Entornos

La variable `ASPNETCORE_ENVIRONMENT` (o `DOTNET_ENVIRONMENT`) determina el entorno. Valores convencionales: `Development`, `Staging`, `Production`. **Si no está definida, el entorno es `Production`** (por seguridad).

```csharp
if (app.Environment.IsDevelopment()) { app.MapOpenApi(); app.MapDemos(); }
if (app.Environment.IsEnvironment("Staging")) { ... }
```

En el proyecto:

| Clave | `appsettings.json` | `Development` | `Production` |
|---|---|---|---|
| `NombreAplicacion` | Gestor de Incidencias | Gestor de Incidencias **[DEV]** | (hereda) |
| `MaxIncidenciasAbiertas` | 50 | **5** | **500** |
| `CargarDatosDemo` | false | **true** | (hereda) |
| Endpoints `/demo/*` y OpenAPI | — | Sí | No |

## 7.4 Patrón Options

Leer cadenas sueltas de `IConfiguration` por todo el código es frágil. El **patrón Options** enlaza una sección con una clase tipada:

```csharp
public class IncidenciasOptions
{
    public const string Seccion = "Incidencias";

    [Required] public string NombreAplicacion { get; set; } = "Gestor de Incidencias";
    [Range(1, 10_000)] public int MaxIncidenciasAbiertas { get; set; } = 100;
    public Prioridad PrioridadPorDefecto { get; set; } = Prioridad.Media;
    public bool CargarDatosDemo { get; set; }
}
```

```csharp
builder.Services
    .AddOptions<IncidenciasOptions>()
    .Bind(builder.Configuration.GetSection(IncidenciasOptions.Seccion))
    .ValidateDataAnnotations()   // aplica [Required], [Range]...
    .ValidateOnStart();          // si la configuración es inválida, la aplicación NO arranca
```

> **`ValidateOnStart`** convierte un error de configuración en producción (que aparecería horas después, en la primera petición que lo use) en un fallo inmediato al desplegar.

### Tres formas de consumirlas

| Interfaz | Lifetime | ¿Ve cambios en caliente? | Uso |
|---|---|---|---|
| `IOptions<T>` | Singleton | **No** (valor leído una vez) | Configuración que no cambia |
| `IOptionsSnapshot<T>` | Scoped | **Sí**, en la siguiente petición | Servicios scoped (nuestro `IncidenciaService`) |
| `IOptionsMonitor<T>` | Singleton | **Sí**, al instante (+ evento `OnChange`) | Servicios singleton, middleware |

Prueba en el proyecto: arranca la aplicación, llama a `GET /demo/opciones`, cambia `MaxIncidenciasAbiertas` en `appsettings.Development.json`, guarda y vuelve a llamar.

## 7.5 Secretos

**Nunca** se guardan contraseñas ni claves en `appsettings.json` (acaba en el control de versiones).

- En desarrollo: **User Secrets** (fichero fuera del proyecto, en el perfil del usuario).

  ```bash
  dotnet user-secrets init
  dotnet user-secrets set "ConnectionStrings:Bd" "Server=...;Password=..."
  ```

- En servidores: variables de entorno, Azure Key Vault, AWS Secrets Manager, etc.

## 7.6 Fuente adicional: `appsettings.Local.json`

En el proyecto añadimos una fuente opcional, ignorada por git, para que cada desarrollador pueda sobrescribir valores sin tocar ficheros compartidos:

```csharp
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
```

## Preguntas de repaso

1. Si `MaxIncidenciasAbiertas` vale 50 en `appsettings.json`, 5 en `appsettings.Development.json` y hay una variable de entorno `Incidencias__MaxIncidenciasAbiertas=20`, ¿qué valor ve la aplicación en Development?
2. ¿Qué ocurre si se publica en un servidor sin definir `ASPNETCORE_ENVIRONMENT`?
3. ¿Qué interfaz de opciones usarías dentro de un middleware por convención si quieres ver cambios sin reiniciar?
