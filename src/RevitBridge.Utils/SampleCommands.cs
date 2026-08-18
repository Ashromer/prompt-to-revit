namespace RevitBridge.Utils;

/// <summary>
/// Comandos de humo para probar el descubrimiento por reflexión de F0.9 end-to-end. No son
/// comandos de modelado reales — el catálogo real se puebla más adelante (F2.2+) según qué
/// snippets de Roslyn se demuestren estables (DOCUMENTACION.md §6). Firma deliberadamente sin
/// tipos de <c>Autodesk.Revit.*</c>: eso es lo que permite reflejar este ensamblado en los tests
/// sin Revit instalado (el ensamblado real de la API no está presente en tiempo de ejecución de
/// los tests, ADR-008).
/// </summary>
public static class SampleCommands
{
    [ComandoRevit("ping")]
    public static object Ping() => new { pong = true };

    [ComandoRevit("eco")]
    public static object Eco(string mensaje) => new { mensaje };
}
