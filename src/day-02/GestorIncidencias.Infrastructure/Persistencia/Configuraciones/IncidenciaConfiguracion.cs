using GestorIncidencias.Domain.Incidencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorIncidencias.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// Mapeo de la entidad a la base de datos. Usamos la API fluida (en lugar de atributos
/// en la entidad) para que el Dominio no dependa de EF Core.
/// </summary>
public class IncidenciaConfiguracion : IEntityTypeConfiguration<Incidencia>
{
    public void Configure(EntityTypeBuilder<Incidencia> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Titulo).IsRequired().HasMaxLength(Incidencia.TituloLongitudMaxima);
        builder.Property(i => i.Descripcion).HasMaxLength(Incidencia.DescripcionLongitudMaxima);
        builder.Property(i => i.Prioridad).HasConversion<string>();
        builder.Property(i => i.Estado).HasConversion<string>();
        builder.Ignore(i => i.EstaAbierta); // propiedad calculada: no se guarda
    }
}
