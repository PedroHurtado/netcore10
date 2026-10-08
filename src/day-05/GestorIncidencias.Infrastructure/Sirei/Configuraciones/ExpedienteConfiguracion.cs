using GestorIncidencias.Domain.Expedientes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorIncidencias.Infrastructure.Sirei.Configuraciones;

/// <summary>
/// Mapeo de Expediente sobre la tabla EXISTENTE de SIREI. El dominio usa nombres claros (Estado, Tramites);
/// la configuración los traduce a los nombres del legacy (IdEstado, IdExpediente). Así el modelo nuevo
/// no hereda los nombres antiguos y la base de datos no hay que tocarla.
/// </summary>
public class ExpedienteConfiguracion : IEntityTypeConfiguration<Expediente>
{
    public void Configure(EntityTypeBuilder<Expediente> builder)
    {
        builder.ToTable("Expedientes");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Numero).IsRequired().HasMaxLength(Expediente.NumeroLongitudMaxima);
        builder.HasIndex(e => e.Numero).IsUnique();
        builder.Property(e => e.Titular).IsRequired().HasMaxLength(Expediente.TitularLongitudMaxima);
        builder.Property(e => e.Asunto).IsRequired().HasMaxLength(Expediente.AsuntoLongitudMaxima);
        builder.Property(e => e.Observaciones).HasMaxLength(Expediente.ObservacionesLongitudMaxima);

        // El enum se guarda como NÚMERO (lo normal en EF Core) en la columna que ya existe: IdEstado.
        // Por eso los valores de EstadoExpediente están fijados a mano (Abierto = 1 ... Cerrado = 4).
        builder.Property(e => e.Estado).HasColumnName("IdEstado");

        // OJO con las fechas de un esquema legacy: SIREI guarda "datetime" en HORA LOCAL (DateTime.Now).
        // Con SQL Server habría que decidir: o un ValueConverter que convierta DateTimeOffset ↔ hora local,
        // o cambiar el legacy para que escriba UTC. Con InMemory no se nota; en el proyecto real, sí.

        // TOKEN DE CONCURRENCIA: en el UPDATE, EF Core añade "WHERE Version = @versionOriginal".
        // Si otra petición ha guardado entretanto, no se actualiza ninguna fila y EF Core lanza
        // DbUpdateConcurrencyException. (Con SQL Server también se podría usar una columna rowversion.)
        builder.Property(e => e.Version).IsConcurrencyToken();

        builder.Ignore(e => e.TieneTramitesPendientes);

        // 1:N con Tramite, clave ajena con el nombre del legacy (IdExpediente) como propiedad sombra.
        builder.HasMany(e => e.Tramites)
            .WithOne()
            .HasForeignKey("IdExpediente")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Tramites).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
