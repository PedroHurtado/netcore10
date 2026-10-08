using GestorIncidencias.Domain.Comun;

namespace GestorIncidencias.Domain.Lotes;

/// <summary>
/// Caso práctico Noticom (día 5): una remesa agrupa lotes VALIDADOS para enviarlos juntos (TipoEjecucion "cr").
///
/// En el legacy, crearRemesa() en JavaScript comprobaba que hubiera nombre y algún lote marcado, y el servidor
/// hacía los UPDATE sin volver a comprobar nada. Aquí las reglas están en el servidor, en un único sitio:
/// el JavaScript puede seguir avisando antes de enviar (comodidad), pero ya no es la única barrera.
/// </summary>
public class Remesa
{
    public const int NombreLongitudMaxima = 60;

    private readonly List<Lote> _lotes = [];

    private Remesa() { }   // para EF Core

    public int Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public DateTimeOffset FechaCreacion { get; private set; }

    public IReadOnlyCollection<Lote> Lotes => _lotes.AsReadOnly();

    public static Resultado<Remesa> Crear(string nombre, IReadOnlyCollection<Lote> lotes, DateTimeOffset ahora)
    {
        nombre = (nombre ?? string.Empty).Trim();

        if (nombre.Length is 0 or > NombreLongitudMaxima)
            return Resultado<Remesa>.Fallo($"El nombre de la remesa debe tener entre 1 y {NombreLongitudMaxima} caracteres.");

        if (lotes.Count == 0)
            return Resultado<Remesa>.Fallo("No se ha seleccionado ningún lote.");

        var noRemesables = lotes.Where(l => !l.PuedeRemesarse).Select(l => l.Id).ToList();
        if (noRemesables.Count > 0)
            return Resultado<Remesa>.Fallo(
                $"Solo se pueden remesar lotes validados y sin remesa. No cumplen la condición: {string.Join(", ", noRemesables)}.",
                TipoError.Conflicto);

        var remesa = new Remesa { Nombre = nombre, FechaCreacion = ahora };
        foreach (var lote in lotes)
        {
            lote.IncluirEn(remesa);
            remesa._lotes.Add(lote);
        }
        return Resultado<Remesa>.Ok(remesa);
    }
}
