using System.Reflection;
using RevitBridge.Core;
using RevitBridge.Utils;

namespace RevitBridge.Addin.Bridge;

/// <summary>
/// Descubre por reflexión los métodos marcados con <see cref="ComandoRevitAttribute"/> en un
/// ensamblado (en producción, <c>RevitBridge.Utils</c>) y construye el catálogo de <c>/commands</c>
/// (ADR-005, F0.9). Nombres duplicados fallan al arrancar el addin, no en silencio.
/// </summary>
public static class CommandCatalog
{
    /// <summary>Escanea todos los tipos de un ensamblado (uso en producción: RevitBridge.Utils).</summary>
    public static IReadOnlyList<CommandDescriptor> Descubrir(Assembly ensamblado) => Descubrir(ensamblado.GetTypes());

    /// <summary>
    /// Escanea un conjunto explícito de tipos. Permite testear la duplicidad de nombres sin
    /// arrastrar todo lo demás que viva en el mismo ensamblado que los tipos de prueba.
    /// </summary>
    public static IReadOnlyList<CommandDescriptor> Descubrir(IEnumerable<Type> tipos)
    {
        var candidatos = tipos
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(m => (Metodo: m, Atributo: m.GetCustomAttribute<ComandoRevitAttribute>()))
            .Where(x => x.Atributo is not null)
            .ToList();

        var duplicados = candidatos
            .GroupBy(x => x.Atributo!.Nombre, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicados.Count > 0)
        {
            throw new InvalidOperationException(
                $"Comandos duplicados en el catálogo: {string.Join(", ", duplicados)}. " +
                "Cada nombre de comando debe ser único (ADR-005).");
        }

        return candidatos
            .Select(x => new CommandDescriptor(x.Atributo!.Nombre, ConstruirEsquema(x.Metodo)))
            .ToList();
    }

    private static IReadOnlyDictionary<string, string> ConstruirEsquema(MethodInfo metodo) =>
        metodo.GetParameters().ToDictionary(p => p.Name ?? "?", p => p.ParameterType.Name);
}
