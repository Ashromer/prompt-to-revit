using System.Text.Json;
using RevitBridge.Core;
using Xunit;

namespace RevitBridge.Tests.Core;

/// <summary>
/// Ida y vuelta de serialización de cada tipo del contrato (specs/tech-spec.md §Testing Strategy,
/// fila Core). Para los campos <c>object?</c> (resultado, filtros, argumentos) comparar por
/// re-serialización en vez de por igualdad CLR: al deserializar quedan como <see cref="JsonElement"/>,
/// no como el tipo original, así que la igualdad estructural del JSON es la comparación correcta.
/// </summary>
public class ContratoRoundTripTests
{
    [Fact]
    public void RespuestaOperacion_Ida_Y_Vuelta_Es_Estable()
    {
        var original = new RespuestaOperacion(
            Ok: false,
            Fase: Fase.Runtime,
            Resultado: null,
            IdsCreados: Array.Empty<long>(),
            Error: "InvalidOperationException: algo falló",
            Traza: "en Foo.Bar()...",
            DuracionMs: 340);

        AssertRoundTrip(original);
    }

    [Fact]
    public void RespuestaOperacion_Serializa_Los_Nombres_Exactos_Del_Contrato()
    {
        var respuesta = new RespuestaOperacion(
            Ok: true,
            Fase: Fase.Ok,
            Resultado: new { ids = new[] { 12345 } },
            IdsCreados: new long[] { 12345 },
            Error: null,
            Traza: null,
            DuracionMs: 12);

        var json = JsonSerializer.Serialize(respuesta);

        Assert.Contains("\"ok\":true", json);
        Assert.Contains("\"fase\":\"ok\"", json);
        Assert.Contains("\"resultado\":", json);
        Assert.Contains("\"ids_creados\":[12345]", json);
        Assert.Contains("\"error\":null", json);
        Assert.Contains("\"traza\":null", json);
        Assert.Contains("\"duracion_ms\":12", json);
    }

    [Fact]
    public void ConsultaRequest_Ida_Y_Vuelta_Es_Estable()
    {
        var original = new ConsultaRequest("niveles del proyecto", new { categoria = "Levels" });

        AssertRoundTrip(original);
    }

    [Fact]
    public void CompileRequest_Ida_Y_Vuelta_Es_Estable()
    {
        var original = new CompileRequest("public static class Script { }");

        AssertRoundTrip(original);
    }

    [Fact]
    public void ExecRequest_Ida_Y_Vuelta_Es_Estable()
    {
        var original = new ExecRequest("public static class Script { }", "crear un muro");

        AssertRoundTrip(original);
    }

    [Fact]
    public void CommandRequest_Ida_Y_Vuelta_Es_Estable()
    {
        var original = new CommandRequest("CrearMuro", new { longitud = 5000 });

        AssertRoundTrip(original);
    }

    [Fact]
    public void RollbackRequest_Ida_Y_Vuelta_Es_Estable()
    {
        var original = new RollbackRequest(new long[] { 1, 2, 3 });

        AssertRoundTrip(original);
    }

    [Fact]
    public void RollbackRequest_Sin_Ids_Serializa_Null()
    {
        var original = new RollbackRequest(null);

        var json = JsonSerializer.Serialize(original);

        Assert.Contains("\"ids\":null", json);
    }

    [Fact]
    public void CommandDescriptor_Ida_Y_Vuelta_Es_Estable()
    {
        var original = new CommandDescriptor(
            "CrearMuro",
            new Dictionary<string, string> { ["longitud"] = "double", ["nivel"] = "ElementId" });

        AssertRoundTrip(original);
    }

    private static void AssertRoundTrip<T>(T original)
    {
        var json1 = JsonSerializer.Serialize(original);
        var deserializado = JsonSerializer.Deserialize<T>(json1);
        var json2 = JsonSerializer.Serialize(deserializado);

        Assert.Equal(json1, json2);
    }
}
