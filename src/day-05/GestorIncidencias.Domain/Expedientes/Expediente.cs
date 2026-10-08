using GestorIncidencias.Domain.Comun;

namespace GestorIncidencias.Domain.Expedientes;

/// <summary>
/// Caso práctico SIREI (día 5): el expediente de ExpedienteDetalle.aspx, convertido en entidad con comportamiento.
///
/// Lo que en Web Forms estaba repartido entre btnGuardar_Click, gvTramites_RowCommand y el DataSet guardado
/// en Session["ExpedienteActual"] vive ahora aquí, y se puede probar sin servidor web ni base de datos:
///   - "No se puede cerrar un expediente con trámites pendientes" (estaba en btnGuardar_Click).
///   - "Un expediente cerrado no admite cambios" (en el legacy la pantalla ocultaba el botón... y nada más).
///
/// Version: contador que sube con cada cambio. Es el TOKEN DE CONCURRENCIA (ver ExpedienteConfiguracion):
/// el legacy hacía "el último que guarda gana" con SqlCommandBuilder; aquí, si dos personas editan a la vez,
/// la segunda recibe un aviso en lugar de pisar los cambios de la primera.
/// </summary>
public class Expediente
{
    public const int NumeroLongitudMaxima = 20;
    public const int TitularLongitudMaxima = 150;
    public const int AsuntoLongitudMaxima = 200;
    public const int ObservacionesLongitudMaxima = 2000;

    private readonly List<Tramite> _tramites = [];

    private Expediente() { }   // para EF Core

    public int Id { get; private set; }

    /// <summary>Número visible del expediente ("2026/000123"). Lo asigna el registro, no la aplicación.</summary>
    public string Numero { get; private set; } = string.Empty;

    public string Titular { get; private set; } = string.Empty;
    public string Asunto { get; private set; } = string.Empty;
    public string? Observaciones { get; private set; }
    public EstadoExpediente Estado { get; private set; }
    public DateTimeOffset FechaAlta { get; private set; }
    public DateTimeOffset FechaModificacion { get; private set; }
    public int Version { get; private set; }

    public IReadOnlyCollection<Tramite> Tramites => _tramites.AsReadOnly();

    public bool TieneTramitesPendientes => _tramites.Any(t => t.Pendiente);

    public static Resultado<Expediente> Crear(string numero, string titular, string asunto, DateTimeOffset fechaAlta)
    {
        numero = (numero ?? string.Empty).Trim();
        titular = (titular ?? string.Empty).Trim();
        asunto = (asunto ?? string.Empty).Trim();

        if (numero.Length is 0 or > NumeroLongitudMaxima)
            return Resultado<Expediente>.Fallo($"El número debe tener entre 1 y {NumeroLongitudMaxima} caracteres.");
        if (titular.Length is 0 or > TitularLongitudMaxima)
            return Resultado<Expediente>.Fallo($"El titular debe tener entre 1 y {TitularLongitudMaxima} caracteres.");
        if (asunto.Length is 0 or > AsuntoLongitudMaxima)
            return Resultado<Expediente>.Fallo($"El asunto debe tener entre 1 y {AsuntoLongitudMaxima} caracteres.");

        return Resultado<Expediente>.Ok(new Expediente
        {
            Numero = numero,
            Titular = titular,
            Asunto = asunto,
            Estado = EstadoExpediente.Abierto,
            FechaAlta = fechaAlta,
            FechaModificacion = fechaAlta,
            Version = 1
        });
    }

    /// <summary>
    /// Lo que hacía btnGuardar_Click: cambiar observaciones y estado, con la regla de negocio que estaba escondida en el evento.
    /// </summary>
    public Resultado Actualizar(string? observaciones, EstadoExpediente nuevoEstado, DateTimeOffset ahora)
    {
        if (Estado == EstadoExpediente.Cerrado)
            return Resultado.Fallo("Un expediente cerrado no admite cambios.", TipoError.Conflicto);

        if (!Enum.IsDefined(nuevoEstado))
            return Resultado.Fallo($"Estado de expediente no válido: {(int)nuevoEstado}.");

        observaciones = string.IsNullOrWhiteSpace(observaciones) ? null : observaciones.Trim();
        if (observaciones?.Length > ObservacionesLongitudMaxima)
            return Resultado.Fallo($"Las observaciones no pueden superar {ObservacionesLongitudMaxima} caracteres.");

        // La regla de SIREI: if (ddlEstado.SelectedValue == "4" && ds.Tables["Tramites"].Select("Pendiente = 1").Length > 0)
        if (nuevoEstado == EstadoExpediente.Cerrado && TieneTramitesPendientes)
            return Resultado.Fallo("No se puede cerrar un expediente con trámites pendientes.", TipoError.Conflicto);

        Observaciones = observaciones;
        Estado = nuevoEstado;
        MarcarModificado(ahora);
        return Resultado.Ok();
    }

    /// <summary>Añade un trámite pendiente. Un expediente cerrado no admite trámites nuevos.</summary>
    public Resultado<Tramite> AgregarTramite(string descripcion, DateTimeOffset ahora)
    {
        if (Estado == EstadoExpediente.Cerrado)
            return Resultado<Tramite>.Fallo("Un expediente cerrado no admite trámites nuevos.", TipoError.Conflicto);

        descripcion = (descripcion ?? string.Empty).Trim();
        if (descripcion.Length is 0 or > Tramite.DescripcionLongitudMaxima)
            return Resultado<Tramite>.Fallo($"La descripción del trámite debe tener entre 1 y {Tramite.DescripcionLongitudMaxima} caracteres.");

        var tramite = new Tramite(descripcion, ahora);
        _tramites.Add(tramite);
        MarcarModificado(ahora);
        return Resultado<Tramite>.Ok(tramite);
    }

    /// <summary>Lo que hacía gvTramites_RowCommand con CommandName="Completar".</summary>
    public Resultado CompletarTramite(int tramiteId, DateTimeOffset ahora)
    {
        if (Estado == EstadoExpediente.Cerrado)
            return Resultado.Fallo("Un expediente cerrado no admite cambios.", TipoError.Conflicto);

        var tramite = _tramites.FirstOrDefault(t => t.Id == tramiteId);
        if (tramite is null)
            return Resultado.Fallo($"El expediente {Numero} no tiene el trámite {tramiteId}.", TipoError.NoEncontrado);

        if (!tramite.Pendiente)
            return Resultado.Fallo("El trámite ya estaba completado.", TipoError.Conflicto);

        tramite.Completar(ahora);
        MarcarModificado(ahora);
        return Resultado.Ok();
    }

    private void MarcarModificado(DateTimeOffset ahora)
    {
        FechaModificacion = ahora;
        Version++;
    }
}
