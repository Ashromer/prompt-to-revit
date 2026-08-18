using System.Text.Json.Serialization;

namespace RevitBridge.Core;

/// <summary>Petición de <c>/compile</c>: dry-run, compila el snippet sin ejecutarlo.</summary>
public sealed record CompileRequest(
    [property: JsonPropertyName("fuente")] string Fuente);
