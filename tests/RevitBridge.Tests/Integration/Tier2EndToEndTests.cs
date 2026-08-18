using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using RevitBridge.Addin.Bridge;
using RevitBridge.Core;
using RevitBridge.Mcp.Bridge;
using RevitBridge.Mcp.Tools;
using RevitBridge.Utils;
using Xunit;

namespace RevitBridge.Tests.Integration;

/// <summary>
/// Criterio de cierre de Tier 2 (specs/roadmap.md): "test E2E automatizado que invoca un comando
/// compilado del catálogo a través del puente, verifica que el nombre coincide entre ambos lados".
///
/// El ejecutor falso reproduce el MISMO mecanismo de despacho que <c>RevitContext.Procesar</c>
/// (rama <c>Operaciones.Command</c>): reflexión sobre métodos estáticos marcados con
/// <see cref="ComandoRevitAttribute"/>, match por nombre exacto -- aquí sobre un comando de prueba
/// sin tipos de Revit, para poder recorrer todo el camino (McpTools -> PipeClient -> PipeServer ->
/// cola -> despacho por atributo -> invocación) sin Revit. No sustituye la verificación en Revit
/// vivo de los comandos reales del catálogo (F2.4/F2.5 siguen viviendo en la única capa sin red de
/// seguridad, RevitContext), pero sí prueba de verdad el mecanismo de coincidencia de nombres que
/// F2.1 exige ("Nombres idénticos entre puente y addin, o el modelo no los encuentra").
/// </summary>
public class Tier2EndToEndTests
{
    private static string NombrePipeDeTest() => $"RevitBridge.Tests.Tier2.{Guid.NewGuid():N}";

    private static class ComandoDePrueba
    {
        [ComandoRevit("TestEcho")]
        public static object TestEcho(string mensaje) => new { eco = mensaje };
    }

    private static RespuestaOperacion ProcesarComandoFalso(PeticionPipe peticion)
    {
        var sw = Stopwatch.StartNew();

        var req = JsonSerializer.Deserialize<CommandRequest>(peticion.Datos.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (req is null)
        {
            return new RespuestaOperacion(false, Fase.Runtime, null, Array.Empty<long>(), "Payload nulo", null, sw.ElapsedMilliseconds);
        }

        var metodos = typeof(ComandoDePrueba)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttribute<ComandoRevitAttribute>()?.Nombre.Equals(req.Nombre, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (metodos.Count == 0)
        {
            return new RespuestaOperacion(false, Fase.Runtime, null, Array.Empty<long>(), $"Comando '{req.Nombre}' no encontrado.", null, sw.ElapsedMilliseconds);
        }

        var metodo = metodos[0];
        var parametros = metodo.GetParameters();
        var args = new object?[parametros.Length];
        for (var i = 0; i < parametros.Length; i++)
        {
            var p = parametros[i];
            if (req.Argumentos is JsonElement argsDict && argsDict.ValueKind == JsonValueKind.Object && argsDict.TryGetProperty(p.Name ?? "", out var je))
            {
                args[i] = JsonSerializer.Deserialize(je.GetRawText(), p.ParameterType);
            }
            else
            {
                args[i] = p.HasDefaultValue ? p.DefaultValue : null;
            }
        }

        try
        {
            var resultado = metodo.Invoke(null, args);
            return new RespuestaOperacion(true, Fase.Ok, resultado, Array.Empty<long>(), null, null, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new RespuestaOperacion(false, Fase.Runtime, null, Array.Empty<long>(), ex.InnerException?.Message ?? ex.Message, ex.ToString(), sw.ElapsedMilliseconds);
        }
    }

    [Fact]
    public async Task Comando_Con_Nombre_Coincidente_Se_Encuentra_Y_Se_Invoca()
    {
        var cola = new ExecutionQueue();
        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, cola);
        server.Iniciar();

        cola.PeticionEncolada += () => Task.Run(() => cola.Procesar(ProcesarComandoFalso));

        var pipeClient = new PipeClient(pipeName);
        var tools = new McpTools(pipeClient);

        var jsonRespuesta = await tools.RunCommand("TestEcho", "{\"mensaje\":\"hola\"}", CancellationToken.None);

        var res = JsonSerializer.Deserialize<RespuestaOperacion>(jsonRespuesta, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(res);
        Assert.True(res.Ok);
        Assert.Equal(Fase.Ok, res.Fase);
        Assert.Contains("hola", res.Resultado!.ToString());

        server.Detener();
    }

    [Fact]
    public async Task Comando_Con_Nombre_Que_No_Coincide_Devuelve_Error_Controlado_No_Crash()
    {
        // F2.1: "Nombres idénticos entre puente y addin, o el modelo no los encuentra" -- este es
        // exactamente el caso de desajuste, y debe volver como fallo de ejecución normal (Fase.Runtime
        // dentro de una respuesta correcta, ADR-007), no como excepción sin capturar ni timeout.
        var cola = new ExecutionQueue();
        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, cola);
        server.Iniciar();

        cola.PeticionEncolada += () => Task.Run(() => cola.Procesar(ProcesarComandoFalso));

        var pipeClient = new PipeClient(pipeName);
        var tools = new McpTools(pipeClient);

        var jsonRespuesta = await tools.RunCommand("ComandoQueNoExiste", null, CancellationToken.None);

        var res = JsonSerializer.Deserialize<RespuestaOperacion>(jsonRespuesta, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(res);
        Assert.False(res.Ok);
        Assert.Contains("no encontrado", res.Error);

        server.Detener();
    }
}
