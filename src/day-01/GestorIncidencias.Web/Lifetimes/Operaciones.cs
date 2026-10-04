namespace GestorIncidencias.Web.Lifetimes;

// Demo didáctica de los tiempos de vida (lifetimes) del contenedor de DI.
// La MISMA clase se registra tres veces con tres interfaces distintas
// para comparar cuándo se crea una instancia nueva.

public interface IOperacion
{
    Guid Id { get; }
}

public interface IOperacionTransient : IOperacion;
public interface IOperacionScoped : IOperacion;
public interface IOperacionSingleton : IOperacion;

public class Operacion : IOperacionTransient, IOperacionScoped, IOperacionSingleton
{
    public Guid Id { get; } = Guid.NewGuid();
}

/// <summary>
/// Servicio intermedio que también recibe las tres operaciones.
/// Sirve para ver si comparte instancia con el endpoint dentro de la misma petición.
/// </summary>
public class ConsumidorOperaciones(
    IOperacionTransient transient,
    IOperacionScoped scoped,
    IOperacionSingleton singleton)
{
    public object Ids => new
    {
        Transient = transient.Id,
        Scoped = scoped.Id,
        Singleton = singleton.Id
    };
}
