using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

    private record RespuestaToken(string TokenType, string AccessToken, int ExpiresIn);
}

[CollectionDefinition("Aplicacion")]
public class AplicacionCollection : ICollectionFixture<AplicacionFixture>;
