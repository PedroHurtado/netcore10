using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Domain.Incidencias;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Infrastructure.Persistencia;

/// <summary>
/// "Adaptador" de salida: implementa el puerto IIncidenciaRepository con EF Core.
/// Si mañana cambiamos a SQL Server o a Dapper, solo cambia esta clase.
/// </summary>
public class EfIncidenciaRepository(IncidenciasDbContext db) : IIncidenciaRepository
{
    public async Task<IReadOnlyList<Incidencia>> ObtenerTodasAsync(EstadoIncidencia? estado = null, CancellationToken ct = default)
    {
        var consulta = db.Incidencias.AsNoTracking();
        if (estado is not null)
            consulta = consulta.Where(i => i.Estado == estado);

        return await consulta.OrderByDescending(i => i.Prioridad).ThenBy(i => i.Id).ToListAsync(ct);
    }

    public Task<Incidencia?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        db.Incidencias.FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<int> ContarAbiertasAsync(CancellationToken ct = default) =>
        db.Incidencias.CountAsync(i => i.Estado == EstadoIncidencia.Abierta || i.Estado == EstadoIncidencia.EnCurso, ct);

    public async Task AgregarAsync(Incidencia incidencia, CancellationToken ct = default)
    {
        db.Incidencias.Add(incidencia);
        await db.SaveChangesAsync(ct); // el proveedor InMemory genera el Id
    }

    // La entidad se obtuvo con seguimiento (tracking): EF Core detecta solo los cambios.
    public Task GuardarCambiosAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
