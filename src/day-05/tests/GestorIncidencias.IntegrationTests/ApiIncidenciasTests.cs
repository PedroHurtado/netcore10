using System.Net;
using System.Net.Http.Json;
using GestorIncidencias.Application.Comun;
using GestorIncidencias.Application.Incidencias;

namespace GestorIncidencias.IntegrationTests;

/// <summary>
/// La API de incidencias de punta a punta, con un token real pedido a /api/cuenta/token.
/// Los datos son los de demostración (4 incidencias "vivas" con Id 1 a 4 y 200 cerradas).
///
/// OJO: la base de datos es la MISMA para todas las pruebas de la colección. Una prueba que modifica datos
/// no debe dar por hecho el estado que dejó otra: mejor crear sus propios datos o comprobar solo lo suyo.
/// </summary>
[Collection("Aplicacion")]
public class ApiIncidenciasTests(AplicacionFixture aplicacion)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Listar_ConToken_DevuelveUnaPagina()
    {
        var cliente = await aplicacion.ClienteApiAsync("luis@demo.local");

        var pagina = await cliente.GetFromJsonAsync<Pagina<IncidenciaDto>>("/api/incidencias?tamano=5", AplicacionFixture.Json, Ct);

        Assert.NotNull(pagina);
        Assert.Equal(5, pagina.Elementos.Count);
        Assert.True(pagina.TotalElementos >= 204);
    }

    [Fact]
    public async Task Crear_ConTituloCorto_Devuelve400()
    {
        var cliente = await aplicacion.ClienteApiAsync("luis@demo.local");

        var respuesta = await cliente.PostAsJsonAsync("/api/incidencias", new { titulo = "abc" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Comentar_PoneComoAutorAlUsuarioDelToken()
    {
        var cliente = await aplicacion.ClienteApiAsync("ana@demo.local");

        var respuesta = await cliente.PostAsJsonAsync("/api/incidencias/2/comentarios", new { texto = "Probado desde una prueba de integración" }, Ct);
        respuesta.EnsureSuccessStatusCode();

        var detalle = await cliente.GetFromJsonAsync<IncidenciaDetalleDto>("/api/incidencias/2", AplicacionFixture.Json, Ct);
        var ultimo = detalle!.Comentarios[^1];
        Assert.Equal("Probado desde una prueba de integración", ultimo.Texto);
        Assert.Equal("Ana García", ultimo.Autor);   // del claim nombre_completo del token
    }

    [Fact]
    public async Task Obtener_UnaQueNoExiste_Devuelve404()
    {
        var cliente = await aplicacion.ClienteApiAsync("luis@demo.local");

        var respuesta = await cliente.GetAsync("/api/incidencias/999999", Ct);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
