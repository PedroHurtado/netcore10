using GestorIncidencias.Application.Expedientes;
using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Application.Lotes;
using Microsoft.Extensions.DependencyInjection;

namespace GestorIncidencias.Application;

/// <summary>
/// Cada capa registra sus propios servicios. Program.cs solo llama a AddApplication().
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IIncidenciaService, IncidenciaService>();

        // Día 5 — caso práctico: los casos de uso de las dos aplicaciones migradas.
        services.AddScoped<IExpedienteService, ExpedienteService>();   // SIREI (Web Forms)
        services.AddScoped<ILoteService, LoteService>();               // Noticom (MVC 5)
        return services;
    }
}
