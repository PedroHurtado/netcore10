using GestorIncidencias.Domain.Incidencias;

namespace GestorIncidencias.Application.Incidencias;

/// <summary>
/// Datos que la capa de Aplicación devuelve hacia fuera.
/// La capa Web nunca recibe la entidad: así no puede saltarse las reglas del dominio.
///
/// Día 3: ya no se construye desde la entidad (IncidenciaDto.Desde), sino con una PROYECCIÓN
/// en la consulta (EfIncidenciaConsultas): la base de datos devuelve directamente estas columnas,
/// incluido el nombre de la categoría (JOIN) y el número de comentarios (COUNT).
/// </summary>
public record IncidenciaDto(
    int Id,
    string Titulo,
    string? Descripcion,
    Prioridad Prioridad,
    EstadoIncidencia Estado,
    DateTimeOffset FechaAlta,
    DateTimeOffset? FechaResolucion,
    int? CategoriaId,
    string? Categoria,
    int NumeroComentarios);

public record ComentarioDto(int Id, string Texto, string Autor, DateTimeOffset Fecha);

/// <summary>Ficha completa: la incidencia y sus comentarios.</summary>
public record IncidenciaDetalleDto(IncidenciaDto Incidencia, IReadOnlyList<ComentarioDto> Comentarios);

public record CategoriaDto(int Id, string Nombre);

/// <summary>Cifras del panel de inicio. Todas se calculan en la base de datos (GROUP BY), sin cargar incidencias.</summary>
public record ResumenIncidenciasDto(
    int Total,
    int Abiertas,
    int EnCurso,
    int CriticasPendientes,
    IReadOnlyList<ResumenCategoriaDto> PorCategoria);

public record ResumenCategoriaDto(string Categoria, int Total, int Pendientes);

/// <summary>Una fila del informe "incidencias por prioridad" (laboratorio 2 del día 3).</summary>
public record ResumenPrioridadDto(Prioridad Prioridad, int Total, int Pendientes);

/// <summary>
/// Datos de entrada del caso de uso "crear incidencia".
/// No lleva atributos de validación de interfaz: eso es cosa de la capa Web.
/// Las reglas de verdad (longitud del título...) están en la entidad.
/// </summary>
public record CrearIncidenciaComando(string Titulo, string? Descripcion, Prioridad? Prioridad, int? CategoriaId = null);

/// <summary>
/// Día 4: ya no lleva Autor. El autor es el usuario autenticado y lo pone el caso de uso (IUsuarioActual):
/// si viniera en el comando, cualquiera podría firmar un comentario con el nombre de otro.
/// </summary>
public record ComentarIncidenciaComando(string Texto);
