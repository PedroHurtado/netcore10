using System.Net;

namespace GestorIncidencias.IntegrationTests;

/// <summary>
/// Día 5: las dos aplicaciones migradas, probadas de punta a punta por la interfaz WEB (cookie + formularios),
/// como las usaría una persona. Son las pruebas que conviene tener ANTES de dar por migrada una pantalla:
/// comparan el comportamiento nuevo con lo que hacía el legacy.
/// </summary>
[Collection("Aplicacion")]
public class CasoPracticoTests(AplicacionFixture aplicacion)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ------------------------------------------------------------------ SIREI

    [Fact]
    public async Task Sirei_SinIniciarSesion_RedirigeAlLogin()
    {
        var cliente = aplicacion.ClienteSinRedirecciones();

        var respuesta = await cliente.GetAsync("/Sirei/Expedientes", Ct);

        // Lo que hacía "if (Session["Usuario"] == null) Response.Redirect("~/Login.aspx")" en cada página.
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.StartsWith("/Cuenta/Login", respuesta.Headers.Location!.PathAndQuery);
    }

    [Theory]
    [InlineData("/ExpedienteDetalle.aspx?id=3", "/Sirei/Expedientes/Detalle/3")]
    [InlineData("/BuscarExpedientes.aspx", "/Sirei")]   // la URL más corta: Expedientes/Index son los valores por defecto del área
    public async Task UrlAntigua_RedirigePermanentementeALaNueva(string antigua, string nueva)
    {
        var cliente = aplicacion.ClienteSinRedirecciones();

        var respuesta = await cliente.GetAsync(antigua, Ct);

        Assert.Equal(HttpStatusCode.MovedPermanently, respuesta.StatusCode);
        Assert.Equal(nueva, respuesta.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Sirei_ConSesion_MuestraElListadoPaginado()
    {
        var cliente = await aplicacion.ClienteWebAsync("luis@demo.local");

        var html = await cliente.GetStringAsync("/Sirei/Expedientes", Ct);

        Assert.Contains("2026/000001", html);
        Assert.Contains("45 expedientes", html);
    }

    [Fact]
    public async Task Sirei_CerrarUnExpedienteConTramitesPendientes_MuestraLaReglaDeNegocio()
    {
        // El expediente 1 tiene todos sus trámites pendientes (InicializadorSirei).
        var cliente = await aplicacion.ClienteWebAsync("luis@demo.local");
        var ficha = await cliente.GetStringAsync("/Sirei/Expedientes/Detalle/1", Ct);

        var respuesta = await cliente.PostAsync("/Sirei/Expedientes/Detalle/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Formulario.Estado"] = "4",   // Cerrado: el mismo valor que enviaba el ddlEstado de SIREI
            ["Formulario.Observaciones"] = "Intento de cierre",
            ["Formulario.Version"] = AplicacionFixture.ValorCampo(ficha, "Formulario.Version"),
            ["__RequestVerificationToken"] = AplicacionFixture.ValorCampo(ficha, "__RequestVerificationToken")
        }), Ct);

        // No se redirige (el cambio no se ha hecho): se vuelve a pintar la ficha con el error del dominio.
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        // HtmlDecode: Razor codifica lo que no es ASCII ("trámites" → "tr&#xE1;mites") al escribirlo en el HTML.
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync(Ct));
        Assert.Contains("No se puede cerrar un expediente con trámites pendientes.", html);
    }

    // ------------------------------------------------------------------ Noticom

    [Fact]
    public async Task Noticom_PeticionAjaxSinSesion_Devuelve401EnLugarDelLogin()
    {
        // lotes.js envía X-Requested-With. Con esa cabecera, la cookie responde 401 (y no 302 + HTML del login),
        // que es lo que el JavaScript sabe tratar. El legacy buscaba el texto "_Logon_" en una respuesta 200.
        var cliente = aplicacion.ClienteSinRedirecciones();
        var peticion = new HttpRequestMessage(HttpMethod.Get, "/Noticom/Lotes/Tabla?modo=Consulta");
        peticion.Headers.Add("X-Requested-With", "XMLHttpRequest");

        var respuesta = await cliente.SendAsync(peticion, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Noticom_Tabla_DevuelveSoloElFragmentoSinLayout()
    {
        var cliente = await aplicacion.ClienteWebAsync("luis@demo.local");

        var html = await cliente.GetStringAsync("/Noticom/Lotes/Tabla?modo=Validacion&ejercicio=2026", Ct);

        Assert.Contains("tabla-lotes", html);
        Assert.DoesNotContain("<html", html);   // vista parcial: sin _Layout
        Assert.DoesNotContain(">Validar<", html);   // Luis no tiene rol: no ve la acción
    }

    [Fact]
    public async Task Noticom_ValidarSinRol_VaAAccesoDenegado()
    {
        var cliente = await aplicacion.ClienteWebAsync("luis@demo.local");
        var token = await AplicacionFixture.TokenAntiforgeryAsync(cliente, "/Noticom/Lotes?modo=Validacion");

        var respuesta = await cliente.PostAsync("/Noticom/Lotes/Validar/29", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }), Ct);

        // Ocultar el botón es comodidad; lo que impide la operación es [Authorize(Policy = GestionarLotes)].
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.StartsWith("/Cuenta/AccesoDenegado", respuesta.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Noticom_CrearRemesaConUnLotePendiente_NoLaCreaYAvisa()
    {
        // El lote 29 (2026) está pendiente de validación: no se puede remesar. La regla está en el SERVIDOR
        // (en el legacy solo la "garantizaba" que la pantalla no enseñaba esos lotes).
        var cliente = await aplicacion.ClienteWebAsync("ana@demo.local");
        var token = await AplicacionFixture.TokenAntiforgeryAsync(cliente, "/Noticom/Lotes?modo=CrearRemesa");

        var respuesta = await cliente.PostAsync("/Noticom/Lotes/CrearRemesa", new FormUrlEncodedContent(
        [
            new("Nombre", "Remesa de prueba"),
            new("LoteIds", "29"),
            new("volver", "/Noticom/Lotes?modo=CrearRemesa"),
            new("__RequestVerificationToken", token)
        ]), Ct);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/Noticom/Lotes?modo=CrearRemesa", respuesta.Headers.Location!.OriginalString);

        var pagina = WebUtility.HtmlDecode(await cliente.GetStringAsync("/Noticom/Lotes?modo=CrearRemesa", Ct));
        Assert.Contains("Solo se pueden remesar lotes validados y sin remesa", pagina);
    }
}
