using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBridge.Utils;

namespace RevitBridge.Addin.Commands;

/// <summary>
/// Catálogo inicial de comandos de lectura para el Tier 2 (F2.2).
/// Contiene las funciones básicas que la IA usará para conocer el estado del modelo
/// en lugar de improvisarlas.
/// </summary>
public static class BaseCommands
{
    [ComandoRevit("ObtenerSeleccion")]
    public static object ObtenerSeleccion(UIApplication app)
    {
        var doc = app.ActiveUIDocument.Document;
        var ids = app.ActiveUIDocument.Selection.GetElementIds();
        
        return ids.Select(id => {
            var elem = doc.GetElement(id);
            return new {
                Id = id.Value,
                Nombre = elem?.Name,
                Categoria = elem?.Category?.Name,
                Tipo = elem?.GetType().Name
            };
        }).ToList();
    }

    [ComandoRevit("ObtenerInfoVistaActual")]
    public static object ObtenerInfoVistaActual(Document doc)
    {
        var vista = doc.ActiveView;
        return new {
            Id = vista.Id.Value,
            Nombre = vista.Name,
            TipoVista = vista.ViewType.ToString(),
            Escala = vista.Scale
        };
    }

    [ComandoRevit("ObtenerElementosDeCategoria")]
    public static object ObtenerElementosDeCategoria(Document doc, string nombreCategoria)
    {
        // Buscar la categoría por nombre
        var categorias = doc.Settings.Categories;
        Category? targetCat = null;
        
        foreach (Category cat in categorias)
        {
            if (cat.Name.Equals(nombreCategoria, System.StringComparison.OrdinalIgnoreCase))
            {
                targetCat = cat;
                break;
            }
        }
        
        if (targetCat == null) return new { error = $"Categoría '{nombreCategoria}' no encontrada." };

        var elementos = new FilteredElementCollector(doc)
            .OfCategoryId(targetCat.Id)
            .WhereElementIsNotElementType()
            .ToElements();

        return elementos.Select(e => new {
            Id = e.Id.Value,
            Nombre = e.Name
        }).Take(100).ToList();
    }
}
