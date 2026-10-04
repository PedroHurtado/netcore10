using GestorIncidencias.Web.Models;

namespace GestorIncidencias.Web.Services;

/// <summary>
/// Abstracción del almacenamiento. Quien la consume (IncidenciaService)
/// no sabe si los datos vienen de EF Core InMemory, de SQL Server o de una lista:
/// basta con cambiar el registro en Program.cs.
/// </summary>
public interface IIncidenciaRepository
{
    Task<IReadOnlyList<Incidencia>> ObtenerTodasAsync(CancellationToken ct = default);
    Task<Incidencia?> ObtenerPorIdAsync(int id, CancellationToken ct = default);
    Task<Incidencia> AgregarAsync(Incidencia incidencia, CancellationToken ct = default);
    Task ActualizarAsync(Incidencia incidencia, CancellationToken ct = default);
    Task<int> ContarAbiertasAsync(CancellationToken ct = default);
}
