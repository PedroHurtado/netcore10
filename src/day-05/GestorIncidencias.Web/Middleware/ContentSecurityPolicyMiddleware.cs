namespace GestorIncidencias.Web.Middleware;

/// <summary>
/// Añade la cabecera Content-Security-Policy (CSP) a las respuestas HTML (las páginas).
///
/// La CSP solo la aplica el navegador al DOCUMENTO: es la página la que decide qué scripts y estilos puede
/// cargar o ejecutar. En los recursos que carga la página (CSS, JS, imágenes) o en el JSON de la API
/// la cabecera no tendría ningún efecto, así que solo se añade cuando la respuesta es text/html.
///
/// La política se lee de configuración ("ContentSecurityPolicy" en appsettings.json), así que cada entorno
/// puede tener la suya sin tocar código. La de partida es estricta a propósito: sirve de "arnés" para que
/// nadie escriba JavaScript ni CSS inline. Razor no da ningún error; es el NAVEGADOR el que bloquea y
/// avisa en la consola (F12) en cuanto aparece algo de esto:
///   - &lt;script&gt;...&lt;/script&gt; inline           → script-src 'self'
///   - onclick="..." y demás atributos on*        → script-src-attr 'none'
///   - href="javascript:..."                       → script-src 'self' (sin 'unsafe-inline')
///   - &lt;style&gt;...&lt;/style&gt; inline             → style-src 'self'
///   - style="..." en cualquier elemento           → style-src-attr 'none'
/// Lo correcto es mover el código a ficheros de wwwroot/js y wwwroot/css.
/// </summary>
public class ContentSecurityPolicyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public const string ClaveConfiguracion = "ContentSecurityPolicy";

    // Se lee una sola vez: el middleware es singleton.
    private readonly string? _politica = configuration[ClaveConfiguracion];

    public Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_politica))
            return next(context);

        // Al entrar aún no se sabe qué se va a devolver: el Content-Type se conoce justo antes de enviar
        // las cabeceras. OnStarting también se ejecuta en los aciertos del Output Cache.
        context.Response.OnStarting(() =>
        {
            if (context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
                context.Response.Headers.ContentSecurityPolicy = _politica;

            return Task.CompletedTask;
        });

        return next(context);
    }
}

public static class ContentSecurityPolicyMiddlewareExtensions
{
    /// <summary>Método de extensión para usarlo como app.UseContentSecurityPolicy().</summary>
    public static IApplicationBuilder UseContentSecurityPolicy(this IApplicationBuilder app) =>
        app.UseMiddleware<ContentSecurityPolicyMiddleware>();
}
