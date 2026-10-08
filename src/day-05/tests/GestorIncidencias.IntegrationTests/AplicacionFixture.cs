using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GestorIncidencias.IntegrationTests;

/// <summary>
/// La aplicación completa, arrancada en memoria. WebApplicationFactory&lt;Program&gt; ejecuta el Program.cs real
/// en el entorno "Development": se cargan los datos y los usuarios de demostración de appsettings.Development.json.
///
/// Se COMPARTE entre todas las clases de pruebas (colección "Aplicacion"): arrancar la aplicación cuesta
/// un par de segundos y las pruebas de una colección se ejecutan una detrás de otra, sin pisarse.
/// </summary>
public class AplicacionFixture : WebApplicationFactory<Program>
{
    public const string Clave = "Curso2026!";

    /// <summary>Opciones JSON iguales a las de la API (camelCase y enums como texto).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Cliente que NO sigue las redirecciones: así se puede comprobar el 302 al login.</summary>
    public HttpClient ClienteSinRedirecciones() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>Pide un token a POST /api/cuenta/token y devuelve un cliente que lo envía en cada petición.</summary>
    public async Task<HttpClient> ClienteApiAsync(string correo)
    {
        var cliente = CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/cuenta/token", new { correo, clave = Clave });
        respuesta.EnsureSuccessStatusCode();
        var token = await respuesta.Content.ReadFromJsonAsync<RespuestaToken>(Json);

        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return cliente;
    }

    /// <summary>
    /// Día 5: cliente del NAVEGADOR (cookie) con la sesión iniciada por el formulario de login, como haría un usuario:
    /// GET /Cuenta/Login → leer el token antiforgery del HTML → POST con correo, clave y token.
    /// El cliente guarda las cookies (autenticación y antiforgery) y NO sigue las redirecciones,
    /// para poder comprobar los 302 de Post-Redirect-Get y de acceso denegado.
    /// </summary>
    public async Task<HttpClient> ClienteWebAsync(string correo)
    {
        var cliente = ClienteSinRedirecciones();

        var token = await TokenAntiforgeryAsync(cliente, "/Cuenta/Login");
        var respuesta = await cliente.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Correo"] = correo,
            ["Clave"] = Clave,
            ["__RequestVerificationToken"] = token
        }));

        if (respuesta.StatusCode != HttpStatusCode.Redirect)
            throw new InvalidOperationException($"No se pudo iniciar sesión como {correo}: {respuesta.StatusCode}");
        return cliente;
    }

    /// <summary>Pide una página con formulario y devuelve el valor del campo oculto __RequestVerificationToken.</summary>
    public static async Task<string> TokenAntiforgeryAsync(HttpClient cliente, string url) =>
        ValorCampo(await cliente.GetStringAsync(url), "__RequestVerificationToken");

    /// <summary>
    /// Valor de un campo (input) del HTML. Las comillas son opcionales porque WebMarkupMin minifica el HTML
    /// y quita las que no hacen falta (name=__RequestVerificationToken).
    /// </summary>
    public static string ValorCampo(string html, string nombre)
    {
        var coincidencia = Regex.Match(html, $"""<input[^>]*name="?{Regex.Escape(nombre)}"?[^>]*value="?([^"\s>]+)""");
        return coincidencia.Success
            ? WebUtility.HtmlDecode(coincidencia.Groups[1].Value)
            : throw new InvalidOperationException($"No se ha encontrado el campo {nombre} en la página.");
    }

    private record RespuestaToken(string TokenType, string AccessToken, int ExpiresIn);
}

[CollectionDefinition("Aplicacion")]
public class AplicacionCollection : ICollectionFixture<AplicacionFixture>;
