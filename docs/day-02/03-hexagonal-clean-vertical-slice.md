# 3. Hexagonal, Clean y Vertical Slice: comparativa

Cuando un equipo empieza un proyecto o una migración en .NET, aparecen siempre tres nombres: **Arquitectura Hexagonal**, **Clean Architecture** y **Vertical Slice Architecture**. Se presentan a veces como rivales, pero responden a preguntas distintas. Este capítulo las compara con el mismo ejemplo para que podáis decidir con criterio y no por moda.

## 3.1 Qué pregunta responde cada una

| Arquitectura | Pregunta que responde | Idea central |
|---|---|---|
| **Hexagonal** (Puertos y Adaptadores) | ¿Cómo aíslo el negocio de **lo que hay fuera** (interfaz, BD, otros sistemas)? | El núcleo habla con el exterior solo a través de **puertos** (interfaces); la tecnología se conecta con **adaptadores** |
| **Clean** | ¿Cómo organizo **las capas** para que el negocio no dependa de nada? | Círculos concéntricos; **las dependencias apuntan hacia dentro** |
| **Vertical Slice** | ¿Cómo organizo el código para que **cada cambio toque el menor número de sitios**? | Agrupar por **funcionalidad** (caso de uso), no por capa técnica |

Hexagonal y Clean son **la misma familia** (aislar el dominio; Onion también pertenece a ella). Vertical Slice cambia de eje: no pregunta "qué depende de qué" sino "qué cambia junto".

```
Clean / Hexagonal: cortes HORIZONTALES          Vertical Slice: cortes VERTICALES

┌─────────────────────────────────────┐        ┌────────┬────────┬────────┬────────┐
│ Web           (todas las pantallas) │        │        │        │        │        │
├─────────────────────────────────────┤        │ Crear  │Resolver│ Cerrar │Listar  │
│ Application   (todos los casos uso) │        │        │        │        │        │
├─────────────────────────────────────┤        │ (web + │ (web + │ (web + │ (web + │
│ Domain        (todas las entidades) │        │ lógica │ lógica │ lógica │ lógica │
├─────────────────────────────────────┤        │ +datos)│ +datos)│ +datos)│ +datos)│
│ Infrastructure (todo el acceso BD)  │        │        │        │        │        │
└─────────────────────────────────────┘        └────────┴────────┴────────┴────────┘
```

## 3.2 Arquitectura Hexagonal (Puertos y Adaptadores)

Propuesta por **Alistair Cockburn** (artículo de 2005). El hexágono no significa nada especial: es solo una forma de dibujar que el núcleo tiene **varias caras** por las que se conecta con el mundo, sin "arriba" ni "abajo".

```
                   ADAPTADORES DE ENTRADA                        ADAPTADORES DE SALIDA
                   (conducen la aplicación)                      (la aplicación los conduce)

   Navegador ──▶ [Controlador MVC] ──┐                      ┌──▶ [Repositorio EF Core] ──▶ SQL Server
                                     │    ╱‾‾‾‾‾‾‾‾‾‾‾‾╲    │
   SPA/móvil ──▶ [Controlador API] ──┼──▶ ○   NÚCLEO    ○ ──┼──▶ [Notificador SMTP]    ──▶ Correo
                                     │   │  (dominio +  │   │
   Pruebas   ──▶ [Test xUnit]     ───┤   │ casos de uso)│   └──▶ [Cliente HTTP]        ──▶ API externa
                                     │    ╲____________╱
   Cron      ──▶ [Proceso batch]  ───┘   ○ = PUERTO (interfaz)
```

Vocabulario:

| Término | Qué es | En nuestro proyecto |
|---|---|---|
| **Puerto de entrada** (*driving / primary port*) | Interfaz que expone lo que la aplicación sabe hacer | `IIncidenciaService` |
| **Adaptador de entrada** | Traduce una tecnología concreta a llamadas al puerto | `IncidenciasController`, `IncidenciasApiController`, las Razor Pages |
| **Puerto de salida** (*driven / secondary port*) | Interfaz de lo que la aplicación necesita del exterior | `IIncidenciaRepository` |
| **Adaptador de salida** | Implementa el puerto de salida con una tecnología | `EfIncidenciaRepository` |

