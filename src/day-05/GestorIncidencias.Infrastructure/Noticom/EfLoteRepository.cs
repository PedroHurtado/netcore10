using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Domain.Lotes;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Infrastructure.Noticom;

/// <summary>Caso práctico Noticom (día 5): adaptador para ESCRIBIR lotes y remesas.</summary>
public class EfLoteRepository(NoticomDbContext db) : ILoteRepository
{
    public Task<Lote?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        db.Lotes.FirstOrDefaultAsync(l => l.Id == id, ct);

    // WHERE Id IN (@p0, @p1, ...): UNA consulta para todos los lotes marcados.
    // El legacy hacía un db.Lotes.Find(id) por cada lote dentro de un foreach (N consultas).
    public async Task<IReadOnlyList<Lote>> ObtenerVariosAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        await db.Lotes.Where(l => ids.Contains(l.Id)).ToListAsync(ct);

    public void Borrar(Lote lote) => db.Lotes.Remove(lote);

    public void AgregarRemesa(Remesa remesa) => db.Remesas.Add(remesa);

    public Task GuardarCambiosAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
