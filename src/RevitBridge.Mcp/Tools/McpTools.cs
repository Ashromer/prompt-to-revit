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

    [McpServerTool(Name = "execute", Title = "Ejecutar script C# en Revit")]
    [Description("Ejecuta codigo C# suministrado. Se abrira una ventana de aprobacion en Revit.")]
    public async Task<string> Execute(
        [Description("Codigo C# a ejecutar")] string codigo,
        [Description("Intencion de la ejecucion")] string intencion,
        CancellationToken cancellationToken)
    {
        var req = new ExecRequest(codigo, intencion);
        var payload = JsonSerializer.SerializeToElement(req);
        var peticion = new PeticionPipe(Operaciones.Exec, payload);

        var respuesta = await _pipeClient.EnviarAsync(peticion, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(15), cancellationToken);
        
        return JsonSerializer.Serialize(respuesta, JsonOptions);
    }

    [McpServerTool(Name = "run_command", Title = "Ejecutar comando pre-compilado de Revit")]
    [Description("Invoca un comando pre-compilado descubierto en el catalogo de Revit. Revisa la lista de comandos disponibles primero.")]
    public async Task<string> RunCommand(
        [Description("Nombre exacto del comando a ejecutar (ej: ExportarContextoMasivo, DuplicarVista, CrearMuroRecto)")] string nombreComando,
        [Description("Argumentos del comando en formato JSON dictionary. Escribe '{}' o null si el comando no tiene argumentos.")] string? argumentosJson,
        CancellationToken cancellationToken)
    {
        Dictionary<string, JsonElement>? args = null;
        if (!string.IsNullOrWhiteSpace(argumentosJson) && argumentosJson != "null")
        {
            try
            {
                args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentosJson);
            }
            catch { /* fallback a nulo si falla parseo */ }
        }

        // Requiere: using RevitBridge.Core; al inicio del archivo
        var req = new CommandRequest(nombreComando, args);
        var payload = JsonSerializer.SerializeToElement(req);
        var peticion = new PeticionPipe(Operaciones.Command, payload);

        var respuesta = await _pipeClient.EnviarAsync(peticion, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(15), cancellationToken);
        
        return JsonSerializer.Serialize(respuesta, JsonOptions);
    }
}
