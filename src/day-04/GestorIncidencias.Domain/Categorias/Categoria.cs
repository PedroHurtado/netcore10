namespace GestorIncidencias.Domain.Categorias;

/// <summary>
/// Catálogo de categorías (Hardware, Software, Redes...).
/// Es una entidad sencilla: casi no tiene reglas, solo un nombre.
/// Las categorías iniciales se cargan con la migración (HasData en CategoriaConfiguracion).
/// </summary>
public class Categoria
{
    public const int NombreLongitudMaxima = 60;

    private Categoria() { }   // para EF Core

    public Categoria(string nombre) => Nombre = nombre.Trim();

    public int Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
}
