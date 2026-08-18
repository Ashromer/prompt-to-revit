using System;
using Autodesk.Revit.DB;
using View = Autodesk.Revit.DB.View;
using RevitBridge.Utils;

namespace RevitBridge.Addin.Commands;

/// <summary>
/// Comandos robustos de pre-compilado para la gestión de vistas y planos (Tier 2).
/// Todas las modificaciones manejan su propia transacción.
/// </summary>
public static class ViewCommands
{
    [ComandoRevit("DuplicarVista")]
    public static object DuplicarVista(Document doc, int vistaId, int modo = 1)
    {
        // modo: 0 = Duplicate, 1 = WithDetailing, 2 = AsDependent
        var view = doc.GetElement(new ElementId(vistaId)) as View;
        if (view == null) throw new ArgumentException("Vista no encontrada");

        ViewDuplicateOption opt = modo switch
        {
            0 => ViewDuplicateOption.Duplicate,
            1 => ViewDuplicateOption.WithDetailing,
            2 => ViewDuplicateOption.AsDependent,
            _ => ViewDuplicateOption.WithDetailing
        };

        ElementId nuevaVistaId = ElementId.InvalidElementId;
        using (var tx = new Transaction(doc, "Duplicar Vista MCP"))
        {
            tx.Start();
            nuevaVistaId = view.Duplicate(opt);
            tx.Commit();
        }

        var nuevaVista = doc.GetElement(nuevaVistaId) as View;
        return new { Id = nuevaVistaId.Value, Nombre = nuevaVista?.Name };
    }

    [ComandoRevit("CambiarEscalaVista")]
    public static object CambiarEscalaVista(Document doc, int vistaId, int nuevaEscala)
    {
        var view = doc.GetElement(new ElementId(vistaId)) as View;
        if (view == null) throw new ArgumentException("Vista no encontrada");

        using (var tx = new Transaction(doc, "Cambiar Escala MCP"))
        {
            tx.Start();
            view.Scale = nuevaEscala;
            tx.Commit();
        }

        return new { Id = view.Id.Value, NuevaEscala = view.Scale };
    }

    [ComandoRevit("CrearPlano")]
    public static object CrearPlano(Document doc, int titleBlockId, string numeroPlano, string nombrePlano)
    {
        var tbId = titleBlockId <= 0 ? ElementId.InvalidElementId : new ElementId(titleBlockId);
        
        ViewSheet? sheet = null;
        using (var tx = new Transaction(doc, "Crear Plano MCP"))
        {
            tx.Start();
            sheet = ViewSheet.Create(doc, tbId);
            if (!string.IsNullOrEmpty(numeroPlano)) sheet.SheetNumber = numeroPlano;
            if (!string.IsNullOrEmpty(nombrePlano)) sheet.Name = nombrePlano;
            tx.Commit();
        }

        return new { Id = sheet.Id.Value, Numero = sheet.SheetNumber, Nombre = sheet.Name };
    }

    [ComandoRevit("ColocarVistaEnPlano")]
    public static object ColocarVistaEnPlano(Document doc, int planoId, int vistaId, double x = 0, double y = 0)
    {
        var sheetId = new ElementId(planoId);
        var viewId = new ElementId(vistaId);

        if (!Viewport.CanAddViewToSheet(doc, sheetId, viewId))
            throw new InvalidOperationException("Esta vista no se puede colocar en este plano.");

        Viewport? vp = null;
        using (var tx = new Transaction(doc, "Colocar Vista MCP"))
        {
            tx.Start();
            vp = Viewport.Create(doc, sheetId, viewId, new XYZ(x, y, 0));
            tx.Commit();
        }

        return new { Id = vp.Id.Value, SheetId = planoId, ViewId = vistaId };
    }

    [ComandoRevit("ColorearCategoriaEnVista")]
    public static object ColorearCategoriaEnVista(Document doc, string categoriaBuiltIn, byte r, byte g, byte b)
    {
        // TRADUCCIÓN NATIVA DESDE PYTHON (Fagocitado de RevitGeminiRAG: color_all_walls_red_in_view.py)
        var activeView = doc.ActiveView;
        if (activeView == null || activeView.IsTemplate || activeView.ViewType == ViewType.Schedule)
            throw new InvalidOperationException("La vista activa no admite override gráfico.");

        if (!Enum.TryParse(categoriaBuiltIn, out BuiltInCategory catEnum))
            throw new ArgumentException($"La categoría '{categoriaBuiltIn}' no es válida.");

        var color = new Autodesk.Revit.DB.Color(r, g, b);
        var overrideSettings = new OverrideGraphicSettings();
        
        overrideSettings.SetProjectionLineColor(color);
        overrideSettings.SetCutLineColor(color);
        // Evitamos patrones para mantener simplicidad gráfica

        var collector = new FilteredElementCollector(doc, activeView.Id)
            .OfCategory(catEnum)
            .WhereElementIsNotElementType()
            .ToElementIds();

        int afectados = 0;
        using (var tx = new Transaction(doc, $"Colorear {categoriaBuiltIn} MCP"))
        {
            tx.Start();
            foreach (var id in collector)
            {
                activeView.SetElementOverrides(id, overrideSettings);
                afectados++;
            }
            tx.Commit();
        }

        return new { Categoria = categoriaBuiltIn, Color = $"RGB({r},{g},{b})", ElementosAfectados = afectados };
    }
}
