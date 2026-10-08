using GestorIncidencias.Application.Seguridad;
using GestorIncidencias.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Web.Controllers;

/// <summary>
/// Administración (solo lectura) de los usuarios de Identity: quién hay, qué roles tiene y si está bloqueado.
///
/// [Authorize(Roles = ...)]: autorización por ROL. Un usuario autenticado sin el rol Administrador
/// recibe un 403, que la cookie convierte en una redirección a /Cuenta/AccesoDenegado.
///
/// Usa UserManager directamente: es la API de Identity para gestionar usuarios. Para una pantalla de
/// administración tan sencilla no compensa esconderlo detrás de una interfaz propia.
/// </summary>
[Authorize(Roles = Roles.Administrador)]
public class UsuariosController(UserManager<IdentityUser> gestorUsuarios) : Controller
{
    // GET /Usuarios
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // UserManager.Users es un IQueryable de EF Core (tabla AspNetUsers).
        var usuarios = await gestorUsuarios.Users.OrderBy(u => u.UserName).ToListAsync(ct);

        // OJO: 3 consultas por usuario (claims, roles, bloqueo) → un N+1 (día 3, capítulo 6).
        // Con 3 usuarios de demostración da igual; con miles, se haría una sola consulta en Infrastructure.
        var filas = new List<UsuarioViewModel>();
        foreach (var usuario in usuarios)
        {
            var claims = await gestorUsuarios.GetClaimsAsync(usuario);
            filas.Add(new UsuarioViewModel(
                usuario.UserName!,
                claims.FirstOrDefault(c => c.Type == TiposClaim.NombreCompleto)?.Value,
                await gestorUsuarios.GetRolesAsync(usuario),
                usuario.AccessFailedCount,
                await gestorUsuarios.IsLockedOutAsync(usuario)));
        }

        return View(filas);
    }
}
