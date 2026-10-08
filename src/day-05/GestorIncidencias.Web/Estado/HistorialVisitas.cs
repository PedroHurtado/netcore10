using System.Text.Json;

namespace GestorIncidencias.Web.Estado;

/// <summary>Una incidencia vista recientemente (lo que se guarda en la sesión).</summary>
public record VisitaReciente(int Id, string Titulo);

/// <summary>
/// Ejemplo de SESIÓN en ASP.NET Core: las últimas 5 incidencias que ha abierto el usuario.
///
/// Diferencias con Session["..."] de Web Forms (ver docs/day-04/02-gestion-estado.md):
///   - Hay que activarla (AddSession + UseSession en Program.cs).
///   - Solo guarda bytes o texto: los objetos se serializan (aquí, a JSON). Nada de guardar un DataSet entero.
///   - Se guarda en un IDistributedCache: en memoria de un servidor (curso) o en Redis/SQL Server (varios servidores).
///   - Es la "memoria de trabajo" del usuario, NO una base de datos: si se pierde, la aplicación sigue funcionando.
///
/// Métodos de extensión sobre ISession para que el controlador escriba HttpContext.Session.RegistrarVisita(...),
/// sin cadenas mágicas ni conversiones repartidas por el código.
/// </summary>
public static class HistorialVisitas
{
    private const string Clave = "HistorialVisitas";
    private const int Maximo = 5;

    public static IReadOnlyList<VisitaReciente> ObtenerVisitas(this ISession sesion) =>
        sesion.GetString(Clave) is { } json
            ? JsonSerializer.Deserialize<List<VisitaReciente>>(json) ?? []
            : [];

    public static void RegistrarVisita(this ISession sesion, int id, string titulo)
    {
        var visitas = sesion.ObtenerVisitas()
            .Where(v => v.Id != id)                         // si ya estaba, se mueve al principio
            .Prepend(new VisitaReciente(id, titulo))
            .Take(Maximo)
            .ToList();

        sesion.SetString(Clave, JsonSerializer.Serialize(visitas));
    }
}
