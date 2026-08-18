using System.Text.Json;
using System.Text.Json.Serialization;

namespace RevitBridge.Core;

/// <summary>
/// Fase en la que terminó una operación de la pasarela. Determina el triaje del error en el
/// cliente (DOCUMENTACION.md §4): un fallo de "compilacion" es casi siempre una API rota por
/// versión; un fallo de "runtime" es casi siempre un supuesto falso sobre el documento.
/// </summary>
[JsonConverter(typeof(FaseJsonConverter))]
public enum Fase
{
    Compilacion,
    Runtime,
    Ok
}

/// <summary>
/// Serializa <see cref="Fase"/> a las cadenas literales exactas del contrato ("compilacion",
/// "runtime", "ok"), sin depender de una naming policy genérica.
/// </summary>
public sealed class FaseJsonConverter : JsonConverter<Fase>
{
    private const string Compilacion = "compilacion";
    private const string Runtime = "runtime";
    private const string Ok = "ok";

    public override Fase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var valor = reader.GetString();
        return valor switch
        {
            Compilacion => Fase.Compilacion,
            Runtime => Fase.Runtime,
            Ok => Fase.Ok,
            _ => throw new JsonException($"Valor de 'fase' no reconocido: '{valor}'")
        };
    }

    public override void Write(Utf8JsonWriter writer, Fase value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            Fase.Compilacion => Compilacion,
            Fase.Runtime => Runtime,
            Fase.Ok => Ok,
            _ => throw new JsonException($"Valor de Fase no serializable: '{value}'")
        });
    }
}
