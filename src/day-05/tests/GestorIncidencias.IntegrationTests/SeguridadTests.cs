using System.Net;
using System.Net.Http.Json;

namespace GestorIncidencias.IntegrationTests;

/// <summary>
/// ¿Está protegido lo que tiene que estarlo? Estas pruebas recorren el pipeline completo
/// (UseAuthentication, UseAuthorization, FallbackPolicy, [AllowAnonymous], [Authorize]...).
/// Si alguien cambia el orden de los middleware o quita un atributo, fallan.
/// </summary>
[Collection("Aplicacion")]
public class SeguridadTests(AplicacionFixture aplicacion)
{
    [Fact]
    public async Task Portada_SinIniciarSesion_EsPublica()
    {
        var cliente = aplicacion.ClienteSinRedirecciones();

        var respuesta = await cliente.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("/Incidencias")]
    [InlineData("/Incidencias/Detalle/1")]
    [InlineData("/Paginas/Incidencias")]
    [InlineData("/Usuarios")]
    public async Task PaginasPrivadas_SinIniciarSesion_RedirigenAlLogin(string url)
    {
        var cliente = aplicacion.ClienteSinRedirecciones();

        var respuesta = await cliente.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.StartsWith("/Cuenta/Login", respuesta.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Api_SinToken_Devuelve401()
    {
        var cliente = aplicacion.ClienteSinRedirecciones();

        var respuesta = await cliente.GetAsync("/api/incidencias", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);   // la API nunca redirige al login
    }

    [Fact]
    public async Task Token_ConUnUsuarioQueNoExiste_Devuelve401()
    {
        var cliente = aplicacion.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/cuenta/token", new { correo = "nadie@demo.local", clave = "Curso2026!" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task FormularioWeb_SinTokenAntiforgery_Devuelve400()
    {
        // Aunque no haya sesión, el filtro antiforgery actúa ANTES que la autorización de la acción:
        // un POST sin token (lo que enviaría una web atacante) se rechaza sin llegar al controlador.
        var cliente = aplicacion.ClienteSinRedirecciones();

        var respuesta = await cliente.PostAsync(
            "/Cuenta/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Correo"] = "ana@demo.local",
                ["Clave"] = AplicacionFixture.Clave
            }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Salud_RespondeHealthy()
    {
        var cliente = aplicacion.CreateClient();

        var respuesta = await cliente.GetStringAsync("/salud", TestContext.Current.CancellationToken);

        Assert.Equal("Healthy", respuesta);
    }
}
