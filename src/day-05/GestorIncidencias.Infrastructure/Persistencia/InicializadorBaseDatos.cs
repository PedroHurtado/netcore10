using GestorIncidencias.Application.Configuracion;
using GestorIncidencias.Domain.Incidencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestorIncidencias.Infrastructure.Persistencia;

/// <summary>
/// Se ejecuta al arrancar la aplicación (IHostedService):
///   1. EnsureCreated: "crea" la base de datos. Con InMemory es lo que inserta los datos de
///      referencia declarados con HasData (las categorías). Con SQL Server esto lo harían las migraciones.
///   2. Si Incidencias:CargarDatosDemo = true → carga incidencias de ejemplo con categorías y comentarios.
///
/// Día 4: es IDEMPOTENTE (si ya hay incidencias, no hace nada). Las pruebas de integración arrancan la
/// aplicación varias veces en el mismo proceso y la base de datos InMemory con el mismo nombre se comparte.
/// </summary>
public class InicializadorBaseDatos(
    IServiceScopeFactory scopeFactory,
    TimeProvider reloj,
    IOptions<IncidenciasOptions> opciones,
    ILogger<InicializadorBaseDatos> logger) : IHostedService
{
    private static readonly string[] TitulosHistorico =
    [
        "No arranca el equipo del puesto", "Olvido de contraseña", "Sin acceso a la carpeta compartida",
        "El monitor parpadea", "Error al generar el informe mensual", "La aplicación de nóminas va lenta",
        "No se reciben correos externos", "Alta de usuario nuevo", "Wifi inestable en la sala de reuniones",
        "Actualizar versión del navegador"
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // El contexto es Scoped: fuera de una petición HTTP hay que crear un ámbito a mano.
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IncidenciasDbContext>();

        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!opciones.Value.CargarDatosDemo || await db.Incidencias.AnyAsync(cancellationToken))
            return;

        var ahora = reloj.GetUtcNow();

        // Las 4 incidencias "vivas" de los días anteriores, ahora con categoría y comentarios.
        var impresora = Nueva("La impresora de la 2ª planta no imprime", null, Prioridad.Baja, ahora.AddDays(-3), categoriaId: 1);
        var error500 = Nueva("Error 500 al guardar un expediente", "Ocurre al pulsar Guardar con adjuntos.", Prioridad.Alta, ahora.AddDays(-1), categoriaId: 2);
        var correo = Nueva("Caída del servicio de correo", null, Prioridad.Critica, ahora.AddHours(-2), categoriaId: 3);
        var vpn = Nueva("No funciona la VPN desde casa", "Error de certificado al conectar.", Prioridad.Media, ahora.AddDays(-5), categoriaId: 4);

        impresora.AgregarComentario("Revisado el tóner: está bien. Parece un atasco.", "Soporte N1", ahora.AddDays(-2));
        error500.Iniciar();
        error500.AgregarComentario("Reproducido con ficheros de más de 10 MB.", "Ana (desarrollo)", ahora.AddHours(-20));
        error500.AgregarComentario("Pendiente de revisar el límite de subida en IIS.", "Ana (desarrollo)", ahora.AddHours(-19));
        vpn.Resolver(ahora.AddDays(-4));
        vpn.AgregarComentario("Certificado renovado en el portátil del usuario.", "Soporte N2", ahora.AddDays(-4));

        // Histórico: incidencias ya cerradas, para que el listado tenga varias páginas.
        var historico = Enumerable.Range(1, opciones.Value.IncidenciasHistoricasDemo).Select(n =>
        {
            var fecha = ahora.AddDays(-30 - n);
            var incidencia = Nueva($"{TitulosHistorico[n % TitulosHistorico.Length]} (#H{n})", null,
                (Prioridad)(n % 4), fecha, categoriaId: n % 5 + 1);
            incidencia.Resolver(fecha.AddHours(n % 48 + 1));
            incidencia.Cerrar();
            return incidencia;
        });

        // UNIDAD DE TRABAJO: todo se añade al contexto y se guarda con UN SaveChanges.
        // Primero las 4 "vivas", para que conserven los Id 1 a 4 de los días anteriores.
        db.Incidencias.AddRange(impresora, error500, correo, vpn);
        db.Incidencias.AddRange(historico);
        var filas = await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Datos de demostración cargados ({Filas} filas)", filas);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static Incidencia Nueva(string titulo, string? descripcion, Prioridad prioridad, DateTimeOffset fecha, int categoriaId) =>
        Incidencia.Crear(titulo, descripcion, prioridad, fecha, categoriaId).Valor!;
}
