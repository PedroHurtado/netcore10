using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GestorIncidencias.Web.Controllers.Api;

/// <summary>Cuerpo JSON de POST /api/cuenta/token.</summary>
public record TokenRequest(
    [Required, EmailAddress] string Correo,
    [Required] string Clave);

/// <summary>
/// Emite un TOKEN para usar la API: POST /api/cuenta/token → { tokenType, accessToken, expiresIn, refreshToken }.
/// Después, cada petición a la API lleva la cabecera "Authorization: Bearer {accessToken}".
///
/// Usa el esquema "Identity.Bearer" de ASP.NET Core Identity: el token es OPACO (cifrado con Data Protection),
/// solo lo entiende esta aplicación. No es OAuth2 ni un JWT: para que OTRAS aplicaciones confíen en el token
/// hace falta un proveedor de identidad (Entra ID, Keycloak...) con OAuth2/OIDC (docs/day-04/05-oauth-oidc-y-legacy.md).
///
/// [IgnoreAntiforgeryToken]: la API no usa cookies, así que no hay CSRF que evitar (ver Program.cs).
/// </summary>
[ApiController]
[Route("api/cuenta")]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class CuentaApiController(SignInManager<IdentityUser> signInManager, ILogger<CuentaApiController> logger) : ControllerBase
{
    // POST /api/cuenta/token   {"correo": "ana@demo.local", "clave": "..."}
    [HttpPost("token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token(TokenRequest request)
    {
        // Mismo SignInManager que el login web, pero "iniciar sesión" con el esquema Bearer significa
        // ESCRIBIR UN TOKEN en la respuesta en lugar de emitir una cookie.
        signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;

        var resultado = await signInManager.PasswordSignInAsync(request.Correo, request.Clave, isPersistent: false, lockoutOnFailure: true);
        if (!resultado.Succeeded)
        {
            logger.LogWarning("Petición de token rechazada para {Usuario} (bloqueada: {Bloqueada})", request.Correo, resultado.IsLockedOut);
            return Problem(
                detail: resultado.IsLockedOut ? "Cuenta bloqueada temporalmente." : "Correo o contraseña incorrectos.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        logger.LogInformation("Token emitido para {Usuario}", request.Correo);

        // El manejador del esquema Bearer ya ha escrito el JSON con el token: no hay que añadir nada.
        return new EmptyResult();
    }
}
