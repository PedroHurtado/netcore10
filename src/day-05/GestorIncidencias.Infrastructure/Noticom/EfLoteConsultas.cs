using System.Linq.Expressions;
using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Lotes;
using GestorIncidencias.Domain.Lotes;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Infrastructure.Noticom;

/// <summary>
/// Caso práctico Noticom (día 5): adaptador para LEER lotes y remesas.
///
/// El LoteService de MVC 5 hacía db.Lotes.ToList() y DESPUÉS filtraba con LINQ en memoria
/// (con EF6 y un IEnumerable, el WHERE no llegaba a SQL). Aquí todos los filtros se añaden al IQueryable.
/// </summary>
public class EfLoteConsultas(NoticomDbContext db) : ILoteConsultas
{
    public async Task<Pagina<LoteDto>> ListarAsync(FiltroLotes filtro, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        IQueryable<Lote> consulta = db.Lotes;
        if (filtro.Proceso is not null)
            consulta = consulta.Where(l => l.Proceso == filtro.Proceso);
        if (filtro.Ejercicio is not null)
            consulta = consulta.Where(l => l.Ejercicio == filtro.Ejercicio);
        if (filtro.Estado is not null)
            consulta = consulta.Where(l => l.Estado == filtro.Estado);
        if (filtro.Tipo is not null)
            consulta = consulta.Where(l => l.Tipo == filtro.Tipo);
        if (filtro.Remesa is not null)
            consulta = consulta.Where(l => l.Remesa != null && l.Remesa.Nombre.Contains(filtro.Remesa));

        var total = await consulta.CountAsync(ct);

        var elementos = await consulta
            .OrderByDescending(l => l.Ejercicio).ThenBy(l => l.Proceso).ThenBy(l => l.Id)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(l => new LoteDto(
                l.Id, l.Proceso, l.Ejercicio, l.Descripcion, l.Tipo, l.NumeroNotificaciones, l.Estado,
                l.FechaAlta, l.FechaValidacion, l.Remesa != null ? l.Remesa.Nombre : null))
            .ToListAsync(ct);

        return new Pagina<LoteDto>(elementos, pagina, tamanoPagina, total);
    }

    public Task<RemesaDto?> ObtenerRemesaAsync(int id, CancellationToken ct = default) =>
        db.Remesas.Where(r => r.Id == id).Select(ARemesaDto()).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<RemesaDto>> ListarRemesasAsync(CancellationToken ct = default) =>
        await db.Remesas.OrderByDescending(r => r.FechaCreacion).ThenByDescending(r => r.Id).Select(ARemesaDto()).ToListAsync(ct);

    private static Expression<Func<Remesa, RemesaDto>> ARemesaDto() => r => new RemesaDto(
        r.Id, r.Nombre, r.FechaCreacion, r.Lotes.Count, r.Lotes.Sum(l => l.NumeroNotificaciones));
}
