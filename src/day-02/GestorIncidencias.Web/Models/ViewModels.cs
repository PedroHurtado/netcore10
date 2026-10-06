using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Domain.Incidencias;

namespace GestorIncidencias.Web.Models;

/// <summary>Datos que necesita la vista Views/Home/Index.cshtml.</summary>
public record PanelViewModel(
    string NombreAplicacion,
    int Total,
    int Abiertas,
    int EnCurso,
    int CriticasPendientes);

/// <summary>Datos que necesita la vista Views/Incidencias/Index.cshtml.</summary>
public record ListadoIncidenciasViewModel(
    IReadOnlyList<IncidenciaDto> Incidencias,
    EstadoIncidencia? FiltroEstado);

/// <summary>Datos de la vista Views/Shared/Error.cshtml.</summary>
public record ErrorViewModel(string? RequestId);
