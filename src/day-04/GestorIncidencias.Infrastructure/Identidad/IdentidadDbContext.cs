using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Infrastructure.Identidad;

/// <summary>
/// Contexto de EF Core de ASP.NET Core Identity. IdentityDbContext ya trae todas las tablas:
///   AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetUserLogins, AspNetUserTokens...
///
/// Es un contexto SEPARADO de IncidenciasDbContext a propósito:
///   - Los usuarios no son parte del modelo de incidencias (el dominio no los conoce).
///   - Con SQL Server cada contexto tiene sus propias migraciones: se puede actualizar Identity sin tocar el resto.
///   - Si mañana los usuarios vienen de Entra ID (OIDC), este contexto desaparece y el resto no cambia.
///
/// Con InMemory, como el resto de datos, se vacía en cada arranque.
/// </summary>
public class IdentidadDbContext(DbContextOptions<IdentidadDbContext> options)
    : IdentityDbContext<IdentityUser, IdentityRole, string>(options);
