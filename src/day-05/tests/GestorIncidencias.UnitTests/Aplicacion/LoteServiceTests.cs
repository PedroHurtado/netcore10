using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Lotes;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Lotes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GestorIncidencias.UnitTests.Aplicacion;

/// <summary>Día 5 — caso práctico Noticom: el caso de uso "crear remesa" con dobles.</summary>
public class LoteServiceTests
{
    private readonly ILoteRepository _repositorio = Substitute.For<ILoteRepository>();
    private readonly ILoteConsultas _consultas = Substitute.For<ILoteConsultas>();
    private readonly IUsuarioActual _usuario = Substitute.For<IUsuarioActual>();
    private readonly FakeTimeProvider _reloj = new(new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private LoteService CrearServicio() =>
        new(_repositorio, _consultas, _usuario, _reloj, NullLogger<LoteService>.Instance);

    [Fact]
    public async Task CrearRemesaAsync_ConUnLoteQueNoExiste_DaNoEncontradoYNoGuarda()
    {
        // El repositorio solo encuentra uno de los dos lotes pedidos (el otro lo ha borrado alguien).
        var lote = Lote.Crear(120, 2026, "Liquidaciones IBI", TipoNotificacion.Postal, 10, _reloj.GetUtcNow()).Valor!;
        _repositorio.ObtenerVariosAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).Returns([lote]);

        var resultado = await CrearServicio().CrearRemesaAsync(new CrearRemesaComando("IBI 2026", [lote.Id, 77]), Ct);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.NoEncontrado, resultado.Tipo);
        Assert.Contains("77", resultado.Error);
        _repositorio.DidNotReceive().AgregarRemesa(Arg.Any<Remesa>());
        await _repositorio.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BorrarAsync_UnLoteValidado_DaConflictoYNoBorra()
    {
        var lote = Lote.Crear(120, 2026, "Liquidaciones IBI", TipoNotificacion.Postal, 10, _reloj.GetUtcNow()).Valor!;
        lote.Validar(_reloj.GetUtcNow());
        _repositorio.ObtenerPorIdAsync(3, Arg.Any<CancellationToken>()).Returns(lote);

        var resultado = await CrearServicio().BorrarAsync(3, Ct);

        Assert.Equal(TipoError.Conflicto, resultado.Tipo);
        _repositorio.DidNotReceive().Borrar(Arg.Any<Lote>());
    }
}
