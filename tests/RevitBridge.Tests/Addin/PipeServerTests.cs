using System.IO.Pipes;
using System.Text.Json;
using RevitBridge.Addin.Bridge;
using RevitBridge.Core;
using Xunit;

namespace RevitBridge.Tests.Addin;

/// <summary>
/// specs/tech-spec.md §Testing Strategy, fila PipeServer: serialización, bloqueo hasta resultado,
/// comportamiento ante cliente desconectado, con un ejecutor falso. Usa named pipes REALES
/// (Windows, sin Revit) — no hace falta simular el transporte, solo el ejecutor.
/// </summary>
public class PipeServerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private static string NombrePipeDeTest() => $"RevitBridge.Tests.{Guid.NewGuid():N}";

    private sealed class EjecutorFalso : IPeticionExecutor
    {
        private readonly Func<PeticionPipe, CancellationToken, Task<RespuestaOperacion>> _comportamiento;

        public EjecutorFalso(Func<PeticionPipe, CancellationToken, Task<RespuestaOperacion>> comportamiento)
        {
            _comportamiento = comportamiento;
        }

        public PeticionPipe? UltimaPeticion { get; private set; }

        public Task<RespuestaOperacion> EjecutarAsync(PeticionPipe peticion, CancellationToken cancellationToken = default)
        {
            UltimaPeticion = peticion;
            return _comportamiento(peticion, cancellationToken);
        }
    }

    private static async Task<string> EnviarYRecibirAsync(string pipeName, PeticionPipe peticion, int timeoutMs = 5000)
    {
        using var cliente = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await cliente.ConnectAsync(timeoutMs);

        var json = JsonSerializer.Serialize(peticion, JsonOptions);
        await PipeFraming.EscribirMensajeAsync(cliente, json);

        using var cts = new CancellationTokenSource(timeoutMs);
        var respuesta = await PipeFraming.LeerMensajeAsync(cliente, cts.Token);
        return respuesta ?? throw new IOException("Sin respuesta del servidor.");
    }

    [Fact]
    public async Task Responde_Con_El_Resultado_Real_Del_Ejecutor()
    {
        var esperada = new RespuestaOperacion(true, Fase.Ok, "ok-resultado", Array.Empty<long>(), null, null, 3);
        var ejecutor = new EjecutorFalso((_, _) => Task.FromResult(esperada));

        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, ejecutor);
        server.Iniciar();

        var peticion = new PeticionPipe(Operaciones.Query, JsonSerializer.SerializeToElement(new ConsultaRequest("niveles")));
        var respuestaJson = await EnviarYRecibirAsync(pipeName, peticion);
        var respuesta = JsonSerializer.Deserialize<RespuestaOperacion>(respuestaJson, JsonOptions);

        // No comparar el record entero: "resultado" deserializa como JsonElement, no como el
        // string original (mismo motivo que ContratoRoundTripTests). Comparar campo a campo.
        Assert.NotNull(respuesta);
        Assert.True(respuesta!.Ok);
        Assert.Equal(Fase.Ok, respuesta.Fase);
        Assert.Equal("ok-resultado", respuesta.Resultado!.ToString());
        Assert.Equal(3, respuesta.DuracionMs);
        Assert.Equal(Operaciones.Query, ejecutor.UltimaPeticion!.Operacion);

        server.Detener();
    }

    [Fact]
    public async Task Bloquea_Hasta_Que_El_Ejecutor_Termina_No_Responde_Antes()
    {
        var puedeTerminar = new TaskCompletionSource();
        var ejecutor = new EjecutorFalso(async (_, ct) =>
        {
            await puedeTerminar.Task.WaitAsync(ct);
            return new RespuestaOperacion(true, Fase.Ok, "tarde", Array.Empty<long>(), null, null, 1);
        });

        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, ejecutor);
        server.Iniciar();

        using var cliente = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await cliente.ConnectAsync(5000);
        await PipeFraming.EscribirMensajeAsync(cliente, JsonSerializer.Serialize(new PeticionPipe(Operaciones.Exec, default), JsonOptions));

        using var ctsCorto = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => PipeFraming.LeerMensajeAsync(cliente, ctsCorto.Token));

        puedeTerminar.SetResult();

        using var ctsLargo = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var respuestaJson = await PipeFraming.LeerMensajeAsync(cliente, ctsLargo.Token);
        var respuesta = JsonSerializer.Deserialize<RespuestaOperacion>(respuestaJson!, JsonOptions);

        Assert.Equal("tarde", respuesta!.Resultado!.ToString());

        server.Detener();
    }

    [Fact]
    public async Task Cliente_Desconectado_A_Medias_No_Tumba_El_Servidor_Y_Acepta_La_Siguiente_Conexion()
    {
        var ejecutor = new EjecutorFalso((_, _) =>
            Task.FromResult(new RespuestaOperacion(true, Fase.Ok, "sigo-vivo", Array.Empty<long>(), null, null, 1)));

        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, ejecutor);
        server.Iniciar();

        // Conecta y cierra sin enviar nada: el servidor debe descartar la conexión sin caerse.
        using (var clienteRoto = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await clienteRoto.ConnectAsync(5000);
        }

        // Una segunda conexión, normal, debe seguir funcionando.
        var peticion = new PeticionPipe(Operaciones.Query, default);
        var respuestaJson = await EnviarYRecibirAsync(pipeName, peticion);
        var respuesta = JsonSerializer.Deserialize<RespuestaOperacion>(respuestaJson, JsonOptions);

        Assert.Equal("sigo-vivo", respuesta!.Resultado!.ToString());

        server.Detener();
    }

    [Fact]
    public async Task Excepcion_Del_Ejecutor_Se_Devuelve_Como_Respuesta_Fase_Runtime_No_Como_Crash()
    {
        var ejecutor = new EjecutorFalso((_, _) => throw new InvalidOperationException("fallo simulado"));

        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, ejecutor);
        server.Iniciar();

        var respuestaJson = await EnviarYRecibirAsync(pipeName, new PeticionPipe(Operaciones.Exec, default));
        var respuesta = JsonSerializer.Deserialize<RespuestaOperacion>(respuestaJson, JsonOptions);

        Assert.False(respuesta!.Ok);
        Assert.Equal(Fase.Runtime, respuesta.Fase);
        Assert.Contains("InvalidOperationException", respuesta.Error);

        server.Detener();
    }

    [Fact]
    public void NombrePorDefecto_Es_Fijo_Y_Coincide_Con_El_Del_Puente_Mcp()
    {
        // Debe ser el mismo valor fijo que usa RevitBridge.Mcp/Program.cs por defecto
        // ("RevitBridgePipe") — de lo contrario el cliente nunca conecta sin REVITBRIDGE_PIPE
        // puesta a mano en los dos procesos (bug real, 2026-08-19).
        var nombre = PipeServer.NombrePorDefecto();

        Assert.Equal("RevitBridgePipe", nombre);
    }
}
