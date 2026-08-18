using System.Text;

namespace RevitBridge.Core;

/// <summary>
/// Framing de mensajes JSON sobre un named pipe: prefijo de 4 bytes (Int32 little-endian) con la
/// longitud del mensaje en bytes UTF-8, seguido del propio JSON. Evita depender de
/// <c>PipeTransmissionMode.Message</c>, cuyo comportamiento exacto no está garantizado igual en
/// cliente y servidor. Puro sobre <see cref="Stream"/> (sin <c>System.IO.Pipes</c>, sin Revit),
/// así que lo comparten el servidor del addin (F0.3) y el cliente del puente sin duplicar código
/// y es testeable con un <see cref="MemoryStream"/>.
/// </summary>
public static class PipeFraming
{
    /// <summary>Cota defensiva contra un prefijo de longitud corrupto o un stream ajeno al protocolo.</summary>
    private const int MaxMensajeBytes = 16 * 1024 * 1024;

    public static async Task EscribirMensajeAsync(Stream stream, string json, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var longitud = BitConverter.GetBytes(bytes.Length);

        await stream.WriteAsync(longitud, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lee un mensaje completo. Devuelve <c>null</c> si el stream se cerró antes de completar el
    /// mensaje (cliente desconectado a medias) en vez de lanzar: es un caso esperado, no un fallo.
    /// </summary>
    public static async Task<string?> LeerMensajeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var longitudBuffer = new byte[4];
        if (!await LeerCompletoAsync(stream, longitudBuffer, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var longitud = BitConverter.ToInt32(longitudBuffer);
        if (longitud < 0 || longitud > MaxMensajeBytes)
        {
            throw new InvalidDataException($"Longitud de mensaje fuera de rango: {longitud}.");
        }

        var buffer = new byte[longitud];
        if (!await LeerCompletoAsync(stream, buffer, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return Encoding.UTF8.GetString(buffer);
    }

    private static async Task<bool> LeerCompletoAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var leido = 0;
        while (leido < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(leido), cancellationToken).ConfigureAwait(false);
            if (n == 0)
            {
                return false;
            }

            leido += n;
        }

        return true;
    }
}
