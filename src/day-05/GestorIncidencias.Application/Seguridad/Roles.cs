namespace GestorIncidencias.Application.Seguridad;

/// <summary>
/// Roles de la aplicación. Son un concepto de NEGOCIO ("los técnicos gestionan incidencias"),
/// por eso están en Aplicación y no en Web ni en Infrastructure: los usan los atributos [Authorize]
/// (Web) y el inicializador de usuarios de demostración (Infrastructure).
///
/// Constantes y no enum: [Authorize(Roles = ...)] necesita un string constante.
/// </summary>
public static class Roles
{
    public const string Tecnico = "Tecnico";
    public const string Administrador = "Administrador";
}

/// <summary>Tipos de claim propios de la aplicación.</summary>
public static class TiposClaim
{
    /// <summary>Nombre y apellidos para mostrar ("Ana García"). El nombre de usuario es el correo.</summary>
    public const string NombreCompleto = "nombre_completo";
}
