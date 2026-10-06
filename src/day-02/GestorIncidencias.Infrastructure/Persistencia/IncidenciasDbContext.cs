using GestorIncidencias.Domain.Incidencias;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Infrastructure.Persistencia;

/// <summary>
/// Contexto de EF Core. Ahora vive en Infraestructura: el Dominio y la Aplicación no lo ven.
/// </summary>
public class IncidenciasDbContext(DbContextOptions<IncidenciasDbContext> options) : DbContext(options)
{
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        // Aplica todas las clases IEntityTypeConfiguration<T> de este ensamblado.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IncidenciasDbContext).Assembly);
}
