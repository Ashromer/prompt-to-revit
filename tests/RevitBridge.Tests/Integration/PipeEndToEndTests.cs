using System.Text.Json;
using RevitBridge.Addin.Bridge;
using RevitBridge.Core;
using RevitBridge.Mcp.Bridge;
using Xunit;

namespace RevitBridge.Tests.Integration;

/// <summary>
/// Ida y vuelta completa sin Revit (specs/tech-spec.md §Testing Strategy, "Integration Tests"):
/// <see cref="PipeServer"/> (addin) + <see cref="PipeClient"/> (puente) + <see cref="ExecutionQueue"/>
/// real. Nadie aquí simula <c>ExternalEvent</c>: en vez de eso, un hilo aparte llama a
/// <c>ExecutionQueue.Procesar</c> tras un pequeño retraso, tal como haría
/// <c>ExecutionQueueEventHandler.Execute</c> cuando Revit queda ocioso — así se prueba el
/// mecanismo real de "encolar → esperar → responder" de F0.5 de punta a punta.
/// </summary>
public class PipeEndToEndTests
{
    private static string NombrePipeDeTest() => $"RevitBridge.Tests.E2E.{Guid.NewGuid():N}";

    [Fact]
    public async Task Peticion_Del_Cliente_Llega_A_La_Cola_Y_Vuelve_Como_Respuesta_Real()
    {
        var cola = new ExecutionQueue();
        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, cola);
        server.Iniciar();

        // Simula ExternalEvent.Raise() -> Execute(app) -> cola.Procesar(...): se dispara en un
        // hilo aparte cuando hay algo pendiente, igual que haría el addin real.
        cola.PeticionEncolada += () => Task.Run(() => cola.Procesar(ExecutionQueue.ProcesarPlaceholder));

        var cliente = new PipeClient(pipeName);
        var peticion = new PeticionPipe(Operaciones.Query, JsonSerializer.SerializeToElement(new ConsultaRequest("niveles")));

        var respuesta = await cliente.EnviarAsync(peticion, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        Assert.True(respuesta.Ok);
        Assert.Equal(Fase.Ok, respuesta.Fase);

        server.Detener();
    }

    [Fact]
    public async Task Sin_Servidor_Escuchando_El_Cliente_Falla_Por_Timeout_De_Conexion_No_Se_Cuelga()
    {
        var cliente = new PipeClient(NombrePipeDeTest()); // nadie escucha este nombre

        var excepcion = await Assert.ThrowsAsync<TimeoutException>(() =>
            cliente.EnviarAsync(
                new PeticionPipe(Operaciones.Query, default),
                TimeSpan.FromMilliseconds(300),
                TimeSpan.FromSeconds(5)));

        Assert.Contains("Revit cerrado", excepcion.Message);
    }

    [Fact]
    public async Task Varias_Peticiones_Secuenciales_Se_Procesan_Todas()
    {
        var cola = new ExecutionQueue();
        var pipeName = NombrePipeDeTest();
        using var server = new PipeServer(pipeName, cola);
        server.Iniciar();

        cola.PeticionEncolada += () => Task.Run(() => cola.Procesar(ExecutionQueue.ProcesarPlaceholder));

        var cliente = new PipeClient(pipeName);

        for (var i = 0; i < 3; i++)
        {
            var respuesta = await cliente.EnviarAsync(
                new PeticionPipe(Operaciones.Compile, default),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5));

            Assert.True(respuesta.Ok);
        }

        server.Detener();
    }
}
