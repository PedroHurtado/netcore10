# Curso: Desarrollo en ASP.NET Core

Curso de 25 horas (5 días × 5 horas, de 9:00 a 14:00) que combina **teoría y práctica**.
El material se construye de forma **incremental**: cada día parte del proyecto del día anterior.

## Estructura del repositorio

```
docs/
  README.md            ← planificación general del curso
  day-01/              ← teoría y laboratorios del día 1
  day-02/              ← teoría y laboratorios del día 2
  day-03/              ← teoría y laboratorios del día 3
  day-04/              ← teoría y laboratorios del día 4
  day-05/ ...          ← (se irá añadiendo)
src/
  day-01/              ← proyecto de ejemplo al FINAL del día 1
  day-02/              ← solución en capas (Clean Architecture) con MVC, Razor Pages y API
  day-03/              ← EF Core: relaciones, lecturas con proyección y paginación
  day-04/              ← Identity, autorización, sesión, logging estructurado y proyectos de pruebas
  day-05/ ...          ← cada día es una copia evolucionada del anterior
```

## Requisitos

- .NET SDK 10 (`dotnet --list-sdks` debe mostrar una versión 10.x)
- Visual Studio 2026 / Visual Studio Code con C# Dev Kit / JetBrains Rider
- Navegador y, opcionalmente, la extensión REST Client (VS Code) para los ficheros `.http`

No hace falta instalar ningún servidor de base de datos: usamos **EF Core con el proveedor InMemory**.

## Cómo ejecutar el ejemplo de un día

```bash
cd src/day-01
dotnet run --project GestorIncidencias.Web
```

Abre http://localhost:5196

Desde el día 4 la solución incluye pruebas automáticas:

```bash
cd src/day-04
dotnet test
```
