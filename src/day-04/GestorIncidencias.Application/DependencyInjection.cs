using GestorIncidencias.Application.Incidencias;
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
        return services;
    }
}
