using System.Text.Json.Serialization;

namespace RevitBridge.Core;

/// <summary>
/// Respuesta común a toda operación de la pasarela (DOCUMENTACION.md §4). Un fallo de ejecución
/// viaja como respuesta correcta con este contenido; el error de protocolo se reserva para lo que
/// no es un fallo de ejecución (Revit cerrado, pipe caído, timeout del transporte — ADR-007).
/// </summary>
public sealed record RespuestaOperacion(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("fase")] Fase Fase,
    [property: JsonPropertyName("resultado")] object? Resultado,
    [property: JsonPropertyName("ids_creados")] IReadOnlyList<long> IdsCreados,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("traza")] string? Traza,
    [property: JsonPropertyName("duracion_ms")] long DuracionMs);
