using GestorIncidencias.Domain.Expedientes;

namespace GestorIncidencias.Application.Abstracciones;

/// <summary>
/// Caso práctico SIREI (día 5): puerto para ESCRIBIR expedientes. Sustituye al SqlDataAdapter + SqlCommandBuilder
/// de ExpedienteDetalle.aspx.cs (y a la concatenación de SQL que lo acompañaba).
/// </summary>
public interface IExpedienteRepository
{
    /// <summary>Carga el expediente CON sus trámites: la regla de cierre los necesita.</summary>
    Task<Expediente?> ObtenerPorIdAsync(int id, CancellationToken ct = default);

    Task GuardarCambiosAsync(CancellationToken ct = default);
}
