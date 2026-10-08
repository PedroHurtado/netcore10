namespace GestorIncidencias.Domain.Expedientes;

/// <summary>
/// Trámite de un expediente (tabla Tramites de SIREI). Forma parte del AGREGADO Expediente:
/// se añade y se completa a través de él, igual que los comentarios de una incidencia.
/// </summary>
public class Tramite
{
    public const int DescripcionLongitudMaxima = 200;

    private Tramite() { }   // para EF Core

    internal Tramite(string descripcion, DateTimeOffset fechaAlta)
    {
        Descripcion = descripcion;
        FechaAlta = fechaAlta;
        Pendiente = true;
    }

    public int Id { get; private set; }
    public string Descripcion { get; private set; } = string.Empty;
    public DateTimeOffset FechaAlta { get; private set; }

    /// <summary>Columna "Pendiente" (bit) del legacy: la regla de cierre de btnGuardar_Click la consultaba.</summary>
    public bool Pendiente { get; private set; }

    public DateTimeOffset? FechaCompletado { get; private set; }

    internal void Completar(DateTimeOffset fecha)
    {
        Pendiente = false;
        FechaCompletado = fecha;
    }
}