Organización típica de carpetas o proyectos:

```
Nucleo/
  Dominio/Incidencia.cs
  Puertos/Entrada/IGestionIncidencias.cs
  Puertos/Salida/IIncidenciaRepository.cs
  Puertos/Salida/INotificador.cs
  Servicios/GestionIncidencias.cs
Adaptadores/
  Entrada/Web/IncidenciasController.cs
  Entrada/Api/IncidenciasApiController.cs
  Salida/Persistencia/EfIncidenciaRepository.cs
  Salida/Correo/SmtpNotificador.cs
```

> **Observad que nuestra solución ya es hexagonal**, aunque los proyectos se llamen como en Clean: `IIncidenciaService` es un puerto de entrada con **tres adaptadores** (MVC, Razor Pages, API), e `IIncidenciaRepository` es un puerto de salida con un adaptador (EF Core). En la práctica, Hexagonal y Clean producen código casi idéntico; cambian el vocabulario y la forma de dibujarlo.

## 3.3 Clean Architecture (y Onion)

Propuesta por **Robert C. Martin** en 2012 como síntesis de Hexagonal, **Onion Architecture** (Jeffrey Palermo, 2008) y otras. Añade a Hexagonal una **organización interna del núcleo** en círculos:

| Círculo (Martin) | Equivalente habitual en .NET | Contenido |
|---|---|---|
| Entities | `Domain` | Reglas de negocio de la empresa |
| Use Cases | `Application` | Reglas de la aplicación (casos de uso) |
| Interface Adapters | `Web` + `Infrastructure` | Controladores, presentadores, repositorios |
| Frameworks & Drivers | ASP.NET Core, EF Core, la BD | Detalles |

La aportación práctica de Clean frente a Hexagonal es que **separa el dominio de los casos de uso** y da una **regla única y comprobable** (la regla de dependencia), que en .NET se hace cumplir con referencias entre proyectos. Por eso es la que más plantillas y ejemplos tiene en .NET. Todo el detalle está en el [capítulo 2](02-clean-architecture.md).

## 3.4 Vertical Slice Architecture

Popularizada por **Jimmy Bogard** (artículo de 2018). Parte de una observación: en una arquitectura por capas, **añadir una funcionalidad obliga a tocar todas las capas**, y cada capa acumula código de muchas funcionalidades que no tienen nada que ver entre sí.

La propuesta: **cada caso de uso es una "rebanada" que contiene todo lo que necesita**, de la petición HTTP a la base de datos. Se minimiza el acoplamiento **entre** rebanadas y se maximiza la cohesión **dentro** de cada una.

```
Features/
  Incidencias/
    CrearIncidencia.cs        ← petición + validación + lógica + acceso a datos + endpoint
    ResolverIncidencia.cs
    CerrarIncidencia.cs
    ListarIncidencias.cs
    ObtenerIncidencia.cs
Dominio/
  Incidencia.cs               ← (opcional) entidades compartidas con sus reglas
Datos/
  AppDbContext.cs             ← compartido por todas las rebanadas
```

Una rebanada típica (con Minimal APIs y EF Core directamente, **sin repositorio**):

```csharp
// Features/Incidencias/ResolverIncidencia.cs
public static class ResolverIncidencia
{
    public record Respuesta(int Id, string Estado, DateTimeOffset? FechaResolucion);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/incidencias/{id:int}/resolver", Ejecutar);

    private static async Task<IResult> Ejecutar(int id, AppDbContext db, TimeProvider reloj, CancellationToken ct)
    {
        var incidencia = await db.Incidencias.FindAsync([id], ct);
        if (incidencia is null)
            return Results.NotFound();

        var resultado = incidencia.Resolver(reloj.GetUtcNow());     // la regla sigue en la entidad
        if (!resultado.Exito)
            return Results.Problem(resultado.Error, statusCode: StatusCodes.Status409Conflict);

        await db.SaveChangesAsync(ct);
        return Results.Ok(new Respuesta(incidencia.Id, incidencia.Estado.ToString(), incidencia.FechaResolucion));
    }
}
```

