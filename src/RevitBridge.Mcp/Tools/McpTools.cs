using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitBridge.Core;
using RevitBridge.Mcp.Bridge;

namespace RevitBridge.Mcp.Tools;

/// <summary>
/// Herramientas del puente MCP (F0.10).
/// Inyectan dependencias implícitamente por los atributos de ModelContextProtocol.
/// </summary>
[McpServerToolType]
public sealed class McpTools
{
    private readonly PipeClient _pipeClient;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public McpTools(PipeClient pipeClient)
    {
        _pipeClient = pipeClient;
    }

    [McpServerTool(Name = "query", Title = "Consultar el modelo de Revit")]
    [Description("Lee informacion del documento activo de Revit: niveles, tipos, seleccion, etc. No modifica nada.")]
    public async Task<string> Query(
        [Description("Tipo de consulta (niveles, tipos, seleccion, documento)")] string consulta,
        CancellationToken cancellationToken)
    {
        var req = new ConsultaRequest(consulta, null);
        var payload = JsonSerializer.SerializeToElement(req);
        var peticion = new PeticionPipe(Operaciones.Query, payload);

        // Timers generosos: 5s para conectar (Revit puede estar cargando), 15s para procesar.
        var respuesta = await _pipeClient.EnviarAsync(peticion, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), cancellationToken);
        
        return JsonSerializer.Serialize(respuesta, JsonOptions);
    }

    [McpServerTool(Name = "commands", Title = "Listar comandos de Revit compilados")]
    [Description("Lista los comandos pre-compilados disponibles en RevitBridge.Utils.")]
    public async Task<string> Commands(CancellationToken cancellationToken)
    {
        var peticion = new PeticionPipe(Operaciones.Commands);

        var respuesta = await _pipeClient.EnviarAsync(peticion, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), cancellationToken);
        
        return JsonSerializer.Serialize(respuesta, JsonOptions);
    }
}
