using System.Text.Json;
using RevitBridge.Addin.Bridge;
using RevitBridge.Core;
using RevitBridge.Mcp.Bridge;
using RevitBridge.Mcp.Tools;
using Xunit;

namespace RevitBridge.Tests.Integration;

/// <summary>
/// Prueba E2E del Tier 0 (F0.11 Healthcheck).
/// Levanta el puente MCP "casi real" (la clase de herramientas) contra un PipeServer local
/// con ejecutor falso (sin Revit). Invoca la consulta y el catálogo y verifica la respuesta.
/// </summary>
public class McpBridgeEndToEndTests
{
    private static string NombrePipeDeTest() => $"RevitBridge.Tests.Mcp.{Guid.NewGuid():N}";

    [Fact]
    public async Task Herramienta_Query_Llega_A_La_Cola_Y_Responde()
    {
        var cola = new ExecutionQueue();
        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, cola);
        server.Iniciar();

        // Ejecutor falso que emula Revit
        cola.PeticionEncolada += () => Task.Run(() => cola.Procesar(ExecutionQueue.ProcesarPlaceholder));

        var pipeClient = new PipeClient(pipeName);
        var tools = new McpTools(pipeClient);

        // Prueba de lectura (F0.8)
        var jsonRespuesta = await tools.Query("niveles", CancellationToken.None);

        Assert.NotNull(jsonRespuesta);
        var res = JsonSerializer.Deserialize<RespuestaOperacion>(jsonRespuesta, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        Assert.NotNull(res);
        Assert.True(res.Ok);
        Assert.Equal(Fase.Ok, res.Fase);

        server.Detener();
    }
    
    [Fact]
    public async Task Herramienta_Commands_Llega_A_La_Cola_Y_Responde()
    {
        var cola = new ExecutionQueue();
        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, cola);
        server.Iniciar();

        // Ejecutor falso que emula Revit
        cola.PeticionEncolada += () => Task.Run(() => cola.Procesar(ExecutionQueue.ProcesarPlaceholder));

        var pipeClient = new PipeClient(pipeName);
        var tools = new McpTools(pipeClient);

        // Prueba de catálogo (F0.9)
        var jsonRespuesta = await tools.Commands(CancellationToken.None);

        Assert.NotNull(jsonRespuesta);
        var res = JsonSerializer.Deserialize<RespuestaOperacion>(jsonRespuesta, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        Assert.NotNull(res);
        Assert.True(res.Ok);
        Assert.Equal(Fase.Ok, res.Fase);

        server.Detener();
    }

    [Fact]
    public async Task Herramienta_Query_Devuelve_Error_Controlado_Si_Falla_La_Ejecucion()
    {
        var cola = new ExecutionQueue();
        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, cola);
        server.Iniciar();

        // Ejecutor falso que emula un fallo interno en Revit (ej. nulo o excepción de la API)
        cola.PeticionEncolada += () => Task.Run(() => cola.Procesar(p => 
        {
            throw new InvalidOperationException("Fallo de prueba en la API simulada");
        }));

        var pipeClient = new PipeClient(pipeName);
        var tools = new McpTools(pipeClient);

        var jsonRespuesta = await tools.Query("niveles", CancellationToken.None);

        Assert.NotNull(jsonRespuesta);
        var res = JsonSerializer.Deserialize<RespuestaOperacion>(jsonRespuesta, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        Assert.NotNull(res);
        Assert.False(res.Ok);
        Assert.Equal(Fase.Runtime, res.Fase);
        Assert.Contains("InvalidOperationException", res.Error);
        Assert.Contains("Fallo de prueba en la API simulada", res.Error);
        Assert.NotNull(res.Traza);

        server.Detener();
    }
}
