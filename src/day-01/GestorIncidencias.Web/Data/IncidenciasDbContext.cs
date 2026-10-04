using GestorIncidencias.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Web.Data;

/// <summary>
/// Contexto de EF Core. Durante el curso usamos el proveedor InMemory
/// (no necesita instalar ningún servidor de base de datos).
/// El DbContext se registra como Scoped: una instancia por petición HTTP.
/// En el día 3 profundizaremos en configuración, relaciones y consultas.
/// </summary>
public class IncidenciasDbContext(DbContextOptions<IncidenciasDbContext> options) : DbContext(options)
{
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Incidencia>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Titulo).IsRequired().HasMaxLength(120);
            e.Property(i => i.Descripcion).HasMaxLength(2000);
        });
    }
}
