using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitBridge.Core;
using RevitBridge.Utils;

namespace RevitBridge.Addin.Commands;

/// <summary>
/// Copiar/mover/rotar elementos ya existentes en el documento (F3.7, extensión "casa completa").
/// Copiar es creación (nuevos elementos, el original queda intacto) -- aprobación por umbral, igual
/// que CrearMurosMasivo. Mover y rotar SÍ modifican el elemento original in situ, así que llevan la
/// salvaguarda de preexistentes (§5.C.10/§5.D.15) igual que ModificarParametro/
/// ModificarParametrosMasivo: solo piden aprobación si algún id afectado NO se creó en esta sesión.
/// Firmas verificadas con MetadataLoadContext: ElementTransformUtils.CopyElement/MoveElement/
/// RotateElement son estáticas y reales en 2026.4.10 (RotateElement ya se usaba en
/// ColocarMobiliarioMasivo para la rotación de mobiliario).
/// </summary>
public static class TransformCommands
{
    [ComandoRevit("CopiarElementosMasivo")]
    public static object CopiarElementosMasivo(Document doc, List<int> elementoIds, double desplazamientoXMetros, double desplazamientoYMetros, double desplazamientoZMetros = 0)
    {
        if (elementoIds == null || elementoIds.Count == 0) return new { Creados = 0 };

        double m2ft = 1.0 / 0.3048;
        var idsOriginal = elementoIds.Select(id => new ElementId(id)).ToList();

        if (UmbralAprobacionCreacion.RequiereAprobacion(idsOriginal.Count))
        {
            var categorias = idsOriginal.Select(id => doc.GetElement(id)?.Category?.Name ?? "Desconocido");
            var resumenCreacion = DeletionPreview.ConstruirResumen("copiar (desplazado)", categorias);
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Copia masiva de elementos cancelada por el usuario.");
        }

        var traslacion = new XYZ(desplazamientoXMetros * m2ft, desplazamientoYMetros * m2ft, desplazamientoZMetros * m2ft);

        var idsCreados = new List<long>();
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Copiar {idsOriginal.Count} Elementos"))
        {
            tx.Start();
            // Un id por llamada a CopyElement (no el overload de colección completa): así un id
            // inexistente o no copiable no descarta el resto del lote, mismo principio que el resto
            // del catálogo (un elemento malo no aborta el batch).
            foreach (var id in idsOriginal)
            {
                try
                {
                    if (doc.GetElement(id) == null) { errores.Add($"Elemento {id.Value} no encontrado."); continue; }

                    var copiados = ElementTransformUtils.CopyElement(doc, id, traslacion);
                    idsCreados.AddRange(copiados.Select(c => c.Value));
                }
                catch (Exception ex)
                {
                    errores.Add(ex.Message);
                }
            }
            tx.Commit();
        }

        return new { ElementosCreados = idsCreados.Count, Ids = idsCreados, ErroresOmitidos = errores.Count, Errores = errores };
    }

    [ComandoRevit("MoverElementosMasivo")]
    public static object MoverElementosMasivo(Document doc, List<int> elementoIds, double desplazamientoXMetros, double desplazamientoYMetros, double desplazamientoZMetros = 0)
    {
        if (elementoIds == null || elementoIds.Count == 0) return new { Movidos = 0 };

        double m2ft = 1.0 / 0.3048;
        var ids = elementoIds.Select(id => new ElementId(id)).ToList();

        if (PreexistingElementGuard.RequiereAprobacion(ids.Select(id => (long)id.Value), App.ElementosCreadosEnSesion))
        {
            var categorias = ids.Select(id => doc.GetElement(id)?.Category?.Name ?? "Desconocido");
            var resumen = DeletionPreview.ConstruirResumen("mover", categorias);
            var approval = new RevitBridge.Addin.UI.ApprovalService();
            if (!approval.SolicitarAprobacion(resumen))
                throw new InvalidOperationException("Movimiento masivo de elementos cancelado por el usuario.");
        }

        var traslacion = new XYZ(desplazamientoXMetros * m2ft, desplazamientoYMetros * m2ft, desplazamientoZMetros * m2ft);

        int movidos = 0;
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Mover {ids.Count} Elementos"))
        {
            tx.Start();
            foreach (var id in ids)
            {
                try
                {
                    if (doc.GetElement(id) == null) { errores.Add($"Elemento {id.Value} no encontrado."); continue; }
                    ElementTransformUtils.MoveElement(doc, id, traslacion);
                    movidos++;
                }
                catch (Exception ex)
                {
                    errores.Add(ex.Message);
                }
            }
            tx.Commit();
        }

        return new { ElementosMovidos = movidos, ErroresOmitidos = errores.Count, Errores = errores };
    }

    [ComandoRevit("RotarElementosMasivo")]
    public static object RotarElementosMasivo(Document doc, List<int> elementoIds, double anguloGrados, double centroXMetros, double centroYMetros)
    {
        if (elementoIds == null || elementoIds.Count == 0) return new { Rotados = 0 };

        double m2ft = 1.0 / 0.3048;
        var ids = elementoIds.Select(id => new ElementId(id)).ToList();

        if (PreexistingElementGuard.RequiereAprobacion(ids.Select(id => (long)id.Value), App.ElementosCreadosEnSesion))
        {
            var categorias = ids.Select(id => doc.GetElement(id)?.Category?.Name ?? "Desconocido");
            var resumen = DeletionPreview.ConstruirResumen("rotar", categorias);
            var approval = new RevitBridge.Addin.UI.ApprovalService();
            if (!approval.SolicitarAprobacion(resumen))
                throw new InvalidOperationException("Rotación masiva de elementos cancelada por el usuario.");
        }

        // Mismo patrón que ColocarMobiliarioMasivo: eje vertical (paralelo a Z) pasando por el
        // centro de rotación pedido.
        var centro = new XYZ(centroXMetros * m2ft, centroYMetros * m2ft, 0);
        var eje = Line.CreateBound(centro, centro + XYZ.BasisZ);
        var anguloRadianes = anguloGrados * Math.PI / 180.0;

        int rotados = 0;
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Rotar {ids.Count} Elementos"))
        {
            tx.Start();
            foreach (var id in ids)
            {
                try
                {
                    if (doc.GetElement(id) == null) { errores.Add($"Elemento {id.Value} no encontrado."); continue; }
                    ElementTransformUtils.RotateElement(doc, id, eje, anguloRadianes);
                    rotados++;
                }
                catch (Exception ex)
                {
                    errores.Add(ex.Message);
                }
            }
            tx.Commit();
        }

        return new { ElementosRotados = rotados, ErroresOmitidos = errores.Count, Errores = errores };
    }
}
