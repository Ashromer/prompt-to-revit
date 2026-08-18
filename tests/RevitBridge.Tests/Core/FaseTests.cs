using System.Text.Json;
using RevitBridge.Core;
using Xunit;

namespace RevitBridge.Tests.Core;

public class FaseTests
{
    [Theory]
    [InlineData(Fase.Compilacion, "\"compilacion\"")]
    [InlineData(Fase.Runtime, "\"runtime\"")]
    [InlineData(Fase.Ok, "\"ok\"")]
    public void Serializa_A_La_Cadena_Literal_Exacta(Fase fase, string jsonEsperado)
    {
        var json = JsonSerializer.Serialize(fase);

        Assert.Equal(jsonEsperado, json);
    }

    [Theory]
    [InlineData("\"compilacion\"", Fase.Compilacion)]
    [InlineData("\"runtime\"", Fase.Runtime)]
    [InlineData("\"ok\"", Fase.Ok)]
    public void Deserializa_Desde_La_Cadena_Literal_Exacta(string json, Fase faseEsperada)
    {
        var fase = JsonSerializer.Deserialize<Fase>(json);

        Assert.Equal(faseEsperada, fase);
    }

    [Fact]
    public void Cadena_No_Reconocida_Lanza_JsonException()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Fase>("\"desconocida\""));
    }

    [Theory]
    [InlineData(Fase.Compilacion)]
    [InlineData(Fase.Runtime)]
    [InlineData(Fase.Ok)]
    public void Ida_Y_Vuelta_Es_Estable(Fase fase)
    {
        var json = JsonSerializer.Serialize(fase);
        var deserializada = JsonSerializer.Deserialize<Fase>(json);

        Assert.Equal(fase, deserializada);
    }
}
