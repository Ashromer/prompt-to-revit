using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;

namespace RevitBridge.CadIngest;

/// <summary>Resumen de una capa del CAD, para que el mapeo capa→uso lo confirme el usuario en la
/// conversación (ADR-012 D2), no una heurística silenciosa.</summary>
public sealed record CadLayerSummary(string Nombre, int Lineas, int Polilineas, int Inserts, int Otros)
{
    public int Total => Lineas + Polilineas + Inserts + Otros;
}

/// <summary>
/// Abre un fichero CAD (DXF o DWG, misma API en memoria vía ACadSharp -- ADR-012 D1) y resume sus
/// capas. Función pura, sin Revit, testeable con xUnit (nivel 2 de "qué significa verificado").
/// </summary>
public static class CadDocumentReader
{
    public static CadDocument Abrir(string ruta)
    {
        var extension = Path.GetExtension(ruta).ToLowerInvariant();
        switch (extension)
        {
            case ".dxf":
                using (var reader = new DxfReader(ruta)) return reader.Read();
            case ".dwg":
                using (var reader = new DwgReader(ruta)) return reader.Read();
            default:
                throw new NotSupportedException($"Extensión '{extension}' no soportada: solo .dxf y .dwg.");
        }
    }

    public static IReadOnlyList<CadLayerSummary> ListarCapas(CadDocument documento)
    {
        return documento.Entities
            .GroupBy(e => e.Layer?.Name ?? "0")
            .Select(g => new CadLayerSummary(
                g.Key,
                g.Count(e => e is Line),
                g.Count(e => e is LwPolyline),
                g.Count(e => e is Insert),
                g.Count(e => e is not Line and not LwPolyline and not Insert)))
            .OrderByDescending(c => c.Total)
            .ToList();
    }
}
