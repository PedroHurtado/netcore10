using GestorIncidencias.Domain.Categorias;
using GestorIncidencias.Domain.Incidencias;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Infrastructure.Persistencia;

/// <summary>
/// Contexto de EF Core. Representa una "sesión" con la base de datos:
///   - Unidad de trabajo: acumula los cambios y los guarda todos juntos en SaveChanges (una transacción).
///   - Mapa de identidad: dentro del mismo contexto, la incidencia 3 es siempre el mismo objeto.
/// Se registra como Scoped: un contexto por petición HTTP.
/// </summary>
public class IncidenciasDbContext(DbContextOptions<IncidenciasDbContext> options) : DbContext(options)
{
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();
    public DbSet<Categoria> Categorias => Set<Categoria>();

    // No hay DbSet<Comentario>: los comentarios se leen y se guardan a través de su incidencia (agregado).

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        // Aplica todas las clases IEntityTypeConfiguration<T> de este ensamblado (carpeta Configuraciones).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IncidenciasDbContext).Assembly);
}
