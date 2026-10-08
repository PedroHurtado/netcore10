using GestorIncidencias.Domain.Lotes;

namespace GestorIncidencias.Application.Abstracciones;

/// <summary>Caso práctico Noticom (día 5): puerto para ESCRIBIR lotes y remesas.</summary>
public interface ILoteRepository
{
    Task<Lote?> ObtenerPorIdAsync(int id, CancellationToken ct = default);

    /// <summary>Los lotes con esos Id (los que no existan, simplemente no vienen).</summary>
    Task<IReadOnlyList<Lote>> ObtenerVariosAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    void Borrar(Lote lote);
    void AgregarRemesa(Remesa remesa);

    /// <summary>Guarda todo lo pendiente en UNA transacción (la remesa nueva y el cambio de estado de sus lotes).</summary>
    Task GuardarCambiosAsync(CancellationToken ct = default);
}
