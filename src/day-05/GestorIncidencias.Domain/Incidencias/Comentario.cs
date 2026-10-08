namespace GestorIncidencias.Domain.Incidencias;

/// <summary>
/// Comentario de una incidencia. Forma parte del AGREGADO Incidencia:
/// no se crea ni se guarda por separado, sino con incidencia.AgregarComentario(...).
/// Por eso el constructor es internal (solo lo usa Incidencia) y no tiene repositorio propio.
/// </summary>
public class Comentario
{
    public const int TextoLongitudMaxima = 1000;
    public const int AutorLongitudMaxima = 100;

    private Comentario() { }   // para EF Core

    internal Comentario(string texto, string autor, DateTimeOffset fecha)
    {
        Texto = texto;
        Autor = autor;
        Fecha = fecha;
    }

    public int Id { get; private set; }
    public string Texto { get; private set; } = string.Empty;
    public string Autor { get; private set; } = string.Empty;
    public DateTimeOffset Fecha { get; private set; }

    // OJO: no hay propiedad IncidenciaId. La clave ajena existe en la tabla, pero el dominio
    // no la necesita: EF Core la gestiona como "propiedad sombra" (ver IncidenciaConfiguracion).
}
