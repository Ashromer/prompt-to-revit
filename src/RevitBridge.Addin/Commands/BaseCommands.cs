using System;
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

    [ComandoRevit("ObtenerTiposCargadosPorCategoria")]
    public static object ObtenerTiposCargadosPorCategoria(Document doc, string nombreCategoria)
    {
        // Base para F3.7 (backlog "casa completa"): puertas/ventanas/mobiliario se colocan por
        // tipo real (FamilySymbol), nunca adivinando un nombre (§5.A.1) -- este comando resuelve
        // qué tipos hay realmente cargados en el proyecto antes de intentar colocar nada.
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

        var tipos = new FilteredElementCollector(doc)
            .OfCategoryId(targetCat.Id)
            .WhereElementIsElementType()
            .ToElements();

        return tipos.Select(t => new
        {
            Id = t.Id.Value,
            Nombre = t.Name,
            Familia = (t as FamilySymbol)?.Family?.Name
        }).Take(200).ToList();
    }

    [ComandoRevit("BuscarTiposDeMuroPorFuncion")]
    public static object BuscarTiposDeMuroPorFuncion(Document doc, string funcion)
    {
        // Base para tabiques (F3.7): un tabique es un muro con Function=Interior, no un tipo de
        // comando distinto -- CrearMurosMasivo ya acepta tipoMuroId, solo falta elegir el tipo real.
        if (!Enum.TryParse<WallFunction>(funcion, ignoreCase: true, out var funcionEnum))
        {
            throw new ArgumentException(
                $"Función de muro '{funcion}' no válida. Valores admitidos: {string.Join(", ", Enum.GetNames(typeof(WallFunction)))}");
        }

        var tipos = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .Where(wt => wt.Function == funcionEnum)
            .Select(wt => new { Id = wt.Id.Value, Nombre = wt.Name })
            .ToList();

        return tipos;
    }
}
