using System.Security.Claims;
using GestorIncidencias.Application.Configuracion;
using GestorIncidencias.Application.Seguridad;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestorIncidencias.Infrastructure.Identidad;

/// <summary>
/// Se ejecuta al arrancar (IHostedService), como InicializadorBaseDatos:
///   1. Crea los ROLES (Tecnico, Administrador). Son datos de referencia: hacen falta en todos los entornos.
///   2. Si Incidencias:CargarDatosDemo = true, crea tres USUARIOS de demostración con la contraseña
///      de Incidencias:ClaveUsuariosDemo (solo en appsettings.Development.json).
///
/// Todo se hace con UserManager y RoleManager, nunca escribiendo en las tablas a mano:
/// así la contraseña se guarda con el hash de Identity y se aplican sus validaciones.
/// </summary>
public class InicializadorIdentidad(
    IServiceScopeFactory scopeFactory,
    IOptions<IncidenciasOptions> opciones,
    ILogger<InicializadorIdentidad> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var gestorRoles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var gestorUsuarios = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        foreach (var rol in new[] { Roles.Tecnico, Roles.Administrador })
        {
            if (!await gestorRoles.RoleExistsAsync(rol))
                await gestorRoles.CreateAsync(new IdentityRole(rol));
        }

        if (!opciones.Value.CargarDatosDemo)
            return;

        var clave = opciones.Value.ClaveUsuariosDemo;
        if (string.IsNullOrWhiteSpace(clave))
        {
            logger.LogWarning("CargarDatosDemo está activo pero falta Incidencias:ClaveUsuariosDemo: no se crean usuarios de demostración");
            return;
        }

        await CrearUsuarioAsync(gestorUsuarios, "ana@demo.local", "Ana García", clave, Roles.Tecnico);
        await CrearUsuarioAsync(gestorUsuarios, "luis@demo.local", "Luis Pérez", clave);                 // sin rol: usuario normal
        await CrearUsuarioAsync(gestorUsuarios, "admin@demo.local", "Marta Ruiz", clave, Roles.Administrador);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CrearUsuarioAsync(
        UserManager<IdentityUser> gestor, string correo, string nombreCompleto, string clave, params string[] roles)
    {
        if (await gestor.FindByNameAsync(correo) is not null)
            return;   // ya existe (por ejemplo, en las pruebas de integración, que arrancan la aplicación varias veces)

        var usuario = new IdentityUser { UserName = correo, Email = correo, EmailConfirmed = true };

        // CreateAsync valida la contraseña con las reglas de IdentityOptions.Password y guarda su HASH (nunca la contraseña).
        var resultado = await gestor.CreateAsync(usuario, clave);
        if (!resultado.Succeeded)
            throw new InvalidOperationException(
                $"No se pudo crear el usuario {correo}: {string.Join(" ", resultado.Errors.Select(e => e.Description))}");

        // Los claims del usuario (tabla AspNetUserClaims) se copian a la cookie al iniciar sesión.
        await gestor.AddClaimAsync(usuario, new Claim(TiposClaim.NombreCompleto, nombreCompleto));

        if (roles.Length > 0)
            await gestor.AddToRolesAsync(usuario, roles);

        logger.LogInformation("Usuario de demostración {Usuario} creado con roles {Roles}", correo, roles);
    }
}
