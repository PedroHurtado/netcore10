using System.Linq.Expressions;
using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Domain.Incidencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace GestorIncidencias.Infrastructure.Persistencia;

/// <summary>
/// "Adaptador" de salida para LEER: implementa IIncidenciaConsultas con EF Core.
///
/// Reglas que sigue (capítulo 6, rendimiento):
///   1. PROYECCIÓN con Select: se piden solo las columnas del DTO, no la entidad entera.
///      Una proyección a DTO no se sigue (no hay entidades que seguir), así que AsNoTracking no hace falta.
///   2. Los recuentos y agrupaciones se hacen en SQL (COUNT, GROUP BY), no en memoria.
///   3. Siempre paginado: Skip/Take con un orden estable.
/// Con InMemory no hay SQL, pero el código es el mismo que con SQL Server: ahí sí se traduciría así.
///
/// Día 4: el catálogo de categorías se guarda en caché (HybridCache): cambia casi nunca y se pide
/// en cada formulario. Es el equivalente moderno de Cache["Categorias"] en Web Forms.
/// </summary>
public class EfIncidenciaConsultas(IncidenciasDbContext db, HybridCache cache) : IIncidenciaConsultas
{
    private const string ClaveCacheCategorias = "categorias";

    private static readonly HybridCacheEntryOptions OpcionesCacheCategorias = new()
    {
        Expiration = TimeSpan.FromMinutes(10),            // caché distribuida (L2), si la hubiera
        LocalCacheExpiration = TimeSpan.FromMinutes(10)   // memoria de este servidor (L1)
    };

    /// <summary>
    /// Proyección reutilizable. Es una EXPRESIÓN (no un método): EF Core la lee y, con un proveedor relacional, la traduce a SQL:
    ///   SELECT i.Id, i.Titulo, ..., c.Nombre, (SELECT COUNT(*) FROM Comentarios WHERE IncidenciaId = i.Id)
    ///   FROM Incidencias i LEFT JOIN Categorias c ON ...
    /// Si fuera un método normal (IncidenciaDto.Desde(i)), EF Core tendría que traer la entidad completa.
    /// </summary>
    private static readonly Expression<Func<Incidencia, IncidenciaDto>> ADto = i => new IncidenciaDto(
        i.Id,
        i.Titulo,
        i.Descripcion,
        i.Prioridad,
        i.Estado,
        i.FechaAlta,
        i.FechaResolucion,
        i.CategoriaId,
        i.Categoria != null ? i.Categoria.Nombre : null,
        i.Comentarios.Count);

    public async Task<Pagina<IncidenciaDto>> ListarAsync(
        EstadoIncidencia? estado, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        // IQueryable: todavía no se ha ejecutado nada; solo se está construyendo la consulta.
        IQueryable<Incidencia> consulta = db.Incidencias;
        if (estado is not null)
            consulta = consulta.Where(i => i.Estado == estado);

        var total = await consulta.CountAsync(ct);                       // 1ª consulta: SELECT COUNT(*)

        var elementos = await consulta                                   // 2ª consulta: la página
            .OrderByDescending(i => i.Prioridad)
            .ThenBy(i => i.Id)                                           // desempate: orden ESTABLE entre páginas
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(ADto)
            .ToListAsync(ct);

        return new Pagina<IncidenciaDto>(elementos, pagina, tamanoPagina, total);
    }

    public Task<IncidenciaDto?> ObtenerAsync(int id, CancellationToken ct = default) =>
        db.Incidencias.Where(i => i.Id == id).Select(ADto).FirstOrDefaultAsync(ct);

    public async Task<IncidenciaDetalleDto?> ObtenerDetalleAsync(int id, CancellationToken ct = default)
    {
        var incidencia = await ObtenerAsync(id, ct);
        if (incidencia is null)
            return null;

        // Comentario no tiene propiedad IncidenciaId (es sombra): se consulta con EF.Property.
        var comentarios = await db.Set<Comentario>()
            .Where(c => EF.Property<int>(c, "IncidenciaId") == id)
            .OrderBy(c => c.Fecha)
            .ThenBy(c => c.Id)
            .Select(c => new ComentarioDto(c.Id, c.Texto, c.Autor, c.Fecha))
            .ToListAsync(ct);

        return new IncidenciaDetalleDto(incidencia, comentarios);
    }

    public async Task<ResumenIncidenciasDto> ObtenerResumenAsync(CancellationToken ct = default)
    {
        // SELECT Estado, COUNT(*) FROM Incidencias GROUP BY Estado
        var porEstado = await db.Incidencias
            .GroupBy(i => i.Estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Estado, x => x.Total, ct);

        var criticasPendientes = await db.Incidencias.CountAsync(i =>
            i.Prioridad == Prioridad.Critica &&
            (i.Estado == EstadoIncidencia.Abierta || i.Estado == EstadoIncidencia.EnCurso), ct);

        // SELECT c.Nombre, COUNT(*), COUNT(CASE WHEN ... THEN 1 END)
        // FROM Incidencias i LEFT JOIN Categorias c ON ... GROUP BY c.Nombre
        var porCategoria = await db.Incidencias
            .GroupBy(i => i.Categoria != null ? i.Categoria.Nombre : null)
            .Select(g => new
            {
                Categoria = g.Key,
                Total = g.Count(),
                Pendientes = g.Count(i => i.Estado == EstadoIncidencia.Abierta || i.Estado == EstadoIncidencia.EnCurso)
            })
            .OrderByDescending(x => x.Pendientes)
            .ThenByDescending(x => x.Total)
            .ToListAsync(ct);

        return new ResumenIncidenciasDto(
            Total: porEstado.Values.Sum(),
            Abiertas: porEstado.GetValueOrDefault(EstadoIncidencia.Abierta),
            EnCurso: porEstado.GetValueOrDefault(EstadoIncidencia.EnCurso),
            CriticasPendientes: criticasPendientes,
            PorCategoria: porCategoria
                .Select(x => new ResumenCategoriaDto(x.Categoria ?? "(sin categoría)", x.Total, x.Pendientes))
                .ToList());
    }

    public async Task<IReadOnlyList<ResumenPrioridadDto>> ResumenPorPrioridadAsync(CancellationToken ct = default) =>
        await db.Incidencias
            .GroupBy(i => i.Prioridad)
            .OrderByDescending(g => g.Key)                // ordenar ANTES de proyectar (laboratorio 2 del día 3)
            .Select(g => new ResumenPrioridadDto(
                g.Key,
                g.Count(),
                g.Count(i => i.Estado == EstadoIncidencia.Abierta || i.Estado == EstadoIncidencia.EnCurso)))
            .ToListAsync(ct);

    /// <summary>
    /// GetOrCreateAsync: si está en caché, la devuelve; si no, ejecuta la consulta y la guarda.
    /// Si llegan 50 peticiones a la vez con la caché vacía, solo UNA va a la base de datos (las demás esperan).
    /// </summary>
    public async Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(
            ClaveCacheCategorias,
            async token => await db.Categorias
                .OrderBy(c => c.Nombre)
                .Select(c => new CategoriaDto(c.Id, c.Nombre))
                .ToListAsync(token),
            OpcionesCacheCategorias,
            cancellationToken: ct);
}