Características:

- **Cada rebanada decide su propia implementación**: una consulta compleja puede usar SQL o Dapper; un CRUD simple, EF Core directamente; un proceso complejo, un modelo de dominio rico.
- **No hay capas obligatorias**: si una rebanada no necesita un repositorio, no lo tiene.
- Suele combinarse con **CQRS** (separar comandos que cambian datos de consultas que los leen).
- Es frecuente usar una librería de *mediator* (MediatR, Wolverine...) para despachar peticiones a *handlers*, pero **no es obligatorio**. Ojo: desde 2025 MediatR tiene licencia comercial (gratuita solo para organizaciones de menos de 5 millones de dólares de facturación y casos similares); un *handler* puede ser simplemente una clase registrada en DI o, como arriba, un método estático.

## 3.5 El mismo cambio en las tres: "añadir Reabrir incidencia"

La mejor forma de compararlas es ver **qué ficheros hay que tocar** para añadir una funcionalidad. Es exactamente lo que haremos en el [Lab 2](labs/lab-02-reabrir-incidencia.md).

| Paso | Clean (nuestro proyecto) | Hexagonal | Vertical Slice |
|---|---|---|---|
| Regla "solo se reabre una resuelta" | `Domain/Incidencia.cs` → `Reabrir()` | `Nucleo/Dominio/Incidencia.cs` | `Dominio/Incidencia.cs` (o dentro de la rebanada si no hay dominio compartido) |
| Caso de uso | `Application/IIncidenciaService.cs` + `IncidenciaService.cs` | `Puertos/Entrada/IGestionIncidencias.cs` + `Servicios/GestionIncidencias.cs` | `Features/Incidencias/ReabrirIncidencia.cs` |
| Acceso a datos | Nada (el repositorio ya sirve) | Nada | Dentro de la misma rebanada |
| Interfaz | `Web/Controllers/IncidenciasController.cs` + vista | `Adaptadores/Entrada/Web/...` | Dentro de la misma rebanada (endpoint) + vista |
| **Ficheros tocados** | **4–5, en 3 proyectos** | **4–5, en varias carpetas** | **1–2, en una carpeta** |

## 3.6 Tabla comparativa

| Criterio | Hexagonal | Clean | Vertical Slice |
|---|---|---|---|
| Eje de organización | Dentro / fuera (núcleo y adaptadores) | Capas concéntricas | Funcionalidades |
| Regla principal | El núcleo solo habla por puertos | Las dependencias apuntan hacia dentro | Minimizar acoplamiento entre rebanadas |
| Ficheros a tocar por funcionalidad | Varios | Varios (uno por capa) | Pocos (casi todo en la rebanada) |
| Curva de aprendizaje | Media (vocabulario puertos/adaptadores) | Media (muchas plantillas y ejemplos) | Baja para empezar; **alta para hacerlo bien** (requiere saber cuándo refactorizar) |
| Aislamiento del dominio | **Muy alto** | **Muy alto** | Depende de la disciplina del equipo |
| Pruebas unitarias del negocio | Muy fáciles (se sustituyen los adaptadores) | Muy fáciles | Más naturales las pruebas de integración por rebanada |
| Cambiar de tecnología (BD, UI) | **Diseñada para ello** | Muy fácil | Rebanada a rebanada |
| Riesgo típico | Exceso de interfaces "por si acaso" | Ceremonia: muchos proyectos y capas para un CRUD | Duplicación de lógica entre rebanadas; reglas de negocio dispersas |
| Encaje con DDD | Excelente | Excelente | Bueno, si se mantiene un dominio compartido |
| Encaje con CRUD simple | Pobre (demasiada estructura) | Pobre (demasiada estructura) | **Excelente** |
| Varias interfaces sobre la misma lógica | **Excelente** (es su razón de ser) | Excelente | Regular: cada rebanada suele estar ligada a un endpoint |
| Equipos grandes trabajando en paralelo | Bien | Bien, pero conflictos en ficheros compartidos (servicios grandes) | **Muy bien**: cada uno en su rebanada |
| Plantillas y ejemplos en .NET | Pocos con ese nombre | **Muchos** | Bastantes y en aumento |

