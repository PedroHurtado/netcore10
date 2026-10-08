using GestorIncidencias.Domain.Lotes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorIncidencias.Infrastructure.Noticom.Configuraciones;

/// <summary>
/// Al portar un modelo EDMX de EF6, lo que el diseñador guardaba en XML (tablas, claves, longitudes,
/// relaciones) se escribe aquí con la API fluida. Es el paso más mecánico de la migración... y el más
/// fácil de hacer mal si no se compara con el esquema real (longitudes, nulos, nombres de columna).
/// </summary>
public class LoteConfiguracion : IEntityTypeConfiguration<Lote>
{
    public void Configure(EntityTypeBuilder<Lote> builder)
    {
        builder.ToTable("Lotes");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Descripcion).IsRequired().HasMaxLength(Lote.DescripcionLongitudMaxima);
        builder.Property(l => l.Estado).HasConversion<string>().HasMaxLength(25);
        builder.Property(l => l.Tipo).HasConversion<string>().HasMaxLength(15);

        builder.Ignore(l => l.PuedeValidarse);
        builder.Ignore(l => l.PuedeBorrarse);
        builder.Ignore(l => l.PuedeRemesarse);

        // La pantalla siempre filtra por proceso y ejercicio: índice compuesto (InMemory lo ignora).
        builder.HasIndex(l => new { l.Proceso, l.Ejercicio });
    }
}
