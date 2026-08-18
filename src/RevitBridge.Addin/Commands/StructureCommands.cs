using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitBridge.Core;
using RevitBridge.Utils;

namespace RevitBridge.Addin.Commands;

/// <summary>
/// Rejillas estructurales (F3.7, extensión "casa completa"). <see cref="Grid"/> es el eje
/// estructural clásico de Revit (A, B, C.../1, 2, 3...) -- no confundir con
/// <c>CurtainGridLine</c> (rejillas de muro cortina, dominio de otro plugin de este usuario).
/// Firma verificada con MetadataLoadContext antes de escribir esto: <c>Grid.Create(Document, Line)</c>
/// es estática y real en 2026.4.10.
/// </summary>
public static class StructureCommands
{
    [ComandoRevit("CrearRejillasEstructuralesMasivo")]
    public static object CrearRejillasEstructuralesMasivo(Document doc, string jsonRejillas)
    {
        double m2ft = 1.0 / 0.3048;

        // Estructura esperada: [{"p1x":0,"p1y":0,"p2x":10,"p2y":0,"etiqueta":"A"}, ...] -- etiqueta
        // es opcional (JsonElement porque es string; el resto son double).
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var listaRejillas = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, System.Text.Json.JsonElement>>>(jsonRejillas, opciones);

        if (listaRejillas == null || listaRejillas.Count == 0) return new { Creadas = 0 };

        if (UmbralAprobacionCreacion.RequiereAprobacion(listaRejillas.Count))
        {
            var resumenCreacion = DeletionPreview.ConstruirResumen(
                "crear en el documento", Enumerable.Repeat("Rejilla estructural", listaRejillas.Count));
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Creación masiva de rejillas cancelada por el usuario.");
        }

        int creadas = 0;
        var idsCreadas = new List<long>();
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Crear {listaRejillas.Count} Rejillas Estructurales"))
        {
            tx.Start();
            foreach (var item in listaRejillas)
            {
                try
                {
                    if (!item.TryGetValue("p1x", out var p1xEl) || !item.TryGetValue("p1y", out var p1yEl) ||
                        !item.TryGetValue("p2x", out var p2xEl) || !item.TryGetValue("p2y", out var p2yEl))
                    {
                        errores.Add("Entrada sin 'p1x'/'p1y'/'p2x'/'p2y', omitida.");
                        continue;
                    }

                    var linea = Line.CreateBound(
                        new XYZ(p1xEl.GetDouble() * m2ft, p1yEl.GetDouble() * m2ft, 0),
                        new XYZ(p2xEl.GetDouble() * m2ft, p2yEl.GetDouble() * m2ft, 0));
                    var grid = Grid.Create(doc, linea);

                    if (item.TryGetValue("etiqueta", out var etiquetaEl) && etiquetaEl.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var etiqueta = etiquetaEl.GetString();
                        // Grid.Name exige unicidad -- un duplicado lanza y cae al catch de abajo,
                        // no aborta el resto del lote.
                        if (!string.IsNullOrWhiteSpace(etiqueta)) grid.Name = etiqueta;
                    }

                    creadas++;
                    idsCreadas.Add(grid.Id.Value);
                }
                catch (Exception ex)
                {
                    errores.Add(ex.Message);
                }
            }
            tx.Commit();
        }

        return new { ElementosCreados = creadas, Ids = idsCreadas, ErroresOmitidos = errores.Count, Errores = errores };
    }

    [ComandoRevit("CrearColumnasMasivo")]
    public static object CrearColumnasMasivo(Document doc, int nivelId, string jsonColumnas)
    {
        // Mismo patrón que ColocarMobiliarioMasivo (misma sobrecarga de NewFamilyInstance, ya
        // verificada y en uso) -- solo cambia StructuralType.NonStructural por .Column, que deja a
        // Revit fijar el nivel base/top automáticamente igual que hace con muros.
        var nivel = doc.GetElement(new ElementId(nivelId)) as Level;
        if (nivel == null) throw new ArgumentException("Nivel no encontrado.");

        double m2ft = 1.0 / 0.3048;

        // Estructura esperada: [{"tipoId": 123, "x": 1.0, "y": 2.0}, ...] -- tipoId de
        // ObtenerTiposCargadosPorCategoria sobre "Structural Columns" o "Columns".
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var listaColumnas = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, double>>>(jsonColumnas, opciones);

        if (listaColumnas == null || listaColumnas.Count == 0) return new { Creadas = 0 };

        if (UmbralAprobacionCreacion.RequiereAprobacion(listaColumnas.Count))
        {
            var resumenCreacion = DeletionPreview.ConstruirResumen(
                $"crear en el nivel '{nivel.Name}'", Enumerable.Repeat("Columna", listaColumnas.Count));
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Creación masiva de columnas cancelada por el usuario.");
        }

        int creadas = 0;
        var idsCreadas = new List<long>();
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Crear {listaColumnas.Count} Columnas"))
        {
            tx.Start();
            foreach (var item in listaColumnas)
            {
                try
                {
                    if (!item.TryGetValue("tipoId", out double tipoIdRaw) ||
                        !item.TryGetValue("x", out double x) || !item.TryGetValue("y", out double y))
                    {
                        errores.Add("Entrada sin 'tipoId', 'x' o 'y', omitida.");
                        continue;
                    }

                    var symbol = doc.GetElement(new ElementId((int)tipoIdRaw)) as FamilySymbol;
                    if (symbol == null)
                    {
                        errores.Add($"El tipo {(int)tipoIdRaw} no es un FamilySymbol válido.");
                        continue;
                    }

                    if (!symbol.IsActive) symbol.Activate();

                    var punto = new XYZ(x * m2ft, y * m2ft, nivel.Elevation);
                    var instancia = doc.Create.NewFamilyInstance(punto, symbol, nivel, StructuralType.Column);

                    creadas++;
                    idsCreadas.Add(instancia.Id.Value);
                }
                catch (Exception ex)
                {
                    errores.Add(ex.Message);
                }
            }
            tx.Commit();
        }

        return new { ElementosCreados = creadas, Ids = idsCreadas, ErroresOmitidos = errores.Count, Errores = errores };
    }
}