## 3.7 Pros y contras

### Hexagonal

| ✅ Pros | ❌ Contras |
|---|---|
| Aislamiento máximo de la tecnología: se puede probar el núcleo sin nada real | Vocabulario abstracto (puerto, adaptador, conductor, conducido) que cuesta explicar |
| Varias entradas (web, API, colas, procesos batch) reutilizan los mismos casos de uso | Tendencia a crear una interfaz para todo, aunque solo haya una implementación |
| Facilita sustituir proveedores externos (correo, pagos, ERP) | No dice cómo organizar el interior del núcleo |
| Encaja muy bien con integraciones y sistemas con muchos sistemas vecinos | Más indirección: seguir una petición requiere saltar entre interfaces |

### Clean

| ✅ Pros | ❌ Contras |
|---|---|
| Regla simple y verificable por el compilador (referencias entre proyectos) | Ceremonia: 4+ proyectos, DTOs, mapeos, interfaces, incluso para operaciones triviales |
| Mucha documentación, plantillas y gente que la conoce: fácil de incorporar personas | Cada funcionalidad se reparte entre varias capas: cambios "en abanico" |
| El dominio es portable: sobrevive a cambios de framework (ideal para migraciones) | Los servicios de aplicación tienden a crecer hasta ser enormes (`IncidenciaService` con 40 métodos) |
| Facilita las pruebas unitarias del negocio | Riesgo de "arquitectura por plantilla": capas vacías que solo reenvían llamadas |
| Separa claramente reglas de dominio y de aplicación | Abstraer EF Core detrás de repositorios a veces oculta capacidades útiles (proyecciones, `Include`...) |

### Vertical Slice

| ✅ Pros | ❌ Contras |
|---|---|
| Un cambio = una carpeta o un fichero: muy fácil de encontrar y revisar | Si no hay un dominio compartido, **las reglas de negocio se duplican** entre rebanadas |
| Cada rebanada usa la solución más simple que funciona (CRUD directo, SQL, dominio rico...) | Exige criterio para saber cuándo extraer código común; los equipos sin experiencia acaban con *code-behind* moderno |
| Añadir funcionalidades no rompe otras: bajo acoplamiento entre rebanadas | Sin capas, el compilador no impide que una rebanada use cualquier cosa |
| Ideal para equipos grandes o funcionalidades independientes | Menos natural cuando la misma lógica se expone por varias interfaces (web + API + batch) |
| Encaja con CQRS: las lecturas pueden ser consultas directas y optimizadas | Menos plantillas "oficiales"; cada equipo lo interpreta a su manera |

## 3.8 Dónde sí y dónde no

### Hexagonal

| ✅ Dónde sí | ❌ Dónde no |
|---|---|
| Aplicaciones con **muchas integraciones** (ERP, servicios web de otros organismos, colas) | CRUD de mantenimiento de tablas |
| Lógica que se expone por **varios canales** (web, API, procesos nocturnos) | Prototipos y herramientas internas de vida corta |
| Sistemas donde se prevé **cambiar proveedores** (correo, firma, pagos, almacenamiento) | Equipos sin experiencia con interfaces y DI |
| Necesidad fuerte de **probar el negocio sin infraestructura** | Aplicaciones cuyo "negocio" es casi solo leer y mostrar datos |

### Clean

| ✅ Dónde sí | ❌ Dónde no |
|---|---|
| Aplicaciones de **vida larga** (10+ años) con reglas de negocio relevantes | Aplicaciones pequeñas o CRUD puro: la estructura pesa más que el código |
| **Migraciones**: el dominio extraído sobrevive al cambio de Web Forms → MVC → lo que venga | Microservicios diminutos con 2–3 operaciones |
| Equipos medianos que necesitan **reglas claras** y fáciles de enseñar | Equipos que van a seguir la plantilla sin entenderla (capas vacías) |
| Proyectos donde varios equipos comparten el mismo modelo de dominio | Funcionalidades muy heterogéneas sin modelo común (informes, exportaciones) |

