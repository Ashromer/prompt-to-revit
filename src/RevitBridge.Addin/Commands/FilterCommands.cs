using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitBridge.Utils;

namespace RevitBridge.Addin.Commands;

/// <summary>
/// Comandos maestros para Filtros de Visibilidad y Gráficos.
/// Fagocitados de docenas de scripts de Python (ocultar, aislar, aplicar override, halftone).
/// </summary>
public static class FilterCommands
{
    [ComandoRevit("OcultarCategoriaEnVista")]
    public static object OcultarCategoriaEnVista(Document doc, string categoriaBuiltIn, bool ocultar)
    {
        var activeView = doc.ActiveView;
        if (activeView == null) throw new InvalidOperationException("No hay vista activa.");
        
        if (!Enum.TryParse(categoriaBuiltIn, out BuiltInCategory catEnum))
            throw new ArgumentException($"La categoría '{categoriaBuiltIn}' no es válida.");

        var catId = new ElementId((int)catEnum);
        
        if (!activeView.CanCategoryBeHidden(catId))
            throw new InvalidOperationException("Esta categoría no se puede ocultar en la vista activa.");

        using (var tx = new Transaction(doc, $"Ocultar {categoriaBuiltIn}"))
        {
            tx.Start();
            activeView.SetCategoryHidden(catId, ocultar);
            tx.Commit();
        }

        return new { Categoria = categoriaBuiltIn, Oculta = ocultar };
    }

    [ComandoRevit("AplicarHalftoneACategoria")]
    public static object AplicarHalftoneACategoria(Document doc, string categoriaBuiltIn, bool halftone)
    {
        var activeView = doc.ActiveView;
        if (activeView == null) throw new InvalidOperationException("No hay vista activa.");
        
        if (!Enum.TryParse(categoriaBuiltIn, out BuiltInCategory catEnum))
            throw new ArgumentException($"La categoría '{categoriaBuiltIn}' no es válida.");

        var catId = new ElementId((int)catEnum);
        var overrideSettings = activeView.GetCategoryOverrides(catId);
        overrideSettings.SetHalftone(halftone);

        using (var tx = new Transaction(doc, $"Halftone {categoriaBuiltIn}"))
        {
            tx.Start();
            activeView.SetCategoryOverrides(catId, overrideSettings);
            tx.Commit();
        }

        return new { Categoria = categoriaBuiltIn, Halftone = halftone };
    }

    [ComandoRevit("AplicarTransparenciaACategoria")]
    public static object AplicarTransparenciaACategoria(Document doc, string categoriaBuiltIn, int porcentaje)
    {
        var activeView = doc.ActiveView;
        if (activeView == null) throw new InvalidOperationException("No hay vista activa.");
        if (porcentaje < 0 || porcentaje > 100) throw new ArgumentException("El porcentaje debe estar entre 0 y 100");
        
        if (!Enum.TryParse(categoriaBuiltIn, out BuiltInCategory catEnum))
            throw new ArgumentException($"La categoría '{categoriaBuiltIn}' no es válida.");

        var catId = new ElementId((int)catEnum);
        var overrideSettings = activeView.GetCategoryOverrides(catId);
        overrideSettings.SetSurfaceTransparency(porcentaje);

        using (var tx = new Transaction(doc, $"Transparencia {categoriaBuiltIn}"))
        {
            tx.Start();
            activeView.SetCategoryOverrides(catId, overrideSettings);
            tx.Commit();
        }

        return new { Categoria = categoriaBuiltIn, Transparencia = porcentaje };
    }
}
