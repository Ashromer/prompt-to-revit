using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitBridge.Utils;

namespace RevitBridge.Addin.Commands;

/// <summary>
/// Comandos maestros para la gestión masiva de parámetros y nomenclatura.
/// Fagocitados de decenas de scripts de Python (adds_prefix, uppercase_grids, modify_parameters).
/// </summary>
public static class ParamCommands
{
    [ComandoRevit("ModificarParametroTextoCategoria")]
    public static object ModificarParametroTextoCategoria(Document doc, string categoriaBuiltIn, string parametroNombre, string prefijo, string sufijo, bool uppercase)
    {
        if (!Enum.TryParse(categoriaBuiltIn, out BuiltInCategory catEnum))
            throw new ArgumentException($"La categoría '{categoriaBuiltIn}' no es válida.");

        var collector = new FilteredElementCollector(doc)
            .OfCategory(catEnum)
            .WhereElementIsNotElementType()
            .ToList();

        // F2.5 Protección de elementos preexistentes (§5.C.10/§5.D.15): modificar algo que no se
        // creó en esta sesión exige aprobación manual siempre, sin excepción -- igual que
        // ModelingCommands.ModificarParametro. Faltaba aquí (hallazgo de auditoría 2026-08-18):
        // este comando modificaba parámetros de toda una categoría de elementos preexistentes
        // sin pedir aprobación en ningún caso.
        var preexistentes = collector.Where(e => !App.ElementosCreadosEnSesion.Contains(e.Id.Value)).ToList();
        if (preexistentes.Count > 0)
        {
            var resumenAprobacion = $"ATENCIÓN: Vas a modificar el parámetro '{parametroNombre}' de {preexistentes.Count} elemento(s) preexistentes de la categoría '{categoriaBuiltIn}'. ¿Aprobar?";
            var approval = new RevitBridge.Addin.UI.ApprovalService();
            if (!approval.SolicitarAprobacion(resumenAprobacion))
                throw new InvalidOperationException("Modificación masiva de elementos preexistentes cancelada por el usuario.");
        }

        int afectados = 0;
        using (var tx = new Transaction(doc, $"Modificar parámetros {categoriaBuiltIn}"))
        {
            tx.Start();
            foreach (var elem in collector)
            {
                var param = elem.LookupParameter(parametroNombre);
                if (param != null && !param.IsReadOnly && param.StorageType == StorageType.String)
                {
                    string valActual = param.AsString() ?? "";
                    if (uppercase) valActual = valActual.ToUpperInvariant();
                    string nuevoValor = $"{prefijo}{valActual}{sufijo}";
                    
                    param.Set(nuevoValor);
                    afectados++;
                }
            }
            tx.Commit();
        }

        return new { Categoria = categoriaBuiltIn, Parametro = parametroNombre, ElementosModificados = afectados };
    }
}
