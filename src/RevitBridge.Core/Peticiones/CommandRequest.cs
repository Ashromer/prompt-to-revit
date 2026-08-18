using System.Text.Json.Serialization;

namespace RevitBridge.Core;

/// <summary>Petición de <c>/command</c>: invoca un comando compilado del catálogo.</summary>
public sealed record CommandRequest(
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("argumentos")] object? Argumentos = null);
