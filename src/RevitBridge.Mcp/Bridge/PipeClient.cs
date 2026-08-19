using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
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
    private readonly string? _pipeNameFijo;
    private static readonly JsonSerializerOptions JsonOptions = new();

    /// <param name="pipeNameFijo">
    /// Nombre exacto a usar. Si es <c>null</c>/vacío, el nombre se descubre en vivo en cada envío
    /// enumerando los procesos <c>Revit</c> en ejecución (bug real encontrado 2026-08-19: fijar el
    /// nombre a mano, con el PID de una sesión anterior, queda obsoleto en cuanto Revit se
    /// reinicia — el PID cambia cada arranque, así que nunca hay un valor fijo correcto).
    /// </param>
    public PipeClient(string? pipeNameFijo)
    {
        _pipeNameFijo = string.IsNullOrWhiteSpace(pipeNameFijo) ? null : pipeNameFijo;
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
        var candidatos = _pipeNameFijo is not null
            ? new[] { _pipeNameFijo }
            : NombresCandidatosPorRevitEnEjecucion();

        if (candidatos.Length == 0)
        {
            throw new TimeoutException(
                "No hay ningún proceso de Revit en ejecución (descubrimiento automático por PID). " +
                "Abre Revit con el documento cargado, o fija REVITBRIDGE_PIPE a mano.");
        }

        NamedPipeClientStream? cliente = null;
        Exception? ultimoError = null;
        var timeoutPorCandidato = TimeSpan.FromMilliseconds(
            Math.Max(1, timeoutConexion.TotalMilliseconds / candidatos.Length));

        foreach (var nombre in candidatos)
        {
            var intento = new NamedPipeClientStream(".", nombre, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await intento.ConnectAsync((int)timeoutPorCandidato.TotalMilliseconds, cancellationToken)
                    .ConfigureAwait(false);
                cliente = intento;
                break;
            }
            catch (TimeoutException ex)
            {
                ultimoError = ex;
                intento.Dispose();
            }
        }

        if (cliente is null)
        {
            throw new TimeoutException(
                $"No se pudo conectar a ningún pipe candidato ({string.Join(", ", candidatos)}) " +
                $"en {timeoutConexion}: Revit cerrado o addin no cargado.",
                ultimoError);
        }

        using var _ = cliente;

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

    /// <summary>
    /// Un candidato de nombre de pipe por cada proceso <c>Revit</c> vivo, con el mismo formato que
    /// <c>PipeServer.NombrePorDefecto()</c> del addin (<c>RevitBridge_{usuario}_{PID}</c>). Con una
    /// sola instancia de Revit abierta (el caso normal) hay un único candidato y siempre acierta,
    /// sin ninguna variable de entorno que mantener sincronizada a mano.
    /// </summary>
    private static string[] NombresCandidatosPorRevitEnEjecucion() =>
        Process.GetProcessesByName("Revit")
            .Select(p => $"RevitBridge_{Environment.UserName}_{p.Id}")
            .ToArray();
}
