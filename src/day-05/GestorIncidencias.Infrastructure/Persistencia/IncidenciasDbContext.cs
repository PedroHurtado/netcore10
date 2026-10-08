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
        // Aplica las clases IEntityTypeConfiguration<T> de la carpeta Persistencia/Configuraciones.
        // Día 5: el filtro por espacio de nombres hace falta porque el ensamblado tiene ya las configuraciones
        // de OTROS contextos (Sirei, Noticom); sin él, este contexto intentaría mapear también Expediente y Lote.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(IncidenciasDbContext).Assembly,
            tipo => tipo.Namespace == typeof(Configuraciones.IncidenciaConfiguracion).Namespace);
}
