using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace PocMcpSdk;

// Herramientas del PoC 001 (FR-001): una sin parámetros y otra con parámetros tipados,
// ambas con respuesta fija. Forma tomada de DECISION-PELDANO.md §3.1/§3.2.
[McpServerToolType]
public static class PocTools
{
    [McpServerTool(Name = "poc_ping", Title = "PoC MCP-SDK: ping sin parámetros")]
    [Description("Herramienta de prueba del PoC 001 (SDK oficial de MCP para .NET). No recibe parámetros y devuelve un texto fijo, para confirmar que Claude Code puede listar e invocar herramientas de este servidor.")]
    public static string PocPing() =>
        "pong - PocMcpSdk (PoC 001, SDK oficial de MCP para .NET), herramienta sin parametros";

    [McpServerTool(Name = "poc_echo_params", Title = "PoC MCP-SDK: eco con parámetros tipados")]
    [Description("Herramienta de prueba del PoC 001 (SDK oficial de MCP para .NET). Recibe un texto y un número entero y los devuelve dentro de una respuesta fija, para confirmar que el esquema tipado y los valores de los parámetros llegan de verdad al servidor.")]
    public static string PocEchoParams(
        [Description("Texto de prueba a incluir en la respuesta.")] string mensaje,
        [Description("Número entero de prueba a incluir en la respuesta.")] int repeticiones) =>
        $"eco - PocMcpSdk (PoC 001) recibio mensaje='{mensaje}' repeticiones={repeticiones}";

    // Marcadores únicos de inicio y fin de la traza (FR-004/SC-003 §6). Si el cliente recorta
    // el contenido, falta el marcador final y se detecta sin comparar el texto entero.
    // Solo ASCII alfanumérico + '=', '-', '_': System.Text.Json (codificador HTML-safe por
    // defecto) no los escapa, así que el marcador aparece literal en la respuesta serializada.
    private const string TrazaInicio = "===TRAZA-INICIO-POC-ERROR===";
    private const string TrazaFin = "===TRAZA-FIN-POC-ERROR===";

    [McpServerTool(Name = "poc_error", Title = "PoC MCP-SDK: error de runtime simulado")]
    [Description("Herramienta de prueba del PoC 001 (SDK oficial de MCP para .NET). No recibe parámetros y devuelve, como respuesta MCP correcta (sin IsError ni excepción lanzada), el JSON del contrato de fallo de runtime del puente (DOCUMENTACION.md §4): ok=false, fase=runtime, error y una traza multilínea con marcadores de inicio y fin, para confirmar que el contenido llega íntegro y sin truncar (FR-004 / SC-003 / ADR-007).")]
    public static string PocError()
    {
        string traza = string.Join('\n', new[]
        {
            TrazaInicio,
            "Autodesk.Revit.Exceptions.InvalidOperationException: No se puede completar la operacion. Revit devolvio el siguiente mensaje: 'No se pueden crear dos niveles con la misma cota.'",
            "   at Autodesk.Revit.DB.Level.Create(Document document, Double elevation)",
            "   at Autodesk.Revit.DB.Level.Create(Document document, Double elevation)",
            "   at Script.Execute(UIApplication uiapp) en D:\\RevitBridge\\snippets\\generated_7f3a9c.cs:linea 27",
            "   at RevitBridge.Core.RoslynExecutor.<ExecuteSnippetAsync>d__12.MoveNext() en D:\\RevitBridge\\src\\Core\\RoslynExecutor.cs:linea 118",
            "   at RevitBridge.Core.ExternalEventHandler.Execute(UIApplication app) en D:\\RevitBridge\\src\\Core\\ExternalEventHandler.cs:linea 64",
            "   at Autodesk.Revit.UI.ExternalEvent.Raise()",
            " ---> System.ArgumentException: El valor no se encuentra dentro del intervalo esperado.",
            "   Nombre del parametro: elevation",
            "   at Autodesk.Revit.DB.LevelUtils.ValidateElevation(Double elevacionEnPies)",
            "   at Autodesk.Revit.DB.Level.Create(Document document, Double elevation)",
            "   --- Fin del seguimiento de la pila de la excepcion interna ---",
            "   at Autodesk.Revit.DB.Level.Create(Document document, Double elevation)",
            "   at Script.Execute(UIApplication uiapp) en D:\\RevitBridge\\snippets\\generated_7f3a9c.cs:linea 27",
            "InnerException: System.ArgumentException: El valor no se encuentra dentro del intervalo esperado.",
            "   Nombre del parametro: elevation",
            "   at Autodesk.Revit.DB.LevelUtils.ValidateElevation(Double elevacionEnPies)",
            "   at Autodesk.Revit.DB.Level.Create(Document document, Double elevation)",
            "   at Script.Execute(UIApplication uiapp) en D:\\RevitBridge\\snippets\\generated_7f3a9c.cs:linea 27",
            "   at RevitBridge.Core.RoslynExecutor.<ExecuteSnippetAsync>d__12.MoveNext() en D:\\RevitBridge\\src\\Core\\RoslynExecutor.cs:linea 118",
            TrazaFin
        });

        var respuesta = new
        {
            ok = false,
            fase = "runtime",
            resultado = (object?)null,
            ids_creados = Array.Empty<int>(),
            error = "InvalidOperationException: No se puede completar la operacion. Revit devolvio el siguiente mensaje: 'No se pueden crear dos niveles con la misma cota.'",
            traza,
            duracion_ms = 340
        };

        return JsonSerializer.Serialize(respuesta, new JsonSerializerOptions { WriteIndented = true });
    }
}
