using RevitBridge.Core;
using Xunit;

namespace RevitBridge.Tests.Core;

/// <summary>
/// Framing de mensajes JSON sobre <see cref="Stream"/> (F0.3). Puro C# sin named pipes reales:
/// un <see cref="MemoryStream"/> basta para probar el protocolo de longitud + payload.
/// </summary>
public class PipeFramingTests
{
    [Fact]
    public async Task Escribir_Y_Leer_Devuelve_El_Mismo_Json()
    {
        using var stream = new MemoryStream();

        await PipeFraming.EscribirMensajeAsync(stream, "{\"operacion\":\"query\"}");
        stream.Position = 0;

        var leido = await PipeFraming.LeerMensajeAsync(stream);

        Assert.Equal("{\"operacion\":\"query\"}", leido);
    }

    [Fact]
    public async Task Soporta_Varios_Mensajes_Consecutivos_En_El_Mismo_Stream()
    {
        using var stream = new MemoryStream();

        await PipeFraming.EscribirMensajeAsync(stream, "primero");
        await PipeFraming.EscribirMensajeAsync(stream, "segundo");
        stream.Position = 0;

        Assert.Equal("primero", await PipeFraming.LeerMensajeAsync(stream));
        Assert.Equal("segundo", await PipeFraming.LeerMensajeAsync(stream));
    }

    [Fact]
    public async Task Soporta_Unicode()
    {
        using var stream = new MemoryStream();
        var original = "{\"intencion\":\"crear muro de 3,5 m — secciónño\"}";

        await PipeFraming.EscribirMensajeAsync(stream, original);
        stream.Position = 0;

        Assert.Equal(original, await PipeFraming.LeerMensajeAsync(stream));
    }

    [Fact]
    public async Task Stream_Vacio_Devuelve_Null()
    {
        using var stream = new MemoryStream();

        var leido = await PipeFraming.LeerMensajeAsync(stream);

        Assert.Null(leido);
    }

    [Fact]
    public async Task Stream_Truncado_A_Medio_Prefijo_Devuelve_Null()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2 }); // menos de los 4 bytes del prefijo

        var leido = await PipeFraming.LeerMensajeAsync(stream);

        Assert.Null(leido);
    }

    [Fact]
    public async Task Stream_Truncado_A_Medio_Payload_Devuelve_Null()
    {
        using var stream = new MemoryStream();
        await PipeFraming.EscribirMensajeAsync(stream, "un mensaje largo de verdad");

        // Cortar el stream a la mitad: prefijo completo, payload incompleto.
        var bytesCompletos = stream.ToArray();
        var truncado = new MemoryStream(bytesCompletos[..(bytesCompletos.Length - 5)]);

        var leido = await PipeFraming.LeerMensajeAsync(truncado);

        Assert.Null(leido);
    }

    [Fact]
    public async Task Longitud_Negativa_Lanza_InvalidDataException()
    {
        using var stream = new MemoryStream();
        await stream.WriteAsync(BitConverter.GetBytes(-1));
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(() => PipeFraming.LeerMensajeAsync(stream));
    }

    [Fact]
    public async Task Longitud_Desproporcionada_Lanza_InvalidDataException()
    {
        using var stream = new MemoryStream();
        await stream.WriteAsync(BitConverter.GetBytes(int.MaxValue));
        stream.Position = 0;

        await Assert.ThrowsAsync<InvalidDataException>(() => PipeFraming.LeerMensajeAsync(stream));
    }
}
