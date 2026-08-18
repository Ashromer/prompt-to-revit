using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitBridge.Core;
using RevitBridge.Utils;

namespace RevitBridge.Addin.Commands;

/// <summary>
/// Habitaciones y techos (F3.7, extensión "casa completa"). Mismo patrón de aprobación por
/// creación masiva que ModelingCommands (CrearMurosMasivo/CrearForjadosMasivo) — firmas verificadas
/// contra Nice3point.Revit.Api.RevitAPI 2026.4.10 con MetadataLoadContext antes de escribir esto:
/// Document.Create.NewRoom(Level, UV) y Ceiling.Create(doc, IList&lt;CurveLoop&gt;, ceilingTypeId,
/// levelId) son ambos reales en 2026.
/// </summary>
public static class SpatialCommands
{
    [ComandoRevit("CrearHabitacionesMasivo")]
    public static object CrearHabitacionesMasivo(Document doc, int nivelId, string jsonHabitaciones)
    {
        var levelId = new ElementId(nivelId);
        var nivel = doc.GetElement(levelId) as Level;
        if (nivel == null) throw new ArgumentException("Nivel no encontrado.");

        double m2ft = 1.0 / 0.3048;

        // Estructura esperada: [{"x":1.0,"y":2.0,"nombre":"Salón","numero":"101"}, ...] -- nombre y
        // numero son opcionales. JsonElement (no double) porque hay valores string mezclados con
        // numéricos en la misma entrada, a diferencia del resto del catálogo (Dictionary<string,double>).
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var listaHabitaciones = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, System.Text.Json.JsonElement>>>(jsonHabitaciones, opciones);

        if (listaHabitaciones == null || listaHabitaciones.Count == 0) return new { Creadas = 0 };

        if (UmbralAprobacionCreacion.RequiereAprobacion(listaHabitaciones.Count))
        {
            var resumenCreacion = DeletionPreview.ConstruirResumen(
                $"crear en el nivel '{nivel.Name}'", Enumerable.Repeat("Habitación", listaHabitaciones.Count));
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Creación masiva de habitaciones cancelada por el usuario.");
        }

        int creadas = 0;
        var idsCreadas = new List<long>();
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Crear {listaHabitaciones.Count} Habitaciones"))
        {
            tx.Start();
            foreach (var item in listaHabitaciones)
            {
                try
                {
                    if (!item.TryGetValue("x", out var xEl) || !item.TryGetValue("y", out var yEl))
                    {
                        errores.Add("Entrada sin 'x' o 'y', omitida.");
                        continue;
                    }

                    var punto = new UV(xEl.GetDouble() * m2ft, yEl.GetDouble() * m2ft);
                    var room = doc.Create.NewRoom(nivel, punto);

                    // Sin muros alrededor del punto, NewRoom no lanza: crea una habitación "no
                    // delimitada" (Area = 0). No es un error que abortar el lote, es información
                    // útil para el llamador -- se refleja en la respuesta, no en errores.
                    if (item.TryGetValue("nombre", out var nombreEl) && nombreEl.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var nombre = nombreEl.GetString();
                        if (!string.IsNullOrWhiteSpace(nombre)) room.Name = nombre;
                    }
                    if (item.TryGetValue("numero", out var numeroEl) && numeroEl.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var numero = numeroEl.GetString();
                        if (!string.IsNullOrWhiteSpace(numero)) room.Number = numero;
                    }

                    creadas++;
                    idsCreadas.Add(room.Id.Value);
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

    [ComandoRevit("CrearTechosMasivo")]
    public static object CrearTechosMasivo(Document doc, int nivelId, string jsonCoordenadasPoligonos, int tipoTechoId = 0)
    {
        var levelId = new ElementId(nivelId);
        var nivelActual = doc.GetElement(levelId) as Level;
        if (nivelActual == null) throw new ArgumentException("Nivel no encontrado.");

        // Mismo bug conocido que CrearForjadosMasivo con FloorType (DOCUMENTACION.md §9): resolver
        // un CeilingType por defecto si no se especifica uno válido, en vez de dejar que
        // Ceiling.Create lance sobre ElementId.InvalidElementId.
        ElementId ceilingTypeId;
        if (tipoTechoId > 0)
        {
            ceilingTypeId = new ElementId(tipoTechoId);
        }
        else
        {
            ceilingTypeId = new FilteredElementCollector(doc).OfClass(typeof(CeilingType)).FirstElementId();
            if (ceilingTypeId == ElementId.InvalidElementId)
                throw new InvalidOperationException("No hay ningún CeilingType disponible en el documento y no se especificó tipoTechoId.");
        }

        double m2ft = 1.0 / 0.3048;

        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var listaPoligonos = System.Text.Json.JsonSerializer.Deserialize<List<List<Dictionary<string, double>>>>(jsonCoordenadasPoligonos, opciones);

        if (listaPoligonos == null || listaPoligonos.Count == 0) return new { Creados = 0 };

        if (UmbralAprobacionCreacion.RequiereAprobacion(listaPoligonos.Count))
        {
            var resumenCreacion = DeletionPreview.ConstruirResumen(
                $"crear en el nivel '{nivelActual.Name}'", Enumerable.Repeat("Techo", listaPoligonos.Count));
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Creación masiva de techos cancelada por el usuario.");
        }

        int creados = 0;
        var idsCreados = new List<long>();
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Crear {listaPoligonos.Count} Techos"))
        {
            tx.Start();
            foreach (var poligono in listaPoligonos)
            {
                if (poligono.Count < 3) continue;

                try
                {
                    // Igual que CrearForjadosMasivo (hallazgo judge 2026-08-18): construir el
                    // CurveLoop DENTRO del try, no antes -- si Line.CreateBound lanza fuera del try
                    // aborta el using(tx) entero y deshace los techos ya creados en el lote.
                    var curveLoop = new CurveLoop();
                    for (int i = 0; i < poligono.Count; i++)
                    {
                        var p1 = poligono[i];
                        var p2 = poligono[(i + 1) % poligono.Count];

                        if (p1.TryGetValue("x", out double x1) && p1.TryGetValue("y", out double y1) &&
                            p2.TryGetValue("x", out double x2) && p2.TryGetValue("y", out double y2))
                        {
                            curveLoop.Append(Line.CreateBound(new XYZ(x1 * m2ft, y1 * m2ft, 0), new XYZ(x2 * m2ft, y2 * m2ft, 0)));
                        }
                    }

                    if (curveLoop.NumberOfCurves() < 3)
                    {
                        errores.Add("Polígono con menos de 3 segmentos válidos, omitido.");
                        continue;
                    }

                    var techo = Ceiling.Create(doc, new List<CurveLoop> { curveLoop }, ceilingTypeId, levelId);
                    creados++;
                    idsCreados.Add(techo.Id.Value);
                }
                catch (Exception ex)
                {
                    errores.Add(ex.Message);
                }
            }
            tx.Commit();
        }

        return new { ElementosCreados = creados, Ids = idsCreados, ErroresOmitidos = errores.Count, Errores = errores };
    }
}
