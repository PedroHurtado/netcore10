using GestorIncidencias.Domain.Lotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorIncidencias.Infrastructure.Noticom.Configuraciones;

public class RemesaConfiguracion : IEntityTypeConfiguration<Remesa>
{
    public void Configure(EntityTypeBuilder<Remesa> builder)
    {
        builder.ToTable("Remesas");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Nombre).IsRequired().HasMaxLength(Remesa.NombreLongitudMaxima);

        // 1:N Remesa → Lotes. Un lote sin remesa tiene RemesaId = null (relación opcional).
        // Restrict: no se puede borrar una remesa que tenga lotes (habría que "desremesarlos" antes).
        builder.HasMany(r => r.Lotes)
            .WithOne(l => l.Remesa)
            .HasForeignKey(l => l.RemesaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(r => r.Lotes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
