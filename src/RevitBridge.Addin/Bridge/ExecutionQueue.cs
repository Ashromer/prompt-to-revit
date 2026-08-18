using System.Collections.Concurrent;
using RevitBridge.Core;

namespace RevitBridge.Addin.Bridge;

/// <summary>
/// Cola de peticiones a la espera de ejecutarse en el hilo de la API vía <c>ExternalEvent</c>
/// (F0.5, DOCUMENTACION.md §5.B.5). Puro C#: no referencia ningún tipo de <c>Autodesk.Revit.*</c>,
/// así que es testeable sin Revit instalado (DOCUMENTACION.md §8, "la costura de abstracción es lo
/// que hace testable el proyecto"). El adaptador que sí toca <c>ExternalEvent</c> es
/// <see cref="ExecutionQueueEventHandler"/>, deliberadamente trivial y sin lógica propia que
/// merezca test: toda la lógica de cola/espera vive aquí.
/// </summary>
public sealed class ExecutionQueue : IPeticionExecutor
{
    private readonly ConcurrentQueue<Pendiente> _pendientes = new();

    /// <summary>
    /// Se dispara cada vez que se encola una petición nueva. El arranque del addin (F0.4) la
    /// engancha para llamar a <c>ExternalEvent.Raise()</c>: esta clase no conoce esa API.
    /// </summary>
    public event Action? PeticionEncolada;

    public bool HayPendientes => !_pendientes.IsEmpty;

    public Task<RespuestaOperacion> EjecutarAsync(PeticionPipe peticion, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<RespuestaOperacion>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (cancellationToken.CanBeCanceled)
        {
            var registro = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            tcs.Task.ContinueWith(_ => registro.Dispose(), TaskScheduler.Default);
        }

        _pendientes.Enqueue(new Pendiente(peticion, tcs));
        PeticionEncolada?.Invoke();

        return tcs.Task;
    }

    /// <summary>
    /// Drena la cola aplicando <paramref name="procesador"/> a cada petición pendiente y
    /// resolviendo su <c>TaskCompletionSource</c>. Se llama desde el hilo de la API (dentro de
    /// <c>IExternalEventHandler.Execute</c>, ya en contexto válido). Devuelve el número de
    /// peticiones procesadas.
    /// </summary>
    public int Procesar(Func<PeticionPipe, RespuestaOperacion> procesador)
    {
        var procesadas = 0;

        while (_pendientes.TryDequeue(out var pendiente))
        {
            RespuestaOperacion respuesta;
            try
            {
                respuesta = procesador(pendiente.Peticion);
            }
            catch (Exception ex)
            {
                // Varias excepciones de la API de Revit llegan con Message vacío: el tipo y la
                // traza completa son el respaldo (DOCUMENTACION.md §4).
                respuesta = new RespuestaOperacion(
                    Ok: false,
                    Fase: Fase.Runtime,
                    Resultado: null,
                    IdsCreados: Array.Empty<long>(),
                    Error: $"{ex.GetType().Name}: {ex.Message}",
                    Traza: ex.ToString(),
                    DuracionMs: 0);
            }

            pendiente.Tcs.TrySetResult(respuesta);
            procesadas++;
        }

        return procesadas;
    }

    /// <summary>
    /// Procesador por defecto para este lote: un eco/placeholder. La ejecución real contra el
    /// documento (adaptador <c>RevitContext</c>, F0.6) sustituye esto sin tocar <see cref="ExecutionQueue"/>.
    /// </summary>
    public static RespuestaOperacion ProcesarPlaceholder(PeticionPipe peticion) => new(
        Ok: true,
        Fase: Fase.Ok,
        Resultado: new { eco = peticion.Operacion },
        IdsCreados: Array.Empty<long>(),
        Error: null,
        Traza: null,
        DuracionMs: 0);

    private sealed record Pendiente(PeticionPipe Peticion, TaskCompletionSource<RespuestaOperacion> Tcs);
}
