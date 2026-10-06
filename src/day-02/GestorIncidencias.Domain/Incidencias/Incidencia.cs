using GestorIncidencias.Domain.Comun;

namespace GestorIncidencias.Domain.Incidencias;

/// <summary>
/// Entidad del dominio con comportamiento ("modelo rico").
///
/// Diferencias con la entidad del día 1:
///  - Los setters son privados: nadie de fuera puede poner Estado = Cerrada "a mano".
///  - Los cambios de estado se hacen con métodos (Iniciar, Resolver, Cerrar) que
///    comprueban las reglas de negocio y devuelven un Resultado.
///  - No conoce EF Core, ni ASP.NET Core, ni la base de datos.
/// </summary>
public class Incidencia
{
    public const int TituloLongitudMinima = 5;
    public const int TituloLongitudMaxima = 120;
    public const int DescripcionLongitudMaxima = 2000;

    // EF Core necesita un constructor sin parámetros para materializar la entidad.
    // Lo hacemos privado para que el resto del código use el método Crear.
    private Incidencia() { }

    public int Id { get; private set; }
    public string Titulo { get; private set; } = string.Empty;
    public string? Descripcion { get; private set; }
    public Prioridad Prioridad { get; private set; }
    public EstadoIncidencia Estado { get; private set; }
    public DateTimeOffset FechaAlta { get; private set; }
    public DateTimeOffset? FechaResolucion { get; private set; }

    /// <summary>Una incidencia "cuenta" como abierta mientras no esté resuelta ni cerrada.</summary>
    public bool EstaAbierta => Estado is EstadoIncidencia.Abierta or EstadoIncidencia.EnCurso;

    /// <summary>
    /// Método de factoría: única forma de crear una incidencia válida.
    /// </summary>
    public static Resultado<Incidencia> Crear(string titulo, string? descripcion, Prioridad prioridad, DateTimeOffset fechaAlta)
    {
        titulo = (titulo ?? string.Empty).Trim();

        if (titulo.Length is < TituloLongitudMinima or > TituloLongitudMaxima)
            return Resultado<Incidencia>.Fallo(
                $"El título debe tener entre {TituloLongitudMinima} y {TituloLongitudMaxima} caracteres.");

        if (descripcion?.Length > DescripcionLongitudMaxima)
            return Resultado<Incidencia>.Fallo(
                $"La descripción no puede superar {DescripcionLongitudMaxima} caracteres.");

        return Resultado<Incidencia>.Ok(new Incidencia
        {
            Titulo = titulo,
            Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim(),
            Prioridad = prioridad,
            Estado = EstadoIncidencia.Abierta,
            FechaAlta = fechaAlta
        });
    }

    /// <summary>Abierta → EnCurso.</summary>
    public Resultado Iniciar()
    {
        if (Estado != EstadoIncidencia.Abierta)
            return Resultado.Fallo($"Solo se puede iniciar una incidencia abierta (estado actual: {Estado}).", TipoError.Conflicto);

        Estado = EstadoIncidencia.EnCurso;
        return Resultado.Ok();
    }

    /// <summary>Abierta o EnCurso → Resuelta.</summary>
    public Resultado Resolver(DateTimeOffset fecha)
    {
        if (!EstaAbierta)
            return Resultado.Fallo($"La incidencia ya está {Estado}.", TipoError.Conflicto);

        Estado = EstadoIncidencia.Resuelta;
        FechaResolucion = fecha;
        return Resultado.Ok();
    }

    /// <summary>Resuelta → Cerrada. Una incidencia cerrada ya no admite cambios.</summary>
    public Resultado Cerrar()
    {
        if (Estado != EstadoIncidencia.Resuelta)
            return Resultado.Fallo($"Solo se puede cerrar una incidencia resuelta (estado actual: {Estado}).", TipoError.Conflicto);

        Estado = EstadoIncidencia.Cerrada;
        return Resultado.Ok();
    }
}
