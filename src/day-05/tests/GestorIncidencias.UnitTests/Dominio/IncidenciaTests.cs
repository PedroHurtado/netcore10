using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Incidencias;

namespace GestorIncidencias.UnitTests.Dominio;

/// <summary>
/// Pruebas de las reglas de la entidad Incidencia. Son las más sencillas y las más valiosas:
/// sin dobles, sin base de datos, sin configuración. Se crea la entidad, se le pide algo y se comprueba.
///
/// Patrón AAA en cada prueba:
///   Arrange (preparar) → Act (actuar) → Assert (comprobar).
/// Nombre: Metodo_Situacion_ResultadoEsperado, para que el informe de pruebas se lea como una especificación.
/// </summary>
public class IncidenciaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

    private static Incidencia NuevaIncidencia(Prioridad prioridad = Prioridad.Media) =>
        Incidencia.Crear("La impresora no imprime", null, prioridad, Ahora).Valor!;

    [Fact]
    public void Crear_ConDatosValidos_QuedaAbierta()
    {
        var resultado = Incidencia.Crear("  La impresora no imprime  ", "Desde ayer", Prioridad.Alta, Ahora, categoriaId: 1);

        Assert.True(resultado.Exito);
        var incidencia = resultado.Valor!;
        Assert.Equal("La impresora no imprime", incidencia.Titulo);   // se recortan los espacios
        Assert.Equal(EstadoIncidencia.Abierta, incidencia.Estado);
        Assert.Equal(Ahora, incidencia.FechaAlta);
        Assert.Equal(1, incidencia.CategoriaId);
    }

    // [Theory] + [InlineData]: la MISMA prueba con varios datos. Cada fila aparece como una prueba distinta.
    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("abc")]      // menos de TituloLongitudMinima (5)
    public void Crear_ConTituloDemasiadoCorto_Falla(string titulo)
    {
        var resultado = Incidencia.Crear(titulo, null, Prioridad.Media, Ahora);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.Tipo);
        Assert.Null(resultado.Valor);
    }

    [Fact]
    public void Crear_ConTituloDemasiadoLargo_Falla()
    {
        var titulo = new string('x', Incidencia.TituloLongitudMaxima + 1);

        var resultado = Incidencia.Crear(titulo, null, Prioridad.Media, Ahora);

        Assert.False(resultado.Exito);
    }

    [Fact]
    public void Resolver_UnaAbierta_LaResuelveYGuardaLaFecha()
    {
        var incidencia = NuevaIncidencia();
        var fecha = Ahora.AddHours(3);

        var resultado = incidencia.Resolver(fecha);

        Assert.True(resultado.Exito);
        Assert.Equal(EstadoIncidencia.Resuelta, incidencia.Estado);
        Assert.Equal(fecha, incidencia.FechaResolucion);
    }

    [Fact]
    public void Cerrar_UnaAbierta_DaConflictoYNoCambiaElEstado()
    {
        var incidencia = NuevaIncidencia();

        var resultado = incidencia.Cerrar();

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Conflicto, resultado.Tipo);
        Assert.Equal(EstadoIncidencia.Abierta, incidencia.Estado);
    }

    // ---- Laboratorio 3 del día 4 -------------------------------------------------------

    [Fact]
    public void Reabrir_UnaResuelta_VuelveAAbiertaYBorraLaFechaDeResolucion()
    {
        var incidencia = NuevaIncidencia();
        incidencia.Resolver(Ahora);

        var resultado = incidencia.Reabrir();

        Assert.True(resultado.Exito);
        Assert.Equal(EstadoIncidencia.Abierta, incidencia.Estado);
        Assert.Null(incidencia.FechaResolucion);
    }

    [Fact]
    public void Reabrir_UnaAbierta_DaConflicto()
    {
        var incidencia = NuevaIncidencia();

        var resultado = incidencia.Reabrir();

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Conflicto, resultado.Tipo);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(null)]          // null = quitar la categoría
    public void CambiarCategoria_DeUnaAbierta_LaCambia(int? categoriaId)
    {
        var incidencia = Incidencia.Crear("La impresora no imprime", null, Prioridad.Media, Ahora, categoriaId: 1).Valor!;

        var resultado = incidencia.CambiarCategoria(categoriaId);

        Assert.True(resultado.Exito);
        Assert.Equal(categoriaId, incidencia.CategoriaId);
    }

    [Fact]
    public void CambiarCategoria_DeUnaCerrada_DaConflictoYConservaLaCategoria()
    {
        var incidencia = Incidencia.Crear("La impresora no imprime", null, Prioridad.Media, Ahora, categoriaId: 1).Valor!;
        incidencia.Resolver(Ahora);
        incidencia.Cerrar();

        var resultado = incidencia.CambiarCategoria(2);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Conflicto, resultado.Tipo);
        Assert.Equal(1, incidencia.CategoriaId);
    }

    [Fact]
    public void AgregarComentario_AUnaAbierta_LoAnade()
    {
        var incidencia = NuevaIncidencia();

        var resultado = incidencia.AgregarComentario("Revisado el tóner", "Ana García", Ahora);

        Assert.True(resultado.Exito);
        var comentario = Assert.Single(incidencia.Comentarios);
        Assert.Equal("Ana García", comentario.Autor);
    }

    [Fact]
    public void AgregarComentario_AUnaCerrada_DaConflicto()
    {
        var incidencia = NuevaIncidencia();
        incidencia.Resolver(Ahora);
        incidencia.Cerrar();

        var resultado = incidencia.AgregarComentario("¿Sigue fallando?", "Ana García", Ahora);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Conflicto, resultado.Tipo);
        Assert.Empty(incidencia.Comentarios);
    }
}
