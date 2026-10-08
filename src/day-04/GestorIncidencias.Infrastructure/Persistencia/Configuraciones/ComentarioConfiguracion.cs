using GestorIncidencias.Domain.Incidencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorIncidencias.Infrastructure.Persistencia.Configuraciones;

public class ComentarioConfiguracion : IEntityTypeConfiguration<Comentario>
{
    public void Configure(EntityTypeBuilder<Comentario> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Texto).IsRequired().HasMaxLength(Comentario.TextoLongitudMaxima);
        builder.Property(c => c.Autor).IsRequired().HasMaxLength(Comentario.AutorLongitudMaxima);
        // La relación con Incidencia (y la clave ajena sombra IncidenciaId) se configura en IncidenciaConfiguracion.
    }
}
