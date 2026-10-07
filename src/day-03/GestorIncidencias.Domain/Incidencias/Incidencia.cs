using GestorIncidencias.Domain.Categorias;
using GestorIncidencias.Domain.Comun;

namespace GestorIncidencias.Domain.Incidencias;

/// <summary>
/// Entidad del dominio con comportamiento ("modelo rico") y RAÍZ DE AGREGADO:
/// los comentarios solo se añaden a través de ella.
///
/// Novedades del día 3:
///  - Categoría: relación "muchas incidencias → una categoría" (CategoriaId + navegación Categoria).
///  - Comentarios: relación "una incidencia → muchos comentarios", expuesta como colección
///    de solo lectura sobre un campo privado (_comentarios). Nadie de fuera puede hacer Comentarios.Add(...).
///  - Reabrir: el método del laboratorio 2 del día 2.
///
/// Sigue sin conocer EF Core: el mapeo está en Infrastructure/Persistencia/Configuraciones.
/// </summary>
public class Incidencia
{
    public const int TituloLongitudMinima = 5;
    public const int TituloLongitudMaxima = 120;
    public const int DescripcionLongitudMaxima = 2000;

    // Campo privado: EF Core lo rellena al cargar los comentarios (Include) y detecta los nuevos al guardar.
    private readonly List<Comentario> _comentarios = [];

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

    /// <summary>Clave ajena: null = sin categoría.</summary>
    public int? CategoriaId { get; private set; }

    /// <summary>Navegación a la categoría. Solo tiene valor si se carga (Include) o se proyecta.</summary>
    public Categoria? Categoria { get; private set; }

    public IReadOnlyCollection<Comentario> Comentarios => _comentarios.AsReadOnly();

    /// <summary>Una incidencia "cuenta" como abierta mientras no esté resuelta ni cerrada.</summary>
    public bool EstaAbierta => Estado is EstadoIncidencia.Abierta or EstadoIncidencia.EnCurso;

    /// <summary>
    /// Método de factoría: única forma de crear una incidencia válida.
    /// Que la categoría EXISTA no lo puede saber el dominio (habría que consultar la base de datos):
    /// lo comprueba el caso de uso en la capa de Aplicación.
    /// </summary>
    public static Resultado<Incidencia> Crear(
        string titulo, string? descripcion, Prioridad prioridad, DateTimeOffset fechaAlta, int? categoriaId = null)
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
            FechaAlta = fechaAlta,
            CategoriaId = categoriaId
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

    /// <summary>Resuelta → Abierta. Se borra la fecha de resolución.</summary>
    public Resultado Reabrir()
    {
        if (Estado != EstadoIncidencia.Resuelta)
            return Resultado.Fallo($"Solo se puede reabrir una incidencia resuelta (estado actual: {Estado}).", TipoError.Conflicto);

        Estado = EstadoIncidencia.Abierta;
        FechaResolucion = null;
        return Resultado.Ok();
    }

    /// <summary>
    /// Añade un comentario. La regla vive aquí, en la raíz del agregado:
    /// una incidencia cerrada no admite comentarios.
    /// </summary>
    public Resultado<Comentario> AgregarComentario(string texto, string autor, DateTimeOffset fecha)
    {
        if (Estado == EstadoIncidencia.Cerrada)
            return Resultado<Comentario>.Fallo("No se pueden añadir comentarios a una incidencia cerrada.", TipoError.Conflicto);

        texto = (texto ?? string.Empty).Trim();
        autor = (autor ?? string.Empty).Trim();

        if (texto.Length is 0 or > Comentario.TextoLongitudMaxima)
            return Resultado<Comentario>.Fallo($"El comentario debe tener entre 1 y {Comentario.TextoLongitudMaxima} caracteres.");

        if (autor.Length is 0 or > Comentario.AutorLongitudMaxima)
            return Resultado<Comentario>.Fallo($"El autor debe tener entre 1 y {Comentario.AutorLongitudMaxima} caracteres.");

        var comentario = new Comentario(texto, autor, fecha);
        _comentarios.Add(comentario);
        return Resultado<Comentario>.Ok(comentario);
    }
}
