using System.Text.Json.Serialization;

namespace RevitBridge.Core;

/// <summary>Petición de <c>/query</c>: lectura del modelo abierto, sin transacción.</summary>
public sealed record ConsultaRequest(
    [property: JsonPropertyName("consulta")] string Consulta,
    [property: JsonPropertyName("filtros")] object? Filtros = null);
