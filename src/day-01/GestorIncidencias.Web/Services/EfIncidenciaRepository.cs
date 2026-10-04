using GestorIncidencias.Web.Data;
using GestorIncidencias.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Web.Services;

/// <summary>
/// Implementación con EF Core (proveedor InMemory).
/// Se registra como Scoped porque depende del DbContext, que también es Scoped.
/// </summary>
public class EfIncidenciaRepository(IncidenciasDbContext db) : IIncidenciaRepository
{
    public async Task<IReadOnlyList<Incidencia>> ObtenerTodasAsync(CancellationToken ct = default) =>
        await db.Incidencias.AsNoTracking().OrderBy(i => i.Id).ToListAsync(ct);

    public Task<Incidencia?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        db.Incidencias.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<Incidencia> AgregarAsync(Incidencia incidencia, CancellationToken ct = default)
    {
        db.Incidencias.Add(incidencia);
        await db.SaveChangesAsync(ct); // el proveedor InMemory genera el Id
        return incidencia;
    }

    public async Task ActualizarAsync(Incidencia incidencia, CancellationToken ct = default)
    {
        db.Incidencias.Update(incidencia);
        await db.SaveChangesAsync(ct);
    }

    public Task<int> ContarAbiertasAsync(CancellationToken ct = default) =>
        db.Incidencias.CountAsync(i => i.Estado == EstadoIncidencia.Abierta || i.Estado == EstadoIncidencia.EnCurso, ct);
}
