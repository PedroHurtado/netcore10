using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Expedientes;
using GestorIncidencias.Domain.Expedientes;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Infrastructure.Sirei;

/// <summary>
/// Caso práctico SIREI (día 5): adaptador para LEER expedientes. Las mismas reglas que EfIncidenciaConsultas:
/// proyección a DTO, recuentos en la base de datos y paginación con Skip/Take.
///
/// El legacy (BuscarExpedientes.aspx) hacía "SELECT * FROM Expedientes WHERE Titular LIKE '%" + txtTexto.Text + "%'"
/// y el GridView paginaba en memoria: inyección SQL y todas las filas en cada clic.
/// </summary>
public class EfExpedienteConsultas(SireiDbContext db) : IExpedienteConsultas
{
    public async Task<Pagina<ExpedienteDto>> ListarAsync(
        string? texto, EstadoExpediente? estado, int pagina, int tamanoPagina, CancellationToken ct = default)
    {
        IQueryable<Expediente> consulta = db.Expedientes;
        if (texto is not null)   // busca en número, titular y asunto (como el cuadro único del legacy)
            consulta = consulta.Where(e => e.Numero.Contains(texto) || e.Titular.Contains(texto) || e.Asunto.Contains(texto));
        if (estado is not null)
            consulta = consulta.Where(e => e.Estado == estado);

        var total = await consulta.CountAsync(ct);

        var elementos = await consulta
            .OrderByDescending(e => e.FechaModificacion)
            .ThenByDescending(e => e.Id)   // orden ESTABLE: sin desempate, la paginación puede repetir o saltarse filas
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(e => new ExpedienteDto(
                e.Id, e.Numero, e.Titular, e.Asunto, e.Estado, e.FechaModificacion,
                e.Tramites.Count(t => t.Pendiente)))
            .ToListAsync(ct);

        return new Pagina<ExpedienteDto>(elementos, pagina, tamanoPagina, total);
    }

    public Task<ExpedienteDetalleDto?> ObtenerDetalleAsync(int id, CancellationToken ct = default) =>
        db.Expedientes
            .Where(e => e.Id == id)
            .Select(e => new ExpedienteDetalleDto(
                e.Id, e.Numero, e.Titular, e.Asunto, e.Observaciones, e.Estado, e.FechaAlta, e.FechaModificacion, e.Version,
                e.Tramites
                    .OrderBy(t => t.FechaAlta).ThenBy(t => t.Id)
                    .Select(t => new TramiteDto(t.Id, t.Descripcion, t.FechaAlta, t.Pendiente, t.FechaCompletado))
                    .ToList()))
            .FirstOrDefaultAsync(ct);
}
