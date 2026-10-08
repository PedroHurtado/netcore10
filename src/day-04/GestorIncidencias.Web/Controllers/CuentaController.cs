using GestorIncidencias.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GestorIncidencias.Web.Controllers;

/// <summary>
/// Inicio y cierre de sesión con ASP.NET Core Identity (cookie).
///
/// [AllowAnonymous]: la política por defecto de la aplicación exige usuario autenticado (Program.cs),
/// y a la página de login, lógicamente, hay que poder llegar sin haber iniciado sesión.
///
/// Equivale al Login.aspx con FormsAuthentication.RedirectFromLoginPage de Web Forms, o al AccountController
/// de la plantilla de MVC 5 (que ya usaba SignInManager de ASP.NET Identity 2).
/// </summary>
[AllowAnonymous]
public class CuentaController(SignInManager<IdentityUser> signInManager, ILogger<CuentaController> logger) : Controller
{
    // GET /Cuenta/Login?ReturnUrl=%2FIncidencias
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) =>
        View(new LoginFormulario { ReturnUrl = returnUrl });

    // POST /Cuenta/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginFormulario formulario)
    {
        if (!ModelState.IsValid)
            return View(formulario);

        // Comprueba la contraseña contra el HASH guardado y, si es correcta, emite la cookie de autenticación.
        // lockoutOnFailure: true → cuenta los fallos y bloquea la cuenta al llegar a MaxFailedAccessAttempts.
        var resultado = await signInManager.PasswordSignInAsync(
            formulario.Correo, formulario.Clave, formulario.Recordarme, lockoutOnFailure: true);

        if (resultado.Succeeded)
        {
            // Log de SEGURIDAD: quién entra. Nunca se escribe la contraseña.
            logger.LogInformation("Inicio de sesión correcto de {Usuario}", formulario.Correo);

            // Solo se vuelve a URLs LOCALES. Si se hiciera Redirect(ReturnUrl) sin comprobar, un enlace como
            // /Cuenta/Login?ReturnUrl=https://sitio-falso.example enviaría al usuario a otra web tras identificarse
            // ("open redirect"). LocalRedirect lanza una excepción si la URL no es local.
            return LocalRedirect(Url.IsLocalUrl(formulario.ReturnUrl) ? formulario.ReturnUrl : "/");
        }

        if (resultado.IsLockedOut)
        {
            logger.LogWarning("Cuenta bloqueada por intentos fallidos: {Usuario}", formulario.Correo);
            ModelState.AddModelError(string.Empty, "La cuenta está bloqueada temporalmente por demasiados intentos fallidos. Inténtalo dentro de unos minutos.");
        }
        else
        {
            logger.LogWarning("Inicio de sesión fallido de {Usuario}", formulario.Correo);
            // El mismo mensaje si el usuario no existe o si la contraseña es incorrecta:
            // así no se puede averiguar qué correos están dados de alta.
            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
        }

        return View(formulario);
    }

    // POST /Cuenta/Logout   ← POST y con antiforgery: un GET se podría disparar desde un <img> de otra web
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();   // borra la cookie de autenticación

        // La sesión NO va ligada a la autenticación: si no se vacía, el siguiente usuario de este navegador
        // vería el historial de visitas del anterior.
        HttpContext.Session.Clear();

        TempData["Mensaje"] = "Has cerrado la sesión.";
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    // GET /Cuenta/AccesoDenegado   (a donde redirige la cookie cuando falta un rol o una política → 403)
    [HttpGet]
    public IActionResult AccesoDenegado() => View();
}
