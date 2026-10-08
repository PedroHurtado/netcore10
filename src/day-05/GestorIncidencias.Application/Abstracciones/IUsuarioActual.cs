namespace GestorIncidencias.Application.Abstracciones;

/// <summary>
/// "Puerto" para saber QUIÉN hace la petición, sin depender de ASP.NET Core.
///
/// En Web Forms se escribía HttpContext.Current.User.Identity.Name en cualquier sitio (también en la capa
/// de negocio). Aquí la capa de Aplicación solo conoce esta interfaz; la implementación (Web/Seguridad/
/// UsuarioActualHttp.cs) lee el usuario de la petición. En las pruebas se sustituye por un doble.
/// </summary>
public interface IUsuarioActual
{
    /// <summary>¿Hay un usuario autenticado?</summary>
    bool EstaAutenticado { get; }

    /// <summary>Nombre para mostrar (claim "nombre_completo" o, si no lo tiene, el nombre de usuario).</summary>
    string Nombre { get; }
}
