using System.Text.Json.Serialization;

namespace RevitBridge.Core;

/// <summary>
/// Descriptor de un comando del catálogo, tal como lo devuelve <c>/commands</c>. El esquema de
/// argumentos es deliberadamente simple aquí (nombre de argumento -> descripción de tipo); un
/// sistema de tipos más elaborado es F0.9/F2.1, fuera de alcance de este lote.
/// </summary>
public sealed record CommandDescriptor(
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("esquema")] IReadOnlyDictionary<string, string> Esquema);
