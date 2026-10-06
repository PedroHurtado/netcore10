# 7. Resumen del día 2 y avance del día 3

## Lo que hemos aprendido

| Concepto | En una frase | Dónde está en el proyecto |
|---|---|---|
| Por qué capas | Las carpetas no son fronteras; el negocio no debe depender de la tecnología | — |
| Regla de dependencia | Las referencias solo apuntan hacia el dominio | Los 4 `.csproj` |
| Entidad rica | Setters privados y métodos que aplican las reglas | `Domain/Incidencias/Incidencia.cs` |
| `Resultado` | Errores de negocio sin excepciones, con su tipo | `Domain/Comun/Resultado.cs` |
| Caso de uso | Cargar → pedir a la entidad → guardar → devolver DTO | `Application/Incidencias/IncidenciaService.cs` |
| Puerto / adaptador | Interfaz en Application, implementación en Infrastructure | `IIncidenciaRepository` / `EfIncidenciaRepository` |
| Raíz de composición | Solo `Program.cs` conoce todas las piezas | `Web/Program.cs` |
| Hexagonal · Clean · Vertical Slice | Aislar el núcleo · capas hacia dentro · organizar por funcionalidad | Capítulo 3 |
| Controlador MVC | Traduce HTTP ↔ caso de uso; elige vista o redirección | `Controllers/IncidenciasController.cs` |
| PRG + TempData | Tras un POST correcto, redirigir con un mensaje | `IncidenciasController`, `_Mensajes.cshtml` |
| Vistas Razor | HTML + C#, codificado por defecto; layout, parciales | `Views/` |
| Tag Helpers | `asp-*`: enlaces, formularios y validación que conocen rutas y modelo | `Views/Incidencias/Crear.cshtml` |
| Razor Pages | Una página = un fichero + su `PageModel` | `Pages/Paginas/Incidencias/` |
| Controlador API | `[ApiController]`: validación automática y ProblemDetails | `Controllers/Api/` |

## Chuleta: una pantalla con formulario en MVC

```csharp
public class CosasController(ICosaService servicio) : Controller
{
    [HttpGet]
    public IActionResult Crear() => View(new CosaFormulario());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CosaFormulario formulario, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(formulario);

        var resultado = await servicio.CrearAsync(formulario.AComando(), ct);
        if (!resultado.Exito)
        {
            ModelState.AddModelError(string.Empty, resultado.Error!);
            return View(formulario);
        }

        TempData["Mensaje"] = "Creado.";
        return RedirectToAction(nameof(Detalle), new { id = resultado.Valor!.Id });
    }
}
```

```cshtml
@model CosaFormulario
<form asp-action="Crear" method="post">
    <div asp-validation-summary="ModelOnly"></div>
    <label asp-for="Nombre"></label>
    <input asp-for="Nombre" />
    <span asp-validation-for="Nombre"></span>
    <button type="submit">Guardar</button>
</form>
```

## Chuleta: añadir una funcionalidad en Clean Architecture

1. **Domain**: método en la entidad que comprueba la regla y devuelve `Resultado`.
2. **Application**: método en la interfaz del servicio + implementación (cargar → entidad → guardar).
3. **Web**: acción en el controlador (`[HttpPost, ValidateAntiForgeryToken]`).
4. **Web**: botón/formulario en la vista.

(Es exactamente el [Lab 2](labs/lab-02-reabrir-incidencia.md).)

## Avance del día 3

Partiremos de `src/day-02`:

1. **EF Core en profundidad**: configuración, relaciones (incidencias con comentarios y categorías), migraciones.
2. **Patrones de acceso a datos**: repositorio, consultas de solo lectura, proyecciones.
3. **De DataSet/DataTable a EF Core**: cómo traducir el acceso a datos de las aplicaciones legacy.
4. **Rendimiento de consultas** y primer análisis de SIREI.

## Referencias generales

> Enlaces comprobados el 6 de octubre de 2026.

- [Arquitecturas de aplicaciones web comunes](https://learn.microsoft.com/es-es/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures) — Lectura recomendada para repasar la mañana.
- [Información general de ASP.NET Core MVC](https://learn.microsoft.com/es-es/aspnet/core/mvc/overview?view=aspnetcore-10.0)
- [Tutorial: Introducción a ASP.NET Core MVC](https://learn.microsoft.com/es-es/aspnet/core/tutorials/first-mvc-app/start-mvc?view=aspnetcore-10.0) — Para practicar en casa a vuestro ritmo.
- [Arquitectura y conceptos de Razor Pages](https://learn.microsoft.com/es-es/aspnet/core/razor-pages/?view=aspnetcore-10.0)
