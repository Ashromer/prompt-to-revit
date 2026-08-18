namespace RevitBridge.Utils;

/// <summary>
/// Marca un método estático como comando del catálogo (DOCUMENTACION.md §3/§6, ADR-005). El addin
/// lo descubre por reflexión al arrancar y lo publica en <c>/commands</c>. Graduar un snippet
/// estable de Roslyn a comando compilado es, en la práctica, añadir un método marcado con este
/// atributo a este ensamblado — nada más.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ComandoRevitAttribute : Attribute
{
    public string Nombre { get; }

    public ComandoRevitAttribute(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ArgumentException("El nombre del comando no puede estar vacío.", nameof(nombre));
        }

        Nombre = nombre;
    }
}
