using System.Text.Json.Serialization;

namespace RevitBridge.Core;

/// <summary>
/// Petición de <c>/rollback</c>: borra los elementos creados en la sesión. Sin <c>Hasta</c>,
/// por defecto toda la sesión.
/// </summary>
public sealed record RollbackRequest(
    [property: JsonPropertyName("ids")] System.Collections.Generic.IReadOnlyList<long>? Ids = null,
    [property: JsonPropertyName("hasta")] string? Hasta = null);
