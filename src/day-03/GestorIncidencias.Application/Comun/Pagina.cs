namespace GestorIncidencias.Application.Comun;

/// <summary>
/// Una página de resultados. Nunca devolvemos "todas las filas": con 50 incidencias da igual,
/// con 500.000 (lo normal en una aplicación legacy con años de datos) la página no carga.
/// </summary>
public record Pagina<T>(IReadOnlyList<T> Elementos, int NumeroPagina, int TamanoPagina, int TotalElementos)
{
    public const int TamanoPorDefecto = 20;
    public const int TamanoMaximo = 100;

    public int TotalPaginas => TotalElementos == 0 ? 1 : (int)Math.Ceiling(TotalElementos / (double)TamanoPagina);
    public bool HayAnterior => NumeroPagina > 1;
    public bool HaySiguiente => NumeroPagina < TotalPaginas;

    /// <summary>Corrige valores absurdos que lleguen de la URL (?pagina=-3&amp;tamano=100000).</summary>
    public static (int Pagina, int Tamano) Normalizar(int pagina, int tamano) =>
        (Math.Max(1, pagina), Math.Clamp(tamano, 1, TamanoMaximo));
}
