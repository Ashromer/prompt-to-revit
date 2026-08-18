using System.Text.Json.Serialization;

namespace RevitBridge.Core;

/// <summary>
/// Petición de <c>/exec</c>: ejecuta C# dentro de una <c>Transaction</c>. Escotilla de emergencia;
/// exige aprobación manual siempre (DOCUMENTACION.md §4).
/// </summary>
public sealed record ExecRequest(
    [property: JsonPropertyName("fuente")] string Fuente,
    [property: JsonPropertyName("intencion")] string Intencion);
