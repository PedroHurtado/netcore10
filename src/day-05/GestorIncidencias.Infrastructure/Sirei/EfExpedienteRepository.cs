using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Domain.Expedientes;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Infrastructure.Sirei;

/// <summary>
/// Caso práctico SIREI (día 5): adaptador para ESCRIBIR expedientes.
/// Sustituye a: new SqlDataAdapter("SELECT * FROM Expedientes WHERE Id = " + id, cn) + SqlCommandBuilder + da.Update(ds).
/// </summary>
public class EfExpedienteRepository(SireiDbContext db) : IExpedienteRepository
{
    // Include: la regla "no cerrar con trámites pendientes" necesita los trámites. Si se olvida el Include,
    // la colección llega VACÍA y la regla deja pasar el cierre (ver el lab de resolución de problemas).
    public Task<Expediente?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        db.Expedientes
            .Include(e => e.Tramites)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task GuardarCambiosAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
