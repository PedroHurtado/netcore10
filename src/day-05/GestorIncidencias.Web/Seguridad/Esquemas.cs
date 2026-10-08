namespace GestorIncidencias.Web.Seguridad;

/// <summary>
/// Nombres de los esquemas de autenticación, como constantes para poder usarlos en atributos
/// ([Authorize(AuthenticationSchemes = ...)] no admite IdentityConstants.BearerScheme, que no es const).
///
///   - Cookie ("Identity.Application"): la usa el NAVEGADOR (MVC y Razor Pages). Va con antiforgery.
///   - Token  ("Identity.Bearer"): lo usan los CLIENTES DE LA API (.http, otra aplicación, un script).
///     El navegador no lo envía solo, así que la API no es vulnerable a CSRF y no necesita antiforgery.
/// </summary>
public static class Esquemas
{
    public const string Cookie = "Identity.Application";
    public const string Token = "Identity.Bearer";
}
