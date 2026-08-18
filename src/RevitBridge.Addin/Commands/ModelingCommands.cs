using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitBridge.Utils;

namespace RevitBridge.Addin.Commands;

/// <summary>
/// Comandos robustos de pre-compilado para la gestión de modelado y metadatos (Tier 2).
/// Todas las modificaciones manejan su propia transacción.
/// </summary>
public static class ModelingCommands
{
    [ComandoRevit("CrearMuroRecto")]
    public static object CrearMuroRecto(Document doc, int nivelId, double p1x, double p1y, double p2x, double p2y, int tipoMuroId = 0)
    {
        var levelId = new ElementId(nivelId);
        var wallTypeId = tipoMuroId > 0 ? new ElementId(tipoMuroId) : ElementId.InvalidElementId;
        
        // Conversión implícita de asunción (metros a pies decimales), ya que la IA suele dar metros
        double m2ft = 1.0 / 0.3048;
        Line geomLine = Line.CreateBound(new XYZ(p1x * m2ft, p1y * m2ft, 0), new XYZ(p2x * m2ft, p2y * m2ft, 0));

        Wall? wall = null;
        using (var tx = new Transaction(doc, "Crear Muro MCP"))
        {
            tx.Start();
            // Si el wallType no se especifica (InvalidElementId), Revit usa el por defecto
            wall = Wall.Create(doc, geomLine, levelId, false);
            if (tipoMuroId > 0)
            {
                wall.ChangeTypeId(wallTypeId);
            }
            tx.Commit();
        }

        return new { Id = wall.Id.Value, Tipo = wall.WallType.Name, Longitud = geomLine.Length };
    }

    [ComandoRevit("CrearMurosMasivo")]
    public static object CrearMurosMasivo(Document doc, int nivelId, string jsonCoordenadas)
    {
        // Vital para F3.2 (Modelado VLM): Recibe un JSON de 100 muros y los genera de golpe.
        var levelId = new ElementId(nivelId);
        double m2ft = 1.0 / 0.3048;
        
        // Estructura esperada: [{"p1x":0,"p1y":0,"p2x":5,"p2y":0}, ...]
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var listaMuros = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, double>>>(jsonCoordenadas, opciones);
        
        if (listaMuros == null || listaMuros.Count == 0) return new { Creados = 0 };

        int creados = 0;
        using (var tx = new Transaction(doc, $"Batch Crear {listaMuros.Count} Muros (VLM)"))
        {
            tx.Start();
            foreach (var coords in listaMuros)
            {
                if (coords.TryGetValue("p1x", out double p1x) && coords.TryGetValue("p1y", out double p1y) &&
                    coords.TryGetValue("p2x", out double p2x) && coords.TryGetValue("p2y", out double p2y))
                {
                    Line geomLine = Line.CreateBound(new XYZ(p1x * m2ft, p1y * m2ft, 0), new XYZ(p2x * m2ft, p2y * m2ft, 0));
                    Wall.Create(doc, geomLine, levelId, false);
                    creados++;
                }
            }
            tx.Commit();
        }

        return new { ElementosCreados = creados };
    }

    [ComandoRevit("CrearForjadosMasivo")]
    public static object CrearForjadosMasivo(Document doc, int nivelId, string jsonCoordenadasPoligonos, int tipoSueloId = 0)
    {
        var levelId = new ElementId(nivelId);
        var floorTypeId = tipoSueloId > 0 ? new ElementId(tipoSueloId) : ElementId.InvalidElementId;
        double m2ft = 1.0 / 0.3048;
        
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        // Estructura esperada: array de poligonos, cada poligono es array de puntos {"x":0,"y":0}
        var listaPoligonos = System.Text.Json.JsonSerializer.Deserialize<List<List<Dictionary<string, double>>>>(jsonCoordenadasPoligonos, opciones);
        
        if (listaPoligonos == null || listaPoligonos.Count == 0) return new { Creados = 0 };

        int creados = 0;
        using (var tx = new Transaction(doc, $"Batch Crear {listaPoligonos.Count} Forjados (VLM)"))
        {
            tx.Start();
            foreach (var poligono in listaPoligonos)
            {
                if (poligono.Count >= 3)
                {
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
                    var floor = Floor.Create(doc, new List<CurveLoop> { curveLoop }, floorTypeId, levelId);
                    creados++;
                }
            }
            tx.Commit();
        }

