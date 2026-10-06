using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestorIncidencias.Infrastructure;

/// <summary>
/// Registro de los servicios de infraestructura. Aquí se "enchufa" cada adaptador a su puerto.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<IncidenciasDbContext>(opt => opt.UseInMemoryDatabase("GestorIncidencias"));
        services.AddScoped<IIncidenciaRepository, EfIncidenciaRepository>();
        services.AddHostedService<DatosDemoInitializer>();
        return services;
    }
}
