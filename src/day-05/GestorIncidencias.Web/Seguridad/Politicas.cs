namespace GestorIncidencias.Web.Seguridad;

/// <summary>Nombres de las políticas de autorización (se registran en Program.cs).</summary>
public static class Politicas
{
    /// <summary>Iniciar, resolver, cerrar, reabrir y cambiar la categoría: técnicos y administradores (laboratorio 2 del día 4).</summary>
    public const string GestionarIncidencias = "GestionarIncidencias";

    /// <summary>Día 5 (Noticom): validar lotes y crear remesas. Borrar lotes queda solo para el rol Administrador.</summary>
    public const string GestionarLotes = "GestionarLotes";
}
