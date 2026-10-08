using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Lotes;

namespace GestorIncidencias.UnitTests.Dominio;

/// <summary>
/// Día 5 — caso práctico Noticom: reglas de lotes y remesas que en MVC 5 dependían de lo que
/// la pantalla dejaba ver (columnas ocultas por JavaScript) y no se comprobaban en el servidor.
/// </summary>
public class LoteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    private static Lote NuevoLote(bool validado = false)
    {
        var lote = Lote.Crear(120, 2026, "Liquidaciones IBI 2026 - lote 1", TipoNotificacion.Postal, 100, Ahora).Valor!;
        if (validado)
            lote.Validar(Ahora);
        return lote;
    }

    [Fact]
    public void Validar_UnLotePendiente_LoValida()
    {
        var lote = NuevoLote();

        var resultado = lote.Validar(Ahora);

        Assert.True(resultado.Exito);
        Assert.Equal(EstadoLote.Validado, lote.Estado);
        Assert.Equal(Ahora, lote.FechaValidacion);
    }

    [Fact]
    public void Validar_DosVeces_DaConflicto()
    {
        var lote = NuevoLote(validado: true);

        var resultado = lote.Validar(Ahora);

        Assert.Equal(TipoError.Conflicto, resultado.Tipo);
    }

    [Fact]
    public void ComprobarBorrado_DeUnLoteValidado_DaConflicto()
    {
        var lote = NuevoLote(validado: true);

        Assert.Equal(TipoError.Conflicto, lote.ComprobarBorrado().Tipo);
    }

    [Fact]
    public void CrearRemesa_ConLotesValidados_LosRemesa()
    {
        var lotes = new[] { NuevoLote(validado: true), NuevoLote(validado: true) };

        var resultado = Remesa.Crear(" IBI 2026 ", lotes, Ahora);

        Assert.True(resultado.Exito);
        Assert.Equal("IBI 2026", resultado.Valor!.Nombre);
        Assert.Equal(2, resultado.Valor.Lotes.Count);
        Assert.All(lotes, l => Assert.Equal(EstadoLote.Remesado, l.Estado));
        Assert.All(lotes, l => Assert.False(l.PuedeRemesarse));
    }

    [Fact]
    public void CrearRemesa_ConUnLotePendiente_DaConflictoYNoTocaNingunLote()
    {
        var validado = NuevoLote(validado: true);
        var pendiente = NuevoLote();

        var resultado = Remesa.Crear("IBI 2026", [validado, pendiente], Ahora);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Conflicto, resultado.Tipo);
        Assert.Equal(EstadoLote.Validado, validado.Estado);   // todo o nada
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearRemesa_SinNombre_DaErrorDeValidacion(string nombre)
    {
        var resultado = Remesa.Crear(nombre, [NuevoLote(validado: true)], Ahora);

        Assert.Equal(TipoError.Validacion, resultado.Tipo);
    }

    [Fact]
    public void CrearRemesa_SinLotes_DaErrorDeValidacion()
    {
        var resultado = Remesa.Crear("IBI 2026", [], Ahora);

        Assert.Equal(TipoError.Validacion, resultado.Tipo);
    }
}
