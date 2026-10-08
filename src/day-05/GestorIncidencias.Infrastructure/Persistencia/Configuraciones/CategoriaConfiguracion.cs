using GestorIncidencias.Domain.Categorias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestorIncidencias.Infrastructure.Persistencia.Configuraciones;

public class CategoriaConfiguracion : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Nombre).IsRequired().HasMaxLength(Categoria.NombreLongitudMaxima);
        builder.HasIndex(c => c.Nombre).IsUnique();

        // DATOS DE REFERENCIA con HasData: forman parte del MODELO. Con SQL Server irían dentro de la migración
        // (INSERT en el método Up); con InMemory los inserta EnsureCreated. Son datos que la aplicación necesita
        // para funcionar, en todos los entornos.
        // Se usan tipos anónimos porque Categoria no tiene setters públicos. Los Id son fijos a propósito.
        // Los datos de DEMOSTRACIÓN (incidencias de ejemplo) NO van aquí: ver InicializadorBaseDatos.
        builder.HasData(
            new { Id = 1, Nombre = "Hardware" },
            new { Id = 2, Nombre = "Software" },
            new { Id = 3, Nombre = "Redes y comunicaciones" },
            new { Id = 4, Nombre = "Cuentas y accesos" },
            new { Id = 5, Nombre = "Otros" });
    }
}
