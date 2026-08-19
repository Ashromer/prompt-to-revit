using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using RevitBridge.Core;

namespace RevitBridge.Addin.Bridge;

/// <summary>
/// Servidor del named pipe (F0.3, DOCUMENTACION.md §2/§5.E.19). Escucha en su propio hilo, nunca
/// en el hilo de la API — ni siquiera indirectamente: solo conoce <see cref="IPeticionExecutor"/>,
/// no <c>ExternalEvent</c> ni ningún tipo de <c>Autodesk.Revit.*</c>. ACL restringida al usuario
/// actual (ADR-002): sin puerto, sin token. Bloquea la conexión hasta que el ejecutor devuelve el
/// resultado real; nunca responde "aceptado" sin más.
/// </summary>
public sealed class PipeServer : IDisposable
{
    private readonly string _pipeName;
    private readonly IPeticionExecutor _executor;
    private readonly CancellationTokenSource _cts = new();
    private static readonly JsonSerializerOptions JsonOptions = new();

    private Thread? _hilo;

    public PipeServer(string pipeName, IPeticionExecutor executor)
    {
        _pipeName = pipeName;
        _executor = executor;
    }

    /// <summary>
    /// Nombre por defecto del pipe. Fijo (no incluye el PID de Revit): el lado del puente
    /// (<c>RevitBridge.Mcp/Program.cs</c>) usa el mismo valor fijo por defecto, y ambos procesos
    /// arrancan en momentos distintos sin forma de acordar un PID de antemano — incluirlo aquí
    /// rompía la conexión en TODA sesión sin <c>REVITBRIDGE_PIPE</c> puesta a mano en los dos
    /// procesos (bug real, encontrado 2026-08-19: el cliente fallaba con "Revit cerrado o addin
    /// no cargado" incluso con Revit y documento abiertos). Varias instancias de Revit a la vez
    /// SÍ necesitan <c>REVITBRIDGE_PIPE</c> distinta cada una (specs/tech-spec.md §Deployment) —
    /// ya no es automático, es la única vía ahora.
    /// </summary>
    public static string NombrePorDefecto() => "RevitBridgePipe";

    public void Iniciar()
    {
        if (_hilo is not null)
        {
            return;
        }

        _hilo = new Thread(BucleEscucha) { IsBackground = true, Name = "RevitBridge.PipeServer" };
        _hilo.Start();
    }

    public void Detener()
    {
        _cts.Cancel();
        _hilo?.Join(TimeSpan.FromSeconds(2));
    }

    private void BucleEscucha()
    {
        while (!_cts.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = CrearPipeConAcl(_pipeName);
                pipe.WaitForConnectionAsync(_cts.Token).GetAwaiter().GetResult();

                AtenderConexionAsync(pipe, _cts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                // Instancia de pipe rota (p. ej. cliente que se desconecta a medio handshake):
                // se descarta esta conexión y se reintenta con una instancia nueva.
            }
            catch (Exception)
            {
                // Cualquier otra excepción no controlada no debe crashear el proceso de Revit entero.
                // Se descarta y el bucle while(true) levantará un nuevo pipe.
                Thread.Sleep(500); // Pequeña pausa para no saturar la CPU si es un error constante
            }
            finally
            {
                pipe?.Dispose();
            }
        }
    }

    private async Task AtenderConexionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        string? json;
        try
        {
            json = await PipeFraming.LeerMensajeAsync(pipe, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (IOException)
        {
            return; // Cliente desconectado durante la lectura: nada que responder.
        }

        if (json is null)
        {
            return; // Cliente desconectado antes de completar el mensaje.
        }

        RespuestaOperacion respuesta;
        try
        {
            var peticion = JsonSerializer.Deserialize<PeticionPipe>(json, JsonOptions)
                ?? throw new JsonException("Petición vacía o mal formada.");
            respuesta = await _executor.EjecutarAsync(peticion, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return; // El servidor se está deteniendo: no hay a quién responder.
        }
        catch (Exception ex)
        {
            // Varias excepciones de la API de Revit llegan con Message vacío (DOCUMENTACION.md §4).
            respuesta = new RespuestaOperacion(
                Ok: false,
                Fase: Fase.Runtime,
                Resultado: null,
                IdsCreados: Array.Empty<long>(),
                Error: $"{ex.GetType().Name}: {ex.Message}",
                Traza: ex.ToString(),
                DuracionMs: 0);
        }

        try
        {
            var respuestaJson = JsonSerializer.Serialize(respuesta, JsonOptions);
            await PipeFraming.EscribirMensajeAsync(pipe, respuestaJson, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Cliente desconectado antes de leer la respuesta: nada que hacer.
        }
        catch (OperationCanceledException)
        {
            // Servidor deteniéndose durante la escritura: nada que hacer.
        }
    }

    private static NamedPipeServerStream CrearPipeConAcl(string nombre)
    {
        var pipeSecurity = new PipeSecurity();
        var usuarioActual = WindowsIdentity.GetCurrent().User;
        if (usuarioActual is not null)
        {
            pipeSecurity.AddAccessRule(new PipeAccessRule(usuarioActual, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        }

        return NamedPipeServerStreamAcl.Create(
            nombre,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 4096,
            outBufferSize: 4096,
            pipeSecurity: pipeSecurity);
    }

    public void Dispose()
    {
        Detener();
        _cts.Dispose();
    }
}
