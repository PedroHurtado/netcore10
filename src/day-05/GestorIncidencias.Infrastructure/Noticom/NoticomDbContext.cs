using GestorIncidencias.Domain.Lotes;
using GestorIncidencias.Infrastructure.Noticom.Configuraciones;
using Microsoft.EntityFrameworkCore;

namespace GestorIncidencias.Infrastructure.Noticom;

/// <summary>
/// Caso práctico Noticom (día 5): contexto de EF Core que sustituye al NoticomEntities de EF6 (modelo EDMX).
///
/// En MVC 5 cada método hacía "using (var db = new NoticomEntities())" o, peor, el controlador guardaba
/// un contexto en un campo y nadie lo liberaba. Aquí el contenedor de DI crea UNO por petición (Scoped)
/// y lo libera al terminar.
/// </summary>
public class NoticomDbContext(DbContextOptions<NoticomDbContext> options) : DbContext(options)
{
    public DbSet<Lote> Lotes => Set<Lote>();
    public DbSet<Remesa> Remesas => Set<Remesa>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new LoteConfiguracion());
        modelBuilder.ApplyConfiguration(new RemesaConfiguracion());
    }
}
