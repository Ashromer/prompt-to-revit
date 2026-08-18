using System.Text.Json;
using RevitBridge.Addin.Bridge;
using RevitBridge.Core;
using Xunit;

namespace RevitBridge.Tests.Addin;

/// <summary>
/// Mecanismo de encolar→esperar→responder de F0.5, sin tocar <c>ExternalEvent</c> ni ningún tipo
/// de <c>Autodesk.Revit.*</c>: <see cref="ExecutionQueue"/> es puro C# a propósito, así que se
/// prueba de punta a punta llamando a <see cref="ExecutionQueue.Procesar"/> directamente, tal
/// como lo haría <c>ExecutionQueueEventHandler.Execute</c> una vez Revit queda ocioso.
/// </summary>
public class ExecutionQueueTests
{
    [Fact]
    public void EjecutarAsync_No_Completa_Hasta_Que_Se_Llama_A_Procesar()
    {
        var cola = new ExecutionQueue();
        var tarea = cola.EjecutarAsync(new PeticionPipe(Operaciones.Query, default));

        Assert.False(tarea.IsCompleted);
        Assert.True(cola.HayPendientes);
    }

    [Fact]
    public void Procesar_Resuelve_La_Tarea_Con_El_Resultado_Del_Procesador()
    {
        var cola = new ExecutionQueue();
        var tarea = cola.EjecutarAsync(new PeticionPipe(Operaciones.Query, default));

        var esperado = new RespuestaOperacion(true, Fase.Ok, "resultado", Array.Empty<long>(), null, null, 5);
        var procesadas = cola.Procesar(_ => esperado);

        Assert.Equal(1, procesadas);
        Assert.True(tarea.IsCompletedSuccessfully);
        Assert.Equal(esperado, tarea.Result);
        Assert.False(cola.HayPendientes);
    }

    [Fact]
    public void Procesar_Sin_Pendientes_No_Hace_Nada()
    {
        var cola = new ExecutionQueue();

        var procesadas = cola.Procesar(ExecutionQueue.ProcesarPlaceholder);

        Assert.Equal(0, procesadas);
    }

    [Fact]
    public void Procesar_Drena_Varias_Peticiones_En_Orden_De_Llegada()
    {
        var cola = new ExecutionQueue();
        var tarea1 = cola.EjecutarAsync(new PeticionPipe(Operaciones.Query, default));
        var tarea2 = cola.EjecutarAsync(new PeticionPipe(Operaciones.Compile, default));

        var vistas = new List<string>();
        cola.Procesar(p =>
        {
            vistas.Add(p.Operacion);
            return ExecutionQueue.ProcesarPlaceholder(p);
        });

        Assert.Equal(new[] { Operaciones.Query, Operaciones.Compile }, vistas);
        Assert.True(tarea1.IsCompletedSuccessfully);
        Assert.True(tarea2.IsCompletedSuccessfully);
    }

    [Fact]
    public void Procesar_Convierte_Una_Excepcion_Del_Procesador_En_Respuesta_Fase_Runtime()
    {
        var cola = new ExecutionQueue();
        var tarea = cola.EjecutarAsync(new PeticionPipe(Operaciones.Exec, default));

        cola.Procesar(_ => throw new InvalidOperationException("algo falló"));

        Assert.True(tarea.IsCompletedSuccessfully);
        Assert.False(tarea.Result.Ok);
        Assert.Equal(Fase.Runtime, tarea.Result.Fase);
        Assert.Contains("InvalidOperationException", tarea.Result.Error);
        Assert.Contains("algo falló", tarea.Result.Traza);
    }

    [Fact]
    public void ProcesarPlaceholder_Devuelve_Eco_De_La_Operacion()
    {
        var respuesta = ExecutionQueue.ProcesarPlaceholder(new PeticionPipe(Operaciones.Commands, default));

        Assert.True(respuesta.Ok);
        Assert.Equal(Fase.Ok, respuesta.Fase);
    }

    [Fact]
    public void EjecutarAsync_Dispara_El_Evento_PeticionEncolada()
    {
        var cola = new ExecutionQueue();
        var disparos = 0;
        cola.PeticionEncolada += () => disparos++;

        cola.EjecutarAsync(new PeticionPipe(Operaciones.Query, default));
        cola.EjecutarAsync(new PeticionPipe(Operaciones.Query, default));

        Assert.Equal(2, disparos);
    }

    [Fact]
    public async Task EjecutarAsync_Se_Cancela_Si_El_Token_Se_Cancela_Antes_De_Procesar()
    {
        var cola = new ExecutionQueue();
        using var cts = new CancellationTokenSource();

        var tarea = cola.EjecutarAsync(new PeticionPipe(Operaciones.Query, default), cts.Token);
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() => tarea);
    }

    [Fact]
    public void PeticionPipe_Datos_Se_Puede_Deserializar_Al_Tipo_Concreto_De_La_Operacion()
    {
        var datos = JsonSerializer.SerializeToElement(new ConsultaRequest("niveles del proyecto"));
        var peticion = new PeticionPipe(Operaciones.Query, datos);

        var consulta = peticion.Datos.Deserialize<ConsultaRequest>();

        Assert.NotNull(consulta);
        Assert.Equal("niveles del proyecto", consulta!.Consulta);
    }
}
