using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Domain.Incidencias;
using GestorIncidencias.Web.Estado;

namespace GestorIncidencias.Web.Models;

/// <summary>Datos que necesita la vista Views/Home/Index.cshtml.</summary>
public record PanelViewModel(
    string NombreAplicacion,
    ResumenIncidenciasDto Resumen,
    IReadOnlyList<ResumenPrioridadDto> PorPrioridad);

/// <summary>Datos que necesita la vista Views/Incidencias/Index.cshtml.</summary>
public record ListadoIncidenciasViewModel(
    Pagina<IncidenciaDto> Pagina,
    EstadoIncidencia? FiltroEstado,
    IReadOnlyList<VisitaReciente> VistasRecientemente);

/// <summary>Una fila de Views/Usuarios/Index.cshtml.</summary>
public record UsuarioViewModel(
    string Usuario,
    string? NombreCompleto,
    IList<string> Roles,
    int IntentosFallidos,
    bool Bloqueado);

/// <summary>Datos de la vista Views/Shared/Error.cshtml.</summary>
public record ErrorViewModel(string? RequestId);