### Vertical Slice

| ✅ Dónde sí | ❌ Dónde no |
|---|---|
| Aplicaciones con **muchas funcionalidades independientes** (pantallas, informes, exportaciones) | Dominios con reglas complejas **compartidas** entre muchas operaciones, si no se mantiene un dominio común |
| APIs donde cada endpoint es un caso de uso distinto | Equipos poco experimentados sin revisión de código ni criterio para refactorizar |
| Sistemas con **lecturas muy variadas** (CQRS): cada consulta, la suya | Cuando la misma operación se expone por muchos canales |
| Equipos grandes trabajando en paralelo | Cuando la organización exige una estructura homogénea y documentada |
| **Migraciones página a página** (cada pantalla de Web Forms → una rebanada) | |

## 3.9 Mitos y malentendidos

| Mito | Realidad |
|---|---|
| "Clean Architecture = 4 proyectos con esos nombres" | La regla es la dependencia hacia dentro. Se puede cumplir con 2 proyectos o con carpetas y pruebas de arquitectura |
| "Hexagonal es para microservicios" | Es para aislar el núcleo; sirve igual en un monolito |
| "Vertical Slice = no tener arquitectura" | Es otra forma de organizar; exige **más** criterio, no menos |
| "Vertical Slice necesita MediatR" | MediatR es una opción. Un *handler* puede ser una clase normal inyectada o un método |
| "Hay que tener un repositorio sobre EF Core siempre" | `DbContext` ya es un repositorio + unidad de trabajo. Se abstrae cuando aporta (aislar el dominio, pruebas, cambiar de tecnología), no por costumbre |
| "Son excluyentes" | Se combinan a menudo (3.10) |
| "La arquitectura se elige una vez para siempre" | Se puede empezar simple y evolucionar; lo importante es que el negocio no quede pegado a la tecnología |

## 3.10 Combinarlas: lo que se ve en proyectos reales

La combinación más habitual en .NET hoy es:

> **Dominio rico compartido** (Clean / Hexagonal) + **casos de uso organizados por funcionalidad** (Vertical Slice) dentro de la capa de aplicación.

```
GestorIncidencias.Domain/              ← entidades y reglas compartidas (Clean)
  Incidencias/Incidencia.cs
GestorIncidencias.Application/
  Incidencias/
    Crear/CrearIncidencia.cs           ← comando + handler + validación (Slice)
    Resolver/ResolverIncidencia.cs
    Listar/ListarIncidencias.cs        ← consulta con su propio DTO (Slice + CQRS)
  Abstracciones/IIncidenciaRepository.cs  ← puerto (Hexagonal)
GestorIncidencias.Infrastructure/      ← adaptadores (Hexagonal)
GestorIncidencias.Web/                 ← adaptadores de entrada
```

Así se obtiene lo mejor de cada una: reglas protegidas en el dominio, un fichero por caso de uso (en lugar de un servicio gigante) y tecnología aislada. Es la evolución natural de nuestro `IncidenciaService` cuando crezca: cuando pase de 10–15 métodos, conviene partirlo en un *handler* por caso de uso.

## 3.11 Cómo decidir

```
¿La aplicación tiene reglas de negocio relevantes (estados, cálculos, validaciones cruzadas)?
│
├── NO → ¿Es pequeña o de vida corta?
│        ├── SÍ → Un solo proyecto con carpetas (como el día 1). Sin capas.
│        └── NO → Vertical Slice (cada pantalla/endpoint, su rebanada).
│
└── SÍ → ¿La misma lógica se usa desde varios canales o integra muchos sistemas externos?
         ├── SÍ → Clean / Hexagonal (puertos para cada canal y cada sistema externo).
         └── NO → ¿Muchas funcionalidades independientes o equipo grande?
                  ├── SÍ → Dominio rico + casos de uso por rebanada (combinación 3.10).
                  └── NO → Clean con servicios de aplicación (nuestro día 2).
```

## 3.12 Aplicado a migraciones desde Web Forms y MVC 5

Para los casos del curso (una aplicación Web Forms y una MVC), algunas pautas generales que retomaremos el día 5:

