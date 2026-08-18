using System.Text.Json;
using System.Text.Json.Serialization;

namespace RevitBridge.Core;

/// <summary>
/// Un <see cref="JsonElement"/> sin inicializar (p. ej. <see cref="PeticionPipe.Datos"/> cuando la
/// operación no lleva payload) tiene <c>ValueKind.Undefined</c>, que el convertidor estándar de
/// System.Text.Json no sabe serializar y lanza <c>InvalidOperationException</c>. Este convertidor
/// lo trata como JSON <c>null</c> al escribir; al leer delega en el comportamiento estándar.
/// </summary>
public sealed class JsonElementOrNullConverter : JsonConverter<JsonElement>
{
    public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => JsonElement.ParseValue(ref reader);

    public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
    {
        if (value.ValueKind == JsonValueKind.Undefined)
        {
            writer.WriteNullValue();
            return;
        }

        value.WriteTo(writer);
    }
}
