using GestorIncidencias.Domain.Comun;

namespace GestorIncidencias.Domain.Lotes;

/// <summary>
/// Caso práctico Noticom (día 5): un lote de notificaciones de un proceso y un ejercicio.
///
/// En Noticom (MVC 5) las reglas estaban repartidas entre la vista (qué botones y columnas se ven según
/// TipoEjecucion), el JavaScript (que ocultaba columnas por número) y LoteService (que hacía los UPDATE).
/// Aquí, la entidad decide qué se puede hacer con un lote; la vista solo pregunta.
/// </summary>
public class Lote
{
    public const int DescripcionLongitudMaxima = 150;

    private Lote() { }   // para EF Core

    public int Id { get; private set; }

    /// <summary>Código del proceso que genera las notificaciones (p. ej. 120 = "Liquidaciones IBI").</summary>
    public int Proceso { get; private set; }

    public int Ejercicio { get; private set; }
    public string Descripcion { get; private set; } = string.Empty;
    public TipoNotificacion Tipo { get; private set; }
    public int NumeroNotificaciones { get; private set; }
    public EstadoLote Estado { get; private set; }
    public DateTimeOffset FechaAlta { get; private set; }
    public DateTimeOffset? FechaValidacion { get; private set; }

    /// <summary>Remesa en la que se ha incluido el lote (null mientras no esté remesado).</summary>
    public int? RemesaId { get; private set; }

    public Remesa? Remesa { get; private set; }

    public bool PuedeValidarse => Estado == EstadoLote.PendienteValidacion;
    public bool PuedeBorrarse => Estado == EstadoLote.PendienteValidacion;
    public bool PuedeRemesarse => Estado == EstadoLote.Validado && RemesaId is null && Remesa is null;

    public static Resultado<Lote> Crear(
        int proceso, int ejercicio, string descripcion, TipoNotificacion tipo, int numeroNotificaciones, DateTimeOffset fechaAlta)
    {
        descripcion = (descripcion ?? string.Empty).Trim();

        if (proceso <= 0)
            return Resultado<Lote>.Fallo("El proceso es obligatorio.");
        if (ejercicio is < 2000 or > 2100)
            return Resultado<Lote>.Fallo("El ejercicio no es válido.");
        if (descripcion.Length is 0 or > DescripcionLongitudMaxima)
            return Resultado<Lote>.Fallo($"La descripción debe tener entre 1 y {DescripcionLongitudMaxima} caracteres.");
        if (numeroNotificaciones <= 0)
            return Resultado<Lote>.Fallo("Un lote debe tener al menos una notificación.");

        return Resultado<Lote>.Ok(new Lote
        {
            Proceso = proceso,
            Ejercicio = ejercicio,
            Descripcion = descripcion,
            Tipo = tipo,
            NumeroNotificaciones = numeroNotificaciones,
            Estado = EstadoLote.PendienteValidacion,
            FechaAlta = fechaAlta
        });
    }

    /// <summary>PendienteValidacion → Validado (TipoEjecucion "v" del legacy).</summary>
    public Resultado Validar(DateTimeOffset ahora)
    {
        if (!PuedeValidarse)
            return Resultado.Fallo($"Solo se puede validar un lote pendiente de validación (lote {Id}: {Estado}).", TipoError.Conflicto);

        Estado = EstadoLote.Validado;
        FechaValidacion = ahora;
        return Resultado.Ok();
    }

    /// <summary>
    /// ¿Se puede borrar? (TipoEjecucion "b"). Borrar lo hace el repositorio: la entidad solo da permiso.
    /// En el legacy esta comprobación NO existía en el servidor: bastaba con que la columna del botón estuviera oculta.
    /// </summary>
    public Resultado ComprobarBorrado() =>
        PuedeBorrarse
            ? Resultado.Ok()
            : Resultado.Fallo($"Solo se puede borrar un lote pendiente de validación (lote {Id}: {Estado}).", TipoError.Conflicto);

    /// <summary>Lo llama Remesa.Crear: un lote solo entra en una remesa a través de ella.</summary>
    internal void IncluirEn(Remesa remesa)
    {
        Remesa = remesa;
        Estado = EstadoLote.Remesado;
    }
}
