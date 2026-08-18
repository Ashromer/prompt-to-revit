using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitBridge.Utils;

namespace RevitBridge.Addin.Commands;

/// <summary>
/// Comandos de extracción de datos para el "Contexto Denso" del Tier 3 (F3.1).
/// Diseñados para precargar al LLM con un mapa topológico completo del modelo
/// en una sola llamada, evitando que el Agente haga 50 preguntas distintas.
/// </summary>
public static class ExtractionCommands
{
    [ComandoRevit("ExportarContextoMasivo")]
    public static object ExportarContextoMasivo(Document doc)
    {
        // 1. Recolectar Niveles
        var niveles = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .Select(l => new { 
                Id = l.Id.Value, 
                Nombre = l.Name, 
                Elevacion = l.Elevation 
            }).ToList();

        // 2. Recolectar Planos (Sheets) y Vistas Activas
        var hojas = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSheet))
            .Cast<ViewSheet>()
            .Select(s => new {
                Id = s.Id.Value,
                Numero = s.SheetNumber,
                Nombre = s.Name
            }).ToList();

        // 3. Recuento masivo de las categorías principales para dar "conciencia espacial"
        var categoriasInteres = new List<BuiltInCategory> {
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Doors,
            BuiltInCategory.OST_Windows,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Ceilings,
            BuiltInCategory.OST_Rooms
        };

        var resumenCategorias = new Dictionary<string, int>();
        foreach (var cat in categoriasInteres)
        {
            int conteo = new FilteredElementCollector(doc)
                .OfCategory(cat)
                .WhereElementIsNotElementType()
                .GetElementCount();
            
            resumenCategorias.Add(cat.ToString().Replace("OST_", ""), conteo);
        }

        return new {
            DocumentoActivo = doc.Title,
            RutaPath = doc.PathName,
            Unidades = doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId().TypeId,
            Niveles = niveles,
            Planos = hojas,
            Inventario = resumenCategorias
        };
    }

    [ComandoRevit("ExportarGrafoTopologico")]
    public static object ExportarGrafoTopologico(Document doc)
    {
        // Vital para la auditoría automática (F3.3) y análisis de CTE / Evacuación
        var doors = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Doors)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .ToList();

        var grafo = new List<object>();

        foreach (var door in doors)
        {
            var fromRoom = door.FromRoom;
            var toRoom = door.ToRoom;
            
            // Anchura aproximada de la puerta
            var widthParam = door.Symbol.LookupParameter("Width") ?? door.LookupParameter("Width");
            var widthVal = widthParam?.AsDouble() ?? 0.0;

            grafo.Add(new {
                PuertaId = door.Id.Value,
                Nombre = door.Name,
                HandFlipped = door.HandFlipped,
                FacingFlipped = door.FacingFlipped,
                Width = widthVal,
                FromRoom = fromRoom != null ? new { Id = fromRoom.Id.Value, Name = fromRoom.Name } : null,
                ToRoom = toRoom != null ? new { Id = toRoom.Id.Value, Name = toRoom.Name } : null
            });
        }

        return new {
            TotalPuertas = doors.Count,
            Conexiones = grafo
        };
    }
}