| Situación en la aplicación legacy | Enfoque recomendado |
|---|---|
| Lógica de negocio escondida en el *code-behind* de muchas páginas | **Extraer primero el dominio** (Clean): sacar las reglas a clases sin dependencias. Es el paso que más valor da y el que permite migrar la interfaz después |
| Muchas pantallas de mantenimiento independientes y sencillas | **Vertical Slice + Razor Pages**: una página de Web Forms → una Razor Page con su lógica. Encaja de forma natural |
| Aplicación MVC 5 que ya tiene servicios y repositorios | **Mantener la estructura** y llevarla a Clean: mover entidades a Domain, servicios a Application, acceso a datos a Infrastructure |
| Integraciones con otros sistemas (servicios web, ficheros, correo) | **Puertos y adaptadores** (Hexagonal): una interfaz por sistema externo, para poder probar y sustituir |
| Migración gradual (convivencia de lo viejo y lo nuevo) | El dominio y la aplicación en bibliotecas `netstandard2.0`/.NET que puedan usar ambas aplicaciones durante la transición |

> Regla práctica para el equipo: **empezad con Clean "ligera"** (los 4 proyectos de hoy, un servicio por agregado) porque deja reglas claras y el compilador ayuda. Cuando un servicio crezca demasiado, partidlo en casos de uso por rebanada. No adoptéis Vertical Slice "pura" hasta que el equipo tenga soltura detectando lógica duplicada.

## Preguntas de discusión

1. Nuestro proyecto del día 2, ¿es Clean, Hexagonal o ambas? Justifícalo con nombres de clases.
2. Un compañero propone añadir `IGenericRepository<T>`, `IUnitOfWork`, AutoMapper y MediatR al proyecto "porque es Clean Architecture". ¿Qué le responderías?
3. Una aplicación tiene 60 pantallas de mantenimiento de tablas maestras y 5 procesos con reglas complejas. ¿Qué enfoque usarías para cada parte?
4. ¿Qué riesgo tiene Vertical Slice en un equipo con poca experiencia? ¿Cómo lo mitigarías?
5. En la migración de una página Web Forms con 800 líneas en el *code-behind*, ¿qué extraerías primero?

## Referencias

> Enlaces comprobados el 6 de octubre de 2026.

**Fuentes originales**

- [Hexagonal Architecture](https://alistair.cockburn.us/hexagonal-architecture/) (inglés) — Artículo original de Alistair Cockburn sobre Puertos y Adaptadores.
- [The Onion Architecture: part 1](https://jeffreypalermo.com/2008/07/the-onion-architecture-part-1/) (inglés) — Jeffrey Palermo (2008).
- [The Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html) (inglés) — Robert C. Martin (2012).
- [Vertical Slice Architecture](https://www.jimmybogard.com/vertical-slice-architecture/) (inglés) — Jimmy Bogard (2018).
- [AutoMapper and MediatR Commercial Editions Launch Today](https://www.jimmybogard.com/automapper-and-mediatr-commercial-editions-launch-today/) (inglés) — Condiciones de la licencia comercial de MediatR.

**Documentación oficial de Microsoft**

- [Arquitecturas de aplicaciones web comunes](https://learn.microsoft.com/es-es/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures) — N capas, Clean Architecture (también llamada Onion o Hexagonal en el texto).
- [Aplicación de patrones CQRS y DDD simplificados en un microservicio](https://learn.microsoft.com/es-es/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/apply-simplified-microservice-cqrs-ddd-patterns) — Separación de lecturas y escrituras, base de muchas implementaciones de Vertical Slice.
- [Patrón CQRS](https://learn.microsoft.com/es-es/azure/architecture/patterns/cqrs) (Azure Architecture Center).

**Ejemplos de código**

- [jasontaylordev/CleanArchitecture](https://github.com/jasontaylordev/CleanArchitecture) (inglés) — Clean Architecture con comandos y consultas agrupados en carpetas por funcionalidad (similar a la combinación de 3.10).
- [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture) (inglés) — Plantilla Clean de Steve Smith; incluye también una variante mínima (`min-clean`).
