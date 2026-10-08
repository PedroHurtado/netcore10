using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Expedientes;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Expedientes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GestorIncidencias.UnitTests.Aplicacion;

/// <summary>
/// Día 5 — caso práctico SIREI: el caso de uso "guardar expediente" con dobles.
/// Lo más interesante: la concurrencia optimista (el legacy hacía "el último que guarda gana").
/// </summary>
public class ExpedienteServiceTests
{
    private readonly IExpedienteRepository _repositorio = Substitute.For<IExpedienteRepository>();
    private readonly IExpedienteConsultas _consultas = Substitute.For<IExpedienteConsultas>();
    private readonly IUsuarioActual _usuario = Substitute.For<IUsuarioActual>();
    private readonly FakeTimeProvider _reloj = new(new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private ExpedienteService CrearServicio() =>
        new(_repositorio, _consultas, _usuario, _reloj, NullLogger<ExpedienteService>.Instance);

    private Expediente ExpedienteGuardado(int id)
    {
        var expediente = Expediente.Crear("2026/000123", "María López", "Licencia de obra menor", _reloj.GetUtcNow()).Valor!;
        _repositorio.ObtenerPorIdAsync(id, Arg.Any<CancellationToken>()).Returns(expediente);
        return expediente;   // versión 1
    }

    [Fact]
    public async Task ActualizarAsync_ConLaVersionActual_GuardaConLaFechaDelReloj()
    {
        var expediente = ExpedienteGuardado(5);
        _reloj.Advance(TimeSpan.FromHours(2));

        var resultado = await CrearServicio().ActualizarAsync(5, new ActualizarExpedienteComando("Revisado", EstadoExpediente.EnTramite, Version: 1), Ct);

        Assert.True(resultado.Exito);
        Assert.Equal(_reloj.GetUtcNow(), expediente.FechaModificacion);
        await _repositorio.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarAsync_ConUnaVersionAntigua_DaConflictoYNoGuarda()
    {
        // Ana abrió la ficha en la versión 1; Luis guardó y ahora está en la 2. Ana pulsa Guardar.
        var expediente = ExpedienteGuardado(5);
        expediente.Actualizar("Cambio de Luis", EstadoExpediente.EnTramite, _reloj.GetUtcNow());   // versión 2

        var resultado = await CrearServicio().ActualizarAsync(5, new ActualizarExpedienteComando("Cambio de Ana", EstadoExpediente.Suspendido, Version: 1), Ct);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Conflicto, resultado.Tipo);
        Assert.Equal("Cambio de Luis", expediente.Observaciones);   // no se ha pisado
        await _repositorio.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarAsync_DeUnExpedienteQueNoExiste_DaNoEncontrado()
    {
        var resultado = await CrearServicio().ActualizarAsync(99, new ActualizarExpedienteComando(null, EstadoExpediente.Abierto, 1), Ct);

        Assert.Equal(TipoError.NoEncontrado, resultado.Tipo);
    }
}
