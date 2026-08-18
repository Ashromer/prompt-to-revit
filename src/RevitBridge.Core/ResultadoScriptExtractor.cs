using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RevitBridge.Core;

/// <summary>
/// Extrae <c>ids_creados</c> del valor de retorno de un snippet Roslyn (DOCUMENTACION.md §4:
/// <c>return new { ids = new[] { 12345 } };</c>). Vive en Core, sin Revit, para que sea testeable:
/// el valor de retorno real del script se descartaba antes de esta corrección (auditoría 2026-08-18)
/// y la respuesta siempre mandaba <c>ids_creados: []</c> y un string fijo en <c>resultado</c>.
/// </summary>
public static class ResultadoScriptExtractor
{
    public static IReadOnlyList<long> ExtraerIdsCreados(object? resultado)
    {
        if (resultado is null)
        {
            return Array.Empty<long>();
        }

        try
        {
            var elemento = resultado is JsonElement je ? je : JsonSerializer.SerializeToElement(resultado);
            if (elemento.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<long>();
            }

            foreach (var propiedad in elemento.EnumerateObject())
            {
                if (!string.Equals(propiedad.Name, "ids", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (propiedad.Value.ValueKind != JsonValueKind.Array)
                {
                    return Array.Empty<long>();
                }

                var ids = new List<long>();
                foreach (var item in propiedad.Value.EnumerateArray())
                {
                    // TryGetInt64 lanza InvalidOperationException si el elemento no es de tipo
                    // Number (no basta con que devuelva false) -- comprobar el ValueKind antes.
                    if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var idValor))
                    {
                        ids.Add(idValor);
                    }
                }

                return ids;
            }
        }
        catch (JsonException)
        {
            // Resultado no serializable (p. ej. un tipo de la API de Revit devuelto directamente):
            // no hay ids_creados que extraer, no es un error del extractor.
        }
        catch (NotSupportedException)
        {
            // Igual que arriba: tipos que System.Text.Json no sabe serializar.
        }

        return Array.Empty<long>();
    }
}
