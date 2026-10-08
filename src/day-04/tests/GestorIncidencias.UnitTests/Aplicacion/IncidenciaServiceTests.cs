using GestorIncidencias.Application.Abstracciones;
using GestorIncidencias.Application.Configuracion;
using GestorIncidencias.Application.Incidencias;
using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Incidencias;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GestorIncidencias.UnitTests.Aplicacion;

/// <summary>
/// Pruebas de los casos de uso. IncidenciaService depende de INTERFACES (repositorio, consultas, usuario actual),
/// así que en la prueba se le pasan DOBLES creados con NSubstitute: no hay base de datos ni HttpContext.
///
///   Substitute.For&lt;IInterfaz&gt;()          → crea el doble
///   doble.Metodo(...).Returns(valor)        → "cuando te llamen así, devuelve esto"
///   await doble.Received(1).Metodo(...)     → "comprueba que te llamaron así una vez"
///   await doble.DidNotReceive().Metodo(...) → "comprueba que NO te llamaron"
///
/// Esto es lo que ha comprado la arquitectura del día 2: en Web Forms, probar btnGuardar_Click
/// exigía un servidor web, una sesión y SQL Server.
/// </summary>
public class IncidenciaServiceTests
{
    private readonly IIncidenciaRepository _repositorio = Substitute.For<IIncidenciaRepository>();
    private readonly IIncidenciaConsultas _consultas = Substitute.For<IIncidenciaConsultas>();
    private readonly IUsuarioActual _usuario = Substitute.For<IUsuarioActual>();

    // Reloj FALSO: la prueba decide qué hora es. Con DateTime.Now las fechas serían imposibles de comprobar.
    private readonly FakeTimeProvider _reloj = new(new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero));

    // Token de cancelación de xUnit: si se cancela la ejecución de las pruebas, las operaciones async se detienen.
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IncidenciasOptions _opciones = new() { MaxIncidenciasAbiertas = 10, PrioridadPorDefecto = Prioridad.Media };

    private IncidenciaService CrearServicio()
    {
        var opciones = Substitute.For<IOptionsSnapshot<IncidenciasOptions>>();
        opciones.Value.Returns(_opciones);
        _usuario.Nombre.Returns("Ana García");

        return new IncidenciaService(_repositorio, _consultas, _usuario, _reloj, opciones, NullLogger<IncidenciaService>.Instance);
    }

    private static IncidenciaDto DtoDe(Incidencia i) =>
        new(i.Id, i.Titulo, i.Descripcion, i.Prioridad, i.Estado, i.FechaAlta, i.FechaResolucion, i.CategoriaId, null, i.Comentarios.Count);

    [Fact]
    public async Task CrearAsync_ConElLimiteDeAbiertasAlcanzado_DaConflictoYNoGuarda()
    {
        _repositorio.ContarAbiertasAsync(Arg.Any<CancellationToken>()).Returns(10);   // ya hay 10 abiertas
        var servicio = CrearServicio();

        var resultado = await servicio.CrearAsync(new CrearIncidenciaComando("Nueva incidencia de prueba", null, null), Ct);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Conflicto, resultado.Tipo);
        await _repositorio.DidNotReceive().AgregarAsync(Arg.Any<Incidencia>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearAsync_SinPrioridad_UsaLaDeLaConfiguracionYLaHoraDelReloj()
    {
        _repositorio.ContarAbiertasAsync(Arg.Any<CancellationToken>()).Returns(0);
        Incidencia? guardada = null;
        await _repositorio.AgregarAsync(Arg.Do<Incidencia>(i => guardada = i), Arg.Any<CancellationToken>());   // captura lo que se guarda
        _consultas.ObtenerAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(_ => DtoDe(guardada!));
        var servicio = CrearServicio();

        var resultado = await servicio.CrearAsync(new CrearIncidenciaComando("Nueva incidencia de prueba", null, Prioridad: null), Ct);

        Assert.True(resultado.Exito);
        Assert.NotNull(guardada);
        Assert.Equal(Prioridad.Media, guardada.Prioridad);
        Assert.Equal(_reloj.GetUtcNow(), guardada.FechaAlta);
    }

    [Fact]
    public async Task CrearAsync_ConUnaCategoriaQueNoExiste_FallaSinGuardar()
    {
        _repositorio.ContarAbiertasAsync(Arg.Any<CancellationToken>()).Returns(0);
        _repositorio.ExisteCategoriaAsync(99, Arg.Any<CancellationToken>()).Returns(false);
        var servicio = CrearServicio();

        var resultado = await servicio.CrearAsync(new CrearIncidenciaComando("Nueva incidencia de prueba", null, null, CategoriaId: 99), Ct);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.Tipo);
        await _repositorio.DidNotReceive().AgregarAsync(Arg.Any<Incidencia>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolverAsync_DeUnaQueNoExiste_DevuelveNoEncontrado()
    {
        _repositorio.ObtenerPorIdAsync(42, Arg.Any<CancellationToken>()).Returns((Incidencia?)null);
        var servicio = CrearServicio();

        var resultado = await servicio.ResolverAsync(42, Ct);

        Assert.Equal(TipoError.NoEncontrado, resultado.Tipo);
        await _repositorio.DidNotReceive().GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComentarAsync_PoneComoAutorAlUsuarioActualYGuarda()
    {
        var incidencia = Incidencia.Crear("La impresora no imprime", null, Prioridad.Baja, _reloj.GetUtcNow()).Valor!;
        _repositorio.ObtenerPorIdAsync(1, Arg.Any<CancellationToken>()).Returns(incidencia);
        _consultas.ObtenerAsync(1, Arg.Any<CancellationToken>()).Returns(DtoDe(incidencia));
        var servicio = CrearServicio();

        var resultado = await servicio.ComentarAsync(1, new ComentarIncidenciaComando("Revisado el tóner"), Ct);

        Assert.True(resultado.Exito);
        var comentario = Assert.Single(incidencia.Comentarios);
        Assert.Equal("Ana García", comentario.Autor);           // viene de IUsuarioActual, no del comando
        Assert.Equal(_reloj.GetUtcNow(), comentario.Fecha);
        await _repositorio.Received(1).GuardarCambiosAsync(Arg.Any<CancellationToken>());
    }
}
