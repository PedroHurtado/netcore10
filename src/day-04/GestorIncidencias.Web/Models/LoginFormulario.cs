using System.ComponentModel.DataAnnotations;

namespace GestorIncidencias.Web.Models;

/// <summary>Formulario de inicio de sesión (Views/Cuenta/Login.cshtml).</summary>
public class LoginFormulario
{
    [Display(Name = "Correo")]
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
    public string Correo { get; set; } = string.Empty;

    [Display(Name = "Contraseña")]
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [DataType(DataType.Password)]   // el Tag Helper genera type="password" y NUNCA devuelve el valor al volver a pintar
    public string Clave { get; set; } = string.Empty;

    [Display(Name = "Mantener la sesión iniciada")]
    public bool Recordarme { get; set; }

    /// <summary>A dónde volver tras iniciar sesión (lo añade el middleware al redirigir: /Cuenta/Login?ReturnUrl=...).</summary>
    public string? ReturnUrl { get; set; }
}
