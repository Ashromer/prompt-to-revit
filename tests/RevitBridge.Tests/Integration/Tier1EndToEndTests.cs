using System.Diagnostics;
using System.Text.Json;
using RevitBridge.Addin.Bridge;
using RevitBridge.Core;
using RevitBridge.Core.Compiler;
using RevitBridge.Mcp.Bridge;
using Xunit;

namespace RevitBridge.Tests.Integration;

public class Tier1EndToEndTests
{
    private static string NombrePipeDeTest() => $"RevitBridge.Tests.Tier1.{Guid.NewGuid():N}";

    private RespuestaOperacion ProcesarFalso(PeticionPipe peticion, bool aprobar, bool lanzarErrorRuntime)
    {
        var sw = Stopwatch.StartNew();
        if (peticion.Operacion == Operaciones.Compile)
        {
            var req = JsonSerializer.Deserialize<CompileRequest>(peticion.Datos.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req == null) return new RespuestaOperacion(false, Fase.Runtime, null, Array.Empty<long>(), "Payload nulo", null, sw.ElapsedMilliseconds);
            
            var compiler = new RoslynCompiler();
            var result = compiler.Compile(req.Fuente);
            if (!result.Success)
            {
                var errores = result.Diagnostics.Select(d => d.ToString()).ToList();
                return new RespuestaOperacion(false, Fase.Compilacion, errores, Array.Empty<long>(), "Error de compilación", string.Join("\n", errores), sw.ElapsedMilliseconds);
            }
            return new RespuestaOperacion(true, Fase.Ok, "Compilación exitosa", Array.Empty<long>(), null, null, sw.ElapsedMilliseconds);
        }
        if (peticion.Operacion == Operaciones.Exec)
        {
            var req = JsonSerializer.Deserialize<ExecRequest>(peticion.Datos.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req == null) return new RespuestaOperacion(false, Fase.Runtime, null, Array.Empty<long>(), "Payload nulo", null, sw.ElapsedMilliseconds);
            
            var compiler = new RoslynCompiler();
            var result = compiler.Compile(req.Fuente);
            if (!result.Success)
            {
                var errores = result.Diagnostics.Select(d => d.ToString()).ToList();
                return new RespuestaOperacion(false, Fase.Compilacion, errores, Array.Empty<long>(), "Error de compilación", string.Join("\n", errores), sw.ElapsedMilliseconds);
            }

            if (!aprobar)
            {
                return new RespuestaOperacion(false, Fase.Runtime, null, Array.Empty<long>(), "Ejecución rechazada por el usuario.", null, sw.ElapsedMilliseconds);
            }

            if (lanzarErrorRuntime)
            {
                return new RespuestaOperacion(false, Fase.Runtime, null, Array.Empty<long>(), "Fallo simulado en runtime", "Traza simulada", sw.ElapsedMilliseconds);
            }

            return new RespuestaOperacion(true, Fase.Ok, "Ejecución finalizada con éxito.", Array.Empty<long>(), null, null, sw.ElapsedMilliseconds);
        }
        if (peticion.Operacion == Operaciones.Rollback)
        {
            return new RespuestaOperacion(true, Fase.Ok, "Rollback ejecutado con éxito.", Array.Empty<long>(), null, null, sw.ElapsedMilliseconds);
        }

        return new RespuestaOperacion(false, Fase.Runtime, null, Array.Empty<long>(), "No soportado", null, sw.ElapsedMilliseconds);
    }

    [Fact]
    public async Task Camino1_DryRun_Con_Diagnosticos_Error_De_Sintaxis()
    {
        var cola = new ExecutionQueue();
        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, cola);
        server.Iniciar();

        cola.PeticionEncolada += () => Task.Run(() => cola.Procesar(p => ProcesarFalso(p, aprobar: true, lanzarErrorRuntime: false)));

        var cliente = new PipeClient(pipeName);
        var codigo = "public class Script { public void Execute() { faltan punto y coma } }";
        var peticion = new PeticionPipe(Operaciones.Compile, JsonSerializer.SerializeToElement(new CompileRequest(codigo)));

        var respuesta = await cliente.EnviarAsync(peticion, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

        Assert.False(respuesta.Ok);
        Assert.Equal(Fase.Compilacion, respuesta.Fase);
        Assert.Contains("Error de compilación", respuesta.Error);

        server.Detener();
    }

    [Fact]
    public async Task Camino2_Snippet_Rechazado_Por_Filtro_Sintactico()
    {
        var cola = new ExecutionQueue();
        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, cola);
        server.Iniciar();

