using System.IO.Pipes;
using System.Text.Json;
using RevitBridge.Core;

namespace RevitBridge.Mcp.Bridge;

/// <summary>
/// Cliente del named pipe del addin (F0.3). Sustituye a curl/Postman, que no sirven contra un
/// named pipe (ADR-002, "se pierde poder depurar con curl... se cubre con un cliente de pipe").
/// Sin ACL propia del lado cliente: la autorización la hace el sistema operativo al conectar
/// contra el pipe ya restringido por el servidor (DOCUMENTACION.md §5.E.19).
/// </summary>
public sealed class PipeClient
{
    private readonly string _pipeName;
    private static readonly JsonSerializerOptions JsonOptions = new();

    public PipeClient(string pipeName)
    {
        _pipeName = pipeName;
    }

    /// <summary>
    /// Conecta, envía la petición y espera la respuesta real.
    /// <paramref name="timeoutConexion"/> agotado significa "Revit cerrado o addin no cargado",
    /// que es el caso normal, no un error del sistema (DOCUMENTACION.md §8). Agotar
    /// <paramref name="timeoutRespuesta"/> corta la ESPERA de este cliente, no la ejecución en
    /// Revit: no hay timeout real de ejecución (DOCUMENTACION.md §5, "Limitación conocida").
    /// </summary>
    public async Task<RespuestaOperacion> EnviarAsync(
        PeticionPipe peticion,
        TimeSpan timeoutConexion,
        TimeSpan timeoutRespuesta,
        CancellationToken cancellationToken = default)
    {
        using var cliente = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            await cliente.ConnectAsync((int)timeoutConexion.TotalMilliseconds, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"No se pudo conectar al pipe '{_pipeName}' en {timeoutConexion}: Revit cerrado o addin no cargado.");
        }

        var json = JsonSerializer.Serialize(peticion, JsonOptions);
        await PipeFraming.EscribirMensajeAsync(cliente, json, cancellationToken).ConfigureAwait(false);

        using var ctsRespuesta = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ctsRespuesta.CancelAfter(timeoutRespuesta);

        string? respuestaJson;
        try
        {
            respuestaJson = await PipeFraming.LeerMensajeAsync(cliente, ctsRespuesta.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Sin respuesta del addin en {timeoutRespuesta}. Esto NO cancela la ejecución en Revit, " +
                "que puede seguir en curso.");
        }

        if (respuestaJson is null)
        {
            throw new IOException("El addin cerró la conexión sin responder.");
        }

        return JsonSerializer.Deserialize<RespuestaOperacion>(respuestaJson, JsonOptions)
            ?? throw new JsonException("Respuesta vacía del addin.");
    }
}
