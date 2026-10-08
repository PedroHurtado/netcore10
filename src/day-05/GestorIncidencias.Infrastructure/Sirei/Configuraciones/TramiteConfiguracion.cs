using GestorIncidencias.Domain.Expedientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorIncidencias.Infrastructure.Sirei.Configuraciones;

public class TramiteConfiguracion : IEntityTypeConfiguration<Tramite>
{
    public void Configure(EntityTypeBuilder<Tramite> builder)
    {
        builder.ToTable("Tramites");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Descripcion).IsRequired().HasMaxLength(Tramite.DescripcionLongitudMaxima);
        // Pendiente (bit) y FechaCompletado: mismos nombres que en el legacy, no hace falta HasColumnName.
        // La relación con Expediente (y la clave ajena sombra IdExpediente) se configura en ExpedienteConfiguracion.
    }
}