        cola.PeticionEncolada += () => Task.Run(() => cola.Procesar(p => ProcesarFalso(p, aprobar: true, lanzarErrorRuntime: false)));

        var cliente = new PipeClient(pipeName);
        var codigo = "public class Script { public void Execute() { object doc = null; doc.Delete(123); } }";
        var peticion = new PeticionPipe(Operaciones.Exec, JsonSerializer.SerializeToElement(new ExecRequest(codigo, "Test")));

        var respuesta = await cliente.EnviarAsync(peticion, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

        Assert.False(respuesta.Ok);
        Assert.Equal(Fase.Compilacion, respuesta.Fase);
        Assert.Contains("RB001", respuesta.Traza);

        server.Detener();
    }

    [Fact]
    public async Task Camino3_Aprobacion_Concedida_Ejecucion_Exitosa()
    {
        var cola = new ExecutionQueue();
        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, cola);
        server.Iniciar();

        cola.PeticionEncolada += () => Task.Run(() => cola.Procesar(p => ProcesarFalso(p, aprobar: true, lanzarErrorRuntime: false)));

        var cliente = new PipeClient(pipeName);
        var codigo = "public class Script { public static void Execute(object uiapp) { } }";
        var peticion = new PeticionPipe(Operaciones.Exec, JsonSerializer.SerializeToElement(new ExecRequest(codigo, "Test exitoso")));

        var respuesta = await cliente.EnviarAsync(peticion, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

        Assert.True(respuesta.Ok);
        Assert.Equal(Fase.Ok, respuesta.Fase);
        Assert.Equal("Ejecución finalizada con éxito.", respuesta.Resultado?.ToString());

        server.Detener();
    }

    [Fact]
    public async Task Camino4_Aprobacion_Rechazada_O_Caducada()
    {
        var cola = new ExecutionQueue();
        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, cola);
        server.Iniciar();

        cola.PeticionEncolada += () => Task.Run(() => cola.Procesar(p => ProcesarFalso(p, aprobar: false, lanzarErrorRuntime: false)));

        var cliente = new PipeClient(pipeName);
        var codigo = "public class Script { public static void Execute(object uiapp) { } }";
        var peticion = new PeticionPipe(Operaciones.Exec, JsonSerializer.SerializeToElement(new ExecRequest(codigo, "Test rechazo")));

        var respuesta = await cliente.EnviarAsync(peticion, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

        Assert.False(respuesta.Ok);
        Assert.Equal(Fase.Runtime, respuesta.Fase);
        Assert.Contains("rechazada", respuesta.Error);

        server.Detener();
    }

    [Fact]
    public async Task Camino5_Fallo_Runtime_Traza_Fase_Correcta()
    {
        var cola = new ExecutionQueue();
        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, cola);
        server.Iniciar();

        cola.PeticionEncolada += () => Task.Run(() => cola.Procesar(p => ProcesarFalso(p, aprobar: true, lanzarErrorRuntime: true)));

        var cliente = new PipeClient(pipeName);
        var codigo = "public class Script { public static void Execute(object uiapp) { } }";
        var peticion = new PeticionPipe(Operaciones.Exec, JsonSerializer.SerializeToElement(new ExecRequest(codigo, "Test fallo runtime")));

        var respuesta = await cliente.EnviarAsync(peticion, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

        Assert.False(respuesta.Ok);
        Assert.Equal(Fase.Runtime, respuesta.Fase);
        Assert.Contains("Fallo simulado", respuesta.Error);

        server.Detener();
    }

    [Fact]
    public async Task Camino6_Operacion_Rollback()
    {
        var cola = new ExecutionQueue();
        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, cola);
        server.Iniciar();

        cola.PeticionEncolada += () => Task.Run(() => cola.Procesar(p => ProcesarFalso(p, aprobar: true, lanzarErrorRuntime: false)));

        var cliente = new PipeClient(pipeName);
        var peticion = new PeticionPipe(Operaciones.Rollback, JsonSerializer.SerializeToElement(new RollbackRequest(new long[] { 1, 2 })));

        var respuesta = await cliente.EnviarAsync(peticion, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));

        Assert.True(respuesta.Ok);
        Assert.Equal(Fase.Ok, respuesta.Fase);
        Assert.Equal("Rollback ejecutado con éxito.", respuesta.Resultado?.ToString());

        server.Detener();
    }
}