        return new { ElementosCreados = creados };
    }

    [ComandoRevit("ModificarParametro")]
    public static object ModificarParametro(Document doc, int elementoId, string parametroNombre, string valor)
    {
        var elem = doc.GetElement(new ElementId(elementoId));
        if (elem == null) throw new ArgumentException("Elemento no encontrado");

        Parameter? param = elem.LookupParameter(parametroNombre);
        if (param == null) throw new ArgumentException($"El parámetro '{parametroNombre}' no existe en este elemento.");
        if (param.IsReadOnly) throw new InvalidOperationException($"El parámetro '{parametroNombre}' es de solo lectura.");

        // F2.5 Protección de elementos preexistentes
        if (!App.ElementosCreadosEnSesion.Contains(elementoId))
        {
            var approval = new RevitBridge.Addin.UI.ApprovalService();
            if (!approval.SolicitarAprobacion($"ATENCIÓN: Vas a modificar el parámetro '{parametroNombre}' de un elemento preexistente (ID {elementoId}). ¿Aprobar?"))
                throw new InvalidOperationException("Modificación de elemento preexistente cancelada por el usuario.");
        }

        using (var tx = new Transaction(doc, "Modificar Parámetro MCP"))
        {
            tx.Start();
            switch (param.StorageType)
            {
                case StorageType.String:
                    param.Set(valor);
                    break;
                case StorageType.Integer:
                    if (int.TryParse(valor, out int iVal)) param.Set(iVal);
                    else throw new ArgumentException("El valor no es un Integer válido.");
                    break;
                case StorageType.Double:
                    if (double.TryParse(valor, out double dVal)) param.Set(dVal);
                    else throw new ArgumentException("El valor no es un Double válido.");
                    break;
                case StorageType.ElementId:
                    if (int.TryParse(valor, out int idVal)) param.Set(new ElementId(idVal));
                    else throw new ArgumentException("El valor no es un ElementId válido.");
                    break;
            }
            tx.Commit();
        }

        return new { Id = elem.Id.Value, Parametro = parametroNombre, NuevoValor = param.AsValueString() ?? valor };
    }

    [ComandoRevit("CrearNivel")]
    public static object CrearNivel(Document doc, string nombre, double elevacionMetros)
    {
        double elevacionFeet = elevacionMetros / 0.3048;
        Level? nivelNuevo = null;

        using (var tx = new Transaction(doc, $"Crear Nivel {nombre}"))
        {
            tx.Start();
            nivelNuevo = Level.Create(doc, elevacionFeet);
            nivelNuevo.Name = nombre;
            tx.Commit();
        }

        return new { Id = nivelNuevo.Id.Value, Nombre = nivelNuevo.Name, ElevacionMetros = elevacionMetros };
    }

    [ComandoRevit("BorrarElementosMasivo")]
    public static object BorrarElementosMasivo(Document doc, List<int> elementoIds)
    {
        var ids = elementoIds.Select(id => new ElementId(id)).ToList();
        
        // F2.4 Previsualización de borrado
        var elems = ids.Select(id => doc.GetElement(id)).Where(e => e != null).ToList();
        if (elems.Count > 0)
        {
            var categorias = elems.GroupBy(e => e.Category?.Name ?? "Desconocido").Select(g => $"{g.Count()} de {g.Key}");
            var resumen = $"ATENCIÓN: Vas a borrar {elems.Count} elementos preexistentes:\n- " + string.Join("\n- ", categorias);
            
            var approval = new RevitBridge.Addin.UI.ApprovalService();
            if (!approval.SolicitarAprobacion(resumen))
                throw new InvalidOperationException("Borrado masivo cancelado por el usuario.");
        }

        var borradosConfirmados = new List<ElementId>();

        using (var tx = new Transaction(doc, "Borrado Masivo MCP"))
        {
            tx.Start();
            var deletedIds = doc.Delete(ids);
            tx.Commit();
            
            if (deletedIds != null)
                borradosConfirmados = deletedIds.ToList();
        }

        return new { ElementosSolicitados = ids.Count, ElementosBorrados = borradosConfirmados.Count };
    }
}
