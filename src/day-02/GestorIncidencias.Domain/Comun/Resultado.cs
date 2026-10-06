namespace GestorIncidencias.Domain.Comun;

/// <summary>
/// Tipo de error de negocio. Permite a la capa Web traducirlo al código HTTP adecuado
/// (400, 404, 409) sin que el Dominio sepa nada de HTTP.
/// </summary>
public enum TipoError
{
    Validacion,
    NoEncontrado,
    Conflicto
}

/// <summary>
/// Resultado de una operación de negocio sin valor de retorno: éxito o error.
/// Evita usar excepciones para el flujo normal ("no se puede resolver una incidencia cerrada").
/// </summary>
public record Resultado(bool Exito, string? Error, TipoError? Tipo)
{
    public static Resultado Ok() => new(true, null, null);
    public static Resultado Fallo(string error, TipoError tipo = TipoError.Validacion) => new(false, error, tipo);
}

/// <summary>
/// Resultado de una operación de negocio que, si tiene éxito, devuelve un valor.
/// </summary>
public record Resultado<T>(bool Exito, T? Valor, string? Error, TipoError? Tipo)
{
    public static Resultado<T> Ok(T valor) => new(true, valor, null, null);
    public static Resultado<T> Fallo(string error, TipoError tipo = TipoError.Validacion) => new(false, default, error, tipo);
}
