using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Infrastructure.Identidad;
using GestorIncidencias.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Identity;
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
        // Con SQL Server sería: opt.UseSqlServer(configuracion.GetConnectionString("GestorIncidencias"))
        services.AddDbContext<IncidenciasDbContext>(opt => opt.UseInMemoryDatabase("GestorIncidencias"));

        services.AddScoped<IIncidenciaRepository, EfIncidenciaRepository>();   // escribir
        services.AddScoped<IIncidenciaConsultas, EfIncidenciaConsultas>();     // leer
        services.AddHostedService<InicializadorBaseDatos>();

        // Caché en dos niveles: L1 en memoria de este servidor y, si se registra un IDistributedCache
        // (Redis, SQL Server...), L2 compartida entre servidores. Sin L2 funciona solo con L1.
        services.AddHybridCache();

        // Comprobación de salud: ¿responde la base de datos? (se publica en /salud, ver Program.cs)
        services.AddHealthChecks().AddDbContextCheck<IncidenciasDbContext>("base-de-datos");

        services.AddIdentidad();
        return services;
    }

    /// <summary>
    /// ASP.NET Core Identity: usuarios, contraseñas (con hash), roles, claims y bloqueo por intentos fallidos.
    /// Las reglas de contraseña y bloqueo son una decisión de SEGURIDAD de la organización: aquí se ven todas juntas.
    /// Cómo se identifica el usuario en la web (cookie, ruta del login...) se configura en Program.cs.
    /// </summary>
    private static void AddIdentidad(this IServiceCollection services)
    {
        services.AddDbContext<IdentidadDbContext>(opt => opt.UseInMemoryDatabase("GestorIncidenciasIdentidad"));

        // AddIdentity registra UserManager, RoleManager, SignInManager y la autenticación por cookie
        // ("Identity.Application"), que pasa a ser el esquema por defecto.
        services.AddIdentity<IdentityUser, IdentityRole>(o =>
            {
                o.Password.RequiredLength = 10;
                o.Password.RequireNonAlphanumeric = true;
                o.Password.RequireDigit = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireLowercase = true;

                o.Lockout.MaxFailedAccessAttempts = 5;                       // 5 fallos seguidos...
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);  // ...bloquean la cuenta 5 minutos
                o.Lockout.AllowedForNewUsers = true;

                o.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<IdentidadDbContext>()   // dónde se guardan usuarios y roles
            .AddDefaultTokenProviders();                      // tokens para restablecer contraseña, confirmar correo...

        services.AddHostedService<InicializadorIdentidad>();
    }
}
