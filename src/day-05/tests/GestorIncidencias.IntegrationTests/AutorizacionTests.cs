using System.Net;

namespace GestorIncidencias.IntegrationTests;

/// <summary>Laboratorio 3 del día 4: la política GestionarIncidencias del laboratorio 2, comprobada de punta a punta.</summary>
[Collection("Aplicacion")]
public class AutorizacionTests(AplicacionFixture aplicacion)
{
    [Fact]
    public async Task Resolver_ConUnUsuarioSinRol_Devuelve403()
    {
        var cliente = await aplicacion.ClienteApiAsync("luis@demo.local");

        var respuesta = await cliente.PostAsync("/api/incidencias/3/resolver", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Iniciar_ConUnTecnico_Devuelve200()
    {
        var cliente = await aplicacion.ClienteApiAsync("ana@demo.local");

        // La incidencia 1 empieza Abierta y ninguna otra prueba la modifica.
        var respuesta = await cliente.PostAsync("/api/incidencias/1/iniciar", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
