# 1. ¿Por qué separar en capas?

Antes de hablar de "arquitecturas" con nombre propio, veamos **qué problema** tenía el proyecto del día 1. Si no entendemos el problema, cualquier arquitectura parecerá burocracia.

## 1.1 El proyecto del día 1: todo en un proyecto

```
GestorIncidencias.Web/
├── Program.cs
├── Models/Incidencia.cs          ← entidad con setters públicos
├── Data/IncidenciasDbContext.cs  ← EF Core
├── Services/IncidenciaService.cs ← lógica de negocio
├── Services/EfIncidenciaRepository.cs
└── Endpoints/IncidenciasEndpoints.cs
```

Funciona y está bastante ordenado: usamos interfaces, inyección de dependencias y un servicio de negocio. Pero **las carpetas no son fronteras**. Nada impide que:

```csharp
// En un endpoint, saltándose el servicio y sus reglas...
app.MapPost("/api/incidencias/{id}/cerrar", async (int id, IncidenciasDbContext db) =>
{
    var i = await db.Incidencias.FindAsync(id);
    i!.Estado = EstadoIncidencia.Cerrada;    // ¿y si estaba Abierta? ¿y la fecha de resolución?
    await db.SaveChangesAsync();
});
```

El compilador lo acepta. El revisor de código quizá no lo vea. Seis meses después, nadie sabe dónde están las reglas.

## 1.2 Lo que ya conocéis: el code-behind de Web Forms

En muchas aplicaciones Web Forms (y SIREI no será una excepción) la lógica vive en el *code-behind*:

```csharp
protected void btnCerrar_Click(object sender, EventArgs e)
{
    using var cn = new SqlConnection(ConfigurationManager.ConnectionStrings["BD"].ConnectionString);
    var cmd = new SqlCommand("UPDATE Incidencias SET Estado='Cerrada' WHERE Id=@id", cn);
    cmd.Parameters.AddWithValue("@id", int.Parse(hfId.Value));
    cn.Open();
    cmd.ExecuteNonQuery();
    lblMensaje.Text = "Cerrada";
    gvIncidencias.DataBind();
}
```

En un solo método se mezclan **cuatro responsabilidades**:

| Responsabilidad | En el ejemplo |
|---|---|
| Presentación | `lblMensaje.Text`, `gvIncidencias.DataBind()` |
| Entrada de datos | `int.Parse(hfId.Value)` |
| Regla de negocio | ¿se puede cerrar? (¡nadie lo comprueba!) |
| Acceso a datos | `SqlConnection`, SQL a mano |

Consecuencias que seguro os suenan:

- **No se puede probar** la regla sin levantar IIS, una página y una base de datos.
- **No se puede reutilizar**: si mañana hay que cerrar incidencias desde una API o un proceso nocturno, se copia y pega.
- **Cambiar la base de datos** (o pasar a EF Core) obliga a tocar todas las páginas.
- **Migrar a ASP.NET Core** obliga a reescribirlo todo, porque la lógica está pegada a los controles de Web Forms.

> Esta es la razón principal por la que hoy dedicamos tanto tiempo a la arquitectura: **una migración es mucho más barata si la lógica de negocio no depende de la tecnología de presentación**. Lo que separemos bien hoy, no habrá que reescribirlo mañana.

## 1.3 La arquitectura "N capas" clásica

La respuesta tradicional (años 2000) fue separar en capas: **Presentación → Negocio (BLL) → Datos (DAL)**.

```
┌──────────────────┐
│  Presentación    │  (Web Forms, MVC)
└────────┬─────────┘
         │ referencia
┌────────▼─────────┐
│  Negocio (BLL)   │
└────────┬─────────┘
         │ referencia
┌────────▼─────────┐
│  Datos (DAL)     │  (ADO.NET, DataSets, EF)
└────────┬─────────┘
         ▼
     Base de datos
```

Es una mejora enorme sobre el *code-behind*, pero tiene un problema escondido: **el negocio depende de los datos**. La BLL referencia a la DAL, así que:

- Las clases de negocio reciben `DataTable`, entidades de EF o `SqlDataReader`.
- Para probar la BLL hace falta una base de datos.
- Cambiar la tecnología de datos "sube" hasta el negocio.

Lo más estable y valioso (las reglas de negocio) depende de lo más volátil (la tecnología de acceso a datos). Está al revés.

## 1.4 La idea clave: dar la vuelta a la flecha

Las arquitecturas modernas (Hexagonal, Onion, Clean) comparten **una sola idea**:

> **El negocio no depende de nada. Todo lo demás depende del negocio.**

```
   N capas clásica                    Clean / Hexagonal / Onion

   Presentación                        Presentación     Datos (EF Core)
        │                                    │              │
        ▼                                    ▼              ▼
     Negocio                               ┌──────────────────┐
        │                                  │     Negocio      │
        ▼                                  │ (define lo que   │
      Datos                                │   necesita)      │
                                           └──────────────────┘
```

¿Cómo puede el negocio guardar datos sin depender de la capa de datos? Con una **interfaz** que el negocio define y la capa de datos implementa. Es la **inversión de dependencias** (la "D" de SOLID), que ya usamos ayer sin ponerle nombre con `IIncidenciaRepository`. La diferencia es que hoy la interfaz y la implementación estarán en **proyectos distintos**, y el compilador vigilará que nadie se salte las fronteras.

## 1.5 Carpetas frente a proyectos

| | Carpetas (día 1) | Proyectos (día 2) |
|---|---|---|
| ¿Quién vigila las dependencias? | La disciplina del equipo | **El compilador** |
| ¿Puede el dominio usar EF Core por error? | Sí | No: no tiene la referencia |
| Coste | Ninguno | Más ficheros `.csproj`, algo más de ceremonia |
| Adecuado para | Prototipos, aplicaciones pequeñas | Aplicaciones con vida larga y varios desarrolladores |

> Separar en proyectos **no es obligatorio** para tener una buena arquitectura. Hay equipos que usan un solo proyecto con carpetas y reglas automáticas (por ejemplo, pruebas de arquitectura con NetArchTest). Para aprender, los proyectos tienen una gran ventaja: **los errores se ven al compilar**.

## Preguntas de repaso

1. En el ejemplo de `btnCerrar_Click`, ¿qué partes sobrevivirían intactas a una migración a ASP.NET Core y cuáles no?
2. ¿Por qué decimos que en la arquitectura N capas clásica "la flecha está al revés"?
3. ¿Qué mecanismo de C# permite que el negocio use la base de datos sin depender de ella?

## Referencias

> Enlaces comprobados el 6 de octubre de 2026.

- [Arquitecturas de aplicaciones web comunes](https://learn.microsoft.com/es-es/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures) — Libro electrónico oficial de Microsoft: aplicación monolítica, N capas tradicional y Clean Architecture, con diagramas.
- [Principios de arquitectura](https://learn.microsoft.com/es-es/dotnet/architecture/modern-web-apps-azure/architectural-principles) — Separación de responsabilidades, inversión de dependencias, dependencias explícitas.
- [Inserción de dependencias en ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-10.0) — Repaso del día 1.
