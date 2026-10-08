using GestorIncidencias.Domain.Expedientes;
using GestorIncidencias.Infrastructure.Sirei.Configuraciones;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Infrastructure.Sirei;

/// <summary>
/// Caso práctico SIREI (día 5): contexto de EF Core sobre la base de datos de SIREI.
///
/// Es un contexto APARTE del de incidencias porque es OTRA base de datos: la que ya existe y sigue usando
/// la aplicación Web Forms mientras conviven las dos (migración incremental, día 3). Por eso:
///   - El mapeo se adapta al esquema existente (nombres de tabla y de columna del legacy), no al revés.
///   - Con SQL Server NO se usarían migraciones de EF Core sobre esta base de datos: el esquema es de SIREI.
///     (Como mucho, cambios aditivos y acordados; ver docs/day-03/07-analisis-legacy.md.)
///
/// Con InMemory, cada contexto con un nombre de base de datos distinto es un almacén independiente.
/// </summary>
public class SireiDbContext(DbContextOptions<SireiDbContext> options) : DbContext(options)
{
    public DbSet<Expediente> Expedientes => Set<Expediente>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configuraciones explícitas: con dos o tres entidades se lee mejor que ApplyConfigurationsFromAssembly.
        modelBuilder.ApplyConfiguration(new ExpedienteConfiguracion());
        modelBuilder.ApplyConfiguration(new TramiteConfiguracion());
    }
}
