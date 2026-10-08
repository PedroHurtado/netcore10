using System.Security.Claims;
using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Seguridad;

namespace GestorIncidencias.Web.Seguridad;

/// <summary>
/// "Adaptador" de IUsuarioActual para la web: lee el usuario de la petición en curso (HttpContext.User),
/// que ha rellenado el middleware de autenticación a partir de la cookie o del token.
///
/// Es el ÚNICO sitio de la solución que usa IHttpContextAccessor. Equivale al HttpContext.Current de
/// Web Forms, pero encerrado aquí: la capa de Aplicación solo ve la interfaz.
/// </summary>
public class UsuarioActualHttp(IHttpContextAccessor accesor) : IUsuarioActual
{
    private ClaimsPrincipal? Usuario => accesor.HttpContext?.User;

    public bool EstaAutenticado => Usuario?.Identity?.IsAuthenticated == true;

    public string Nombre =>
        Usuario?.FindFirstValue(TiposClaim.NombreCompleto)
        ?? Usuario?.Identity?.Name
        ?? "(anónimo)";
}
