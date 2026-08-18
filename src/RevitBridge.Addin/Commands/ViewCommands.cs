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

    [ComandoRevit("CrearVistaPlanta")]
    public static object CrearVistaPlanta(Document doc, int nivelId)
    {
        var level = doc.GetElement(new ElementId(nivelId)) as Level;
        if (level == null) throw new ArgumentException("Nivel no encontrado");

        var viewFamilyType = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan);

        if (viewFamilyType == null) throw new InvalidOperationException("No se encontró ViewFamilyType para FloorPlan");

        ViewPlan? nuevaVista = null;
        using (var tx = new Transaction(doc, "Crear Vista Planta MCP"))
        {
            tx.Start();
            nuevaVista = ViewPlan.Create(doc, viewFamilyType.Id, level.Id);
            tx.Commit();
        }

        return new { Id = nuevaVista.Id.Value, Nombre = nuevaVista.Name };
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

    [ComandoRevit("CrearVistaSeccion")]
    public static object CrearVistaSeccion(Document doc, double p1x, double p1y, double p2x, double p2y, double alturaMetros = 3.0, double profundidadMetros = 5.0, double elevacionBaseMetros = 0)
    {
        // F3.7, extensión "casa completa". Firma verificada con MetadataLoadContext:
        // ViewSection.CreateSection(Document, ElementId viewFamilyTypeId, BoundingBoxXYZ sectionBox)
        // es real en 2026.4.10. Geometría del sectionBox: patrón estándar del SDK de Revit (no
        // probado en vivo -- es el comando de este lote con más riesgo geométrico; primer candidato
        // a revisar si la sección sale mal orientada o recortada).
        var viewFamilyType = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Section);
        if (viewFamilyType == null) throw new InvalidOperationException("No se encontró ViewFamilyType para Section.");

        double m2ft = 1.0 / 0.3048;
        var p1 = new XYZ(p1x * m2ft, p1y * m2ft, elevacionBaseMetros * m2ft);
        var p2 = new XYZ(p2x * m2ft, p2y * m2ft, elevacionBaseMetros * m2ft);
        var longitud = p1.DistanceTo(p2);
        if (longitud < 1e-6) throw new ArgumentException("Los dos puntos de la línea de sección son coincidentes.");

        var puntoMedio = (p1 + p2) * 0.5;
        var direccion = (p2 - p1).Normalize(); // BasisX: horizontal a lo largo de la línea de corte
        var arriba = XYZ.BasisZ;               // BasisY: vertical del propio corte
        var direccionVista = direccion.CrossProduct(arriba); // BasisZ: hacia dónde mira la sección

        var transform = Transform.Identity;
        transform.Origin = puntoMedio;
        transform.BasisX = direccion;
        transform.BasisY = arriba;
        transform.BasisZ = direccionVista;

        var sectionBox = new BoundingBoxXYZ { Transform = transform };
        var mitadLongitud = longitud / 2.0;
        var profundidadFt = profundidadMetros * m2ft;
        var alturaFt = alturaMetros * m2ft;
        sectionBox.Min = new XYZ(-mitadLongitud, 0, -profundidadFt);
        sectionBox.Max = new XYZ(mitadLongitud, alturaFt, profundidadFt);

        ViewSection? seccion = null;
        using (var tx = new Transaction(doc, "Crear Vista Sección MCP"))
        {
            tx.Start();
            seccion = ViewSection.CreateSection(doc, viewFamilyType.Id, sectionBox);
            tx.Commit();
        }

        return new { Id = seccion!.Id.Value, Nombre = seccion.Name };
    }

    [ComandoRevit("CrearVista3D")]
    public static object CrearVista3D(Document doc, string nombre = "")
    {
        // F3.7, extensión "casa completa". Firma verificada con MetadataLoadContext:
        // View3D.CreateIsometric(Document, ElementId viewFamilyTypeId) es estática y real en
        // 2026.4.10. Creación única, no masiva -- sin aprobación, mismo régimen que CrearVistaPlanta.
        var viewFamilyType = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>()
            .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional);
        if (viewFamilyType == null) throw new InvalidOperationException("No se encontró ViewFamilyType para vista 3D.");

        View3D? vista3D = null;
        using (var tx = new Transaction(doc, "Crear Vista 3D MCP"))
        {
            tx.Start();
            vista3D = View3D.CreateIsometric(doc, viewFamilyType.Id);
            if (!string.IsNullOrWhiteSpace(nombre)) vista3D.Name = nombre;
            tx.Commit();
        }

        return new { Id = vista3D.Id.Value, Nombre = vista3D.Name };
    }

    [ComandoRevit("AplicarPlantillaDeVista")]
    public static object AplicarPlantillaDeVista(Document doc, int vistaId, int plantillaId)
    {
        // Cambio de un único parámetro (ViewTemplateId) sobre una vista -- mismo régimen que
        // CambiarEscalaVista: modifica una vista, no el modelo, no lleva PreexistingElementGuard
        // (ese guard es para geometría/parámetros del modelo, §5.C.10/§5.D.15, no vistas).
        var vista = doc.GetElement(new ElementId(vistaId)) as View;
        if (vista == null) throw new ArgumentException("Vista no encontrada.");

        var plantilla = doc.GetElement(new ElementId(plantillaId)) as View;
        if (plantilla == null || !plantilla.IsTemplate)
            throw new ArgumentException("El id indicado no corresponde a una plantilla de vista (¿lo obtuviste de ObtenerVistas con incluirPlantillas=true?).");

        using (var tx = new Transaction(doc, "Aplicar Plantilla de Vista MCP"))
        {
            tx.Start();
            vista.ViewTemplateId = plantilla.Id;
            tx.Commit();
        }

        return new { Id = vista.Id.Value, PlantillaAplicada = plantilla.Name };
    }
}
