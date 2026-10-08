using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Domain.Incidencias;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Infrastructure.Persistencia;

/// <summary>
/// "Adaptador" de salida para ESCRIBIR: implementa IIncidenciaRepository con EF Core.
/// Trabaja con ENTIDADES y CON seguimiento de cambios (tracking): lo que se carga aquí se modifica y se guarda.
/// Las lecturas para pantallas están en EfIncidenciaConsultas.
/// </summary>
public class EfIncidenciaRepository(IncidenciasDbContext db) : IIncidenciaRepository
{
    // Carga el AGREGADO completo: la incidencia y sus comentarios (Include → LEFT JOIN Comentarios).
    // Así la entidad puede aplicar cualquier regla que dependa de sus comentarios.
    public Task<Incidencia?> ObtenerPorIdAsync(int id, CancellationToken ct = default) =>
        db.Incidencias
            .Include(i => i.Comentarios)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    // COUNT en la base de datos: no se trae ninguna incidencia a memoria.
    public Task<int> ContarAbiertasAsync(CancellationToken ct = default) =>
        db.Incidencias.CountAsync(i => i.Estado == EstadoIncidencia.Abierta || i.Estado == EstadoIncidencia.EnCurso, ct);

    public Task<bool> ExisteCategoriaAsync(int categoriaId, CancellationToken ct = default) =>
        db.Categorias.AnyAsync(c => c.Id == categoriaId, ct);

    public async Task AgregarAsync(Incidencia incidencia, CancellationToken ct = default)
    {
        db.Incidencias.Add(incidencia);
        await db.SaveChangesAsync(ct);   // el proveedor genera el Id y EF Core lo copia a la entidad
    }

    // La entidad se obtuvo con seguimiento: EF Core compara con la foto original y genera
    // solo los UPDATE/INSERT necesarios (p. ej. UPDATE del Estado + INSERT del comentario nuevo), en UNA transacción.
    public Task GuardarCambiosAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
