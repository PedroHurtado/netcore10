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

        // Estado como TEXTO: legible al consultar la tabla a mano ("Abierta", "EnCurso"...).
        builder.Property(i => i.Estado).HasConversion<string>().HasMaxLength(20);

        // Prioridad como NÚMERO (lo que hace EF Core por defecto con los enums):
        // ORDER BY Prioridad ordena Baja(0) < Media(1) < Alta(2) < Critica(3).
        // Si fuera texto, ordenaría alfabéticamente: Alta, Baja, Critica, Media.

        builder.Ignore(i => i.EstaAbierta);   // propiedad calculada: no se guarda

        // ---- Relación N:1 con Categoria -------------------------------------------------
        // Muchas incidencias → una categoría. CategoriaId es int? → relación OPCIONAL.
        // Si se borra una categoría, sus incidencias se quedan sin categoría (SET NULL).
        builder.HasOne(i => i.Categoria)
            .WithMany()
            .HasForeignKey(i => i.CategoriaId)
            .OnDelete(DeleteBehavior.SetNull);

        // ---- Relación 1:N con Comentario (dentro del agregado) ---------------------------
        // La clave ajena "IncidenciaId" no existe en la clase Comentario: es una PROPIEDAD SOMBRA
        // (shadow property). Está en la tabla y en el modelo de EF Core, pero no en el dominio.
        builder.HasMany(i => i.Comentarios)
            .WithOne()
            .HasForeignKey("IncidenciaId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);   // borrar una incidencia borra sus comentarios

        // La propiedad Comentarios no tiene setter: EF Core lee y escribe el campo privado _comentarios.
        builder.Navigation(i => i.Comentarios).UsePropertyAccessMode(PropertyAccessMode.Field);

        // ---- Índices -------------------------------------------------------------------
        // El listado filtra por Estado y ordena por Prioridad: un índice compuesto le evita recorrer toda la tabla.
        // (InMemory lo ignora; en SQL Server una migración lo crearía.)
        builder.HasIndex(i => new { i.Estado, i.Prioridad });
    }
}
