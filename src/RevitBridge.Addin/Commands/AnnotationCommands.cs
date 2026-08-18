using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using View = Autodesk.Revit.DB.View;
using RevitBridge.Core;
using RevitBridge.Utils;

namespace RevitBridge.Addin.Commands;

/// <summary>
/// Etiquetado automático de elementos en una vista (F3.7, extensión "casa completa").
/// TagMode.TM_ADDBY_CATEGORY deja que Revit resuelva la familia de etiqueta por defecto de la
/// categoría del elemento (ya cargada, o la que trae la plantilla) -- evita tener que resolver un
/// tipo de etiqueta explícito, que sería otro comando de consulta más antes de poder etiquetar.
/// Firma verificada con MetadataLoadContext: IndependentTag.Create(Document, ElementId ownerDBViewId,
/// Reference referenceToTag, bool addLeader, TagMode tagMode, TagOrientation tagOrientation, XYZ pnt)
/// es real en 2026.4.10 (hay un segundo overload con tipo de etiqueta explícito, no usado aquí).
/// </summary>
public static class AnnotationCommands
{
    [ComandoRevit("EtiquetarElementosEnVista")]
    public static object EtiquetarElementosEnVista(Document doc, int vistaId, List<int> elementoIds, bool conLinea = false)
    {
        var vista = doc.GetElement(new ElementId(vistaId)) as View;
        if (vista == null) throw new ArgumentException("Vista no encontrada.");
        if (elementoIds == null || elementoIds.Count == 0) return new { Etiquetados = 0 };

        // Las etiquetas son elementos nuevos en el documento (anotaciones), no modifican el
        // elemento etiquetado -- mismo régimen de aprobación por creación masiva que el resto del
        // catálogo (CrearMurosMasivo, etc.), no el de "modificar preexistentes".
        if (UmbralAprobacionCreacion.RequiereAprobacion(elementoIds.Count))
        {
            var resumenCreacion = DeletionPreview.ConstruirResumen(
                $"etiquetar en la vista '{vista.Name}'", Enumerable.Repeat("Etiqueta", elementoIds.Count));
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Etiquetado masivo cancelado por el usuario.");
        }

        int etiquetados = 0;
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Etiquetar {elementoIds.Count} Elementos"))
        {
            tx.Start();
            foreach (var idRaw in elementoIds)
            {
                try
                {
                    var elemId = new ElementId(idRaw);
                    var elem = doc.GetElement(elemId);
                    if (elem == null) { errores.Add($"Elemento {idRaw} no encontrado."); continue; }

                    var bbox = elem.get_BoundingBox(vista);
                    if (bbox == null) { errores.Add($"Elemento {idRaw} no es visible en la vista '{vista.Name}' (sin bounding box)."); continue; }
                    var punto = (bbox.Min + bbox.Max) / 2.0;

                    var referencia = new Reference(elem);
                    IndependentTag.Create(doc, vista.Id, referencia, conLinea, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, punto);

                    etiquetados++;
                }
                catch (Exception ex)
                {
                    // Falta de familia de etiqueta cargada para la categoría es el fallo más
                    // probable ("no tag family loaded for category X"), no una excepción rara --
                    // por eso por elemento, para que el resto del lote no se pierda.
                    errores.Add(ex.Message);
                }
            }
            tx.Commit();
        }

        return new { ElementosEtiquetados = etiquetados, ErroresOmitidos = errores.Count, Errores = errores };
    }
}
