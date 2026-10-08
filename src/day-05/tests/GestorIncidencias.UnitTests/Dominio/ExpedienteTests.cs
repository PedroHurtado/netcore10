using GestorIncidencias.Domain.Comun;
using GestorIncidencias.Domain.Expedientes;

namespace GestorIncidencias.UnitTests.Dominio;

/// <summary>
/// Día 5 — caso práctico SIREI: la regla que estaba en btnGuardar_Click, probada sin servidor web,
/// sin Session y sin SQL Server. En SIREI esta prueba no se podía escribir.
/// </summary>
public class ExpedienteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    private static Expediente NuevoExpediente(params string[] tramites)
    {
        var expediente = Expediente.Crear("2026/000123", "María López", "Licencia de obra menor", Ahora).Valor!;
        foreach (var tramite in tramites)
            expediente.AgregarTramite(tramite, Ahora);
        return expediente;
    }

    [Fact]
    public void Crear_EmpiezaAbiertoEnLaVersion1()
    {
        var expediente = NuevoExpediente();

        Assert.Equal(EstadoExpediente.Abierto, expediente.Estado);
        Assert.Equal(1, expediente.Version);
    }

    [Fact]
    public void Actualizar_CerrarConTramitesPendientes_DaConflictoYNoCambiaNada()
    {
        var expediente = NuevoExpediente("Informe técnico");
        var version = expediente.Version;

        var resultado = expediente.Actualizar("Cierre", EstadoExpediente.Cerrado, Ahora.AddDays(1));

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Conflicto, resultado.Tipo);
        Assert.Equal(EstadoExpediente.Abierto, expediente.Estado);
        Assert.Null(expediente.Observaciones);
        Assert.Equal(version, expediente.Version);
    }

    [Fact]
    public void Actualizar_CerrarSinTramitesPendientes_LoCierraYSubeLaVersion()
    {
        var expediente = NuevoExpediente();

        var resultado = expediente.Actualizar("  Archivado  ", EstadoExpediente.Cerrado, Ahora.AddDays(1));

        Assert.True(resultado.Exito);
        Assert.Equal(EstadoExpediente.Cerrado, expediente.Estado);
        Assert.Equal("Archivado", expediente.Observaciones);
        Assert.Equal(Ahora.AddDays(1), expediente.FechaModificacion);
        Assert.Equal(2, expediente.Version);
    }

    [Fact]
    public void Actualizar_UnExpedienteCerrado_DaConflicto()
    {
        var expediente = NuevoExpediente();
        expediente.Actualizar(null, EstadoExpediente.Cerrado, Ahora);

        var resultado = expediente.Actualizar("Reabrir", EstadoExpediente.Abierto, Ahora);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Conflicto, resultado.Tipo);
    }

    [Fact]
    public void Actualizar_ConUnEstadoQueNoExiste_DaErrorDeValidacion()
    {
        // Lo que llegaría si alguien manipula el desplegable y envía IdEstado=9.
        var expediente = NuevoExpediente();

        var resultado = expediente.Actualizar(null, (EstadoExpediente)9, Ahora);

        Assert.False(resultado.Exito);
        Assert.Equal(TipoError.Validacion, resultado.Tipo);
    }

    [Fact]
    public void CompletarTramite_ElUnicoPendiente_PermiteCerrarDespues()
    {
        var expediente = NuevoExpediente("Informe técnico");
        var tramite = expediente.Tramites.Single();   // sin base de datos el Id es 0, pero es el único

        var completar = expediente.CompletarTramite(tramite.Id, Ahora);
        var cerrar = expediente.Actualizar(null, EstadoExpediente.Cerrado, Ahora);

        Assert.True(completar.Exito);
        Assert.False(tramite.Pendiente);
        Assert.True(cerrar.Exito);
    }

    [Fact]
    public void CompletarTramite_QueNoExiste_DaNoEncontrado()
    {
        var expediente = NuevoExpediente();

        var resultado = expediente.CompletarTramite(99, Ahora);

        Assert.Equal(TipoError.NoEncontrado, resultado.Tipo);
    }
}
