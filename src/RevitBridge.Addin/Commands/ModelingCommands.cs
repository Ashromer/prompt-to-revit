using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitBridge.Core;
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

    [ComandoRevit("CrearMuroCurvo")]
    public static object CrearMuroCurvo(Document doc, int nivelId, double centroXMetros, double centroYMetros, double radioMetros, double anguloInicioGrados, double anguloFinGrados, int tipoMuroId = 0)
    {
        // F3.7: CrearMuroRecto/CrearMurosMasivo solo cubrían tramos rectos (Line). Wall.Create
        // acepta cualquier Curve (firma verificada con MetadataLoadContext: Create(Document, Curve,
        // ElementId levelId, bool structural) -- Curve, no Line), así que un Arc real es la misma
        // llamada, no un comando nuevo por dentro.
        var levelId = new ElementId(nivelId);
        var wallTypeId = tipoMuroId > 0 ? new ElementId(tipoMuroId) : ElementId.InvalidElementId;

        double m2ft = 1.0 / 0.3048;
        var centro = new XYZ(centroXMetros * m2ft, centroYMetros * m2ft, 0);
        var arco = Arc.Create(centro, radioMetros * m2ft,
            anguloInicioGrados * Math.PI / 180.0, anguloFinGrados * Math.PI / 180.0,
            XYZ.BasisX, XYZ.BasisY);

        Wall? wall = null;
        using (var tx = new Transaction(doc, "Crear Muro Curvo MCP"))
        {
            tx.Start();
            wall = Wall.Create(doc, arco, levelId, false);
            if (tipoMuroId > 0) wall.ChangeTypeId(wallTypeId);
            tx.Commit();
        }

        return new { Id = wall.Id.Value, Tipo = wall.WallType.Name, Longitud = arco.Length };
    }

    [ComandoRevit("CrearMurosMasivo")]
    public static object CrearMurosMasivo(Document doc, int nivelId, string jsonCoordenadas, double alturaMetros = 3.0, int tipoMuroId = 0)
    {
        // Vital para F3.2 (Modelado VLM): Recibe un JSON de 100 muros y los genera de golpe.
        var levelId = new ElementId(nivelId);
        var nivelActual = doc.GetElement(levelId) as Level;
        if (nivelActual == null) throw new ArgumentException("Nivel no encontrado.");

        // Bug conocido (DOCUMENTACION.md §9, confirmado en Revit vivo): sin Top Constraint, Revit
        // asigna una altura no conectada por defecto que invade los niveles superiores y los muros
        // aparecen solapados. Preferir el siguiente nivel por elevación como restricción superior;
        // si no hay ninguno por encima, usar altura desconectada explícita (alturaMetros).
        var nivelSuperior = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .Where(l => l.Elevation > nivelActual.Elevation)
            .OrderBy(l => l.Elevation)
            .FirstOrDefault();

        double m2ft = 1.0 / 0.3048;

        // Estructura esperada: [{"p1x":0,"p1y":0,"p2x":5,"p2y":0}, ...]
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var listaMuros = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, double>>>(jsonCoordenadas, opciones);

        if (listaMuros == null || listaMuros.Count == 0) return new { Creados = 0 };

        // Salvaguarda nueva (hallazgo independiente de las dos sesiones de architect de ADR-012,
        // CAD y PDF/VLM, 2026-08-18): creación masiva sin ningún punto de revisión humana rompe en
        // espíritu §5.D.14. Previsualización + aprobación, mismo patrón que BorrarElementosMasivo.
        if (UmbralAprobacionCreacion.RequiereAprobacion(listaMuros.Count))
        {
            var resumenCreacion = DeletionPreview.ConstruirResumen(
                $"crear en el nivel '{nivelActual.Name}'", Enumerable.Repeat("Muro", listaMuros.Count));
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Creación masiva de muros cancelada por el usuario.");
        }

        int creados = 0;
        var idsCreados = new List<long>();
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Crear {listaMuros.Count} Muros (VLM)"))
        {
            tx.Start();
            foreach (var coords in listaMuros)
            {
                try
                {
                    if (!coords.TryGetValue("p1x", out double p1x) || !coords.TryGetValue("p1y", out double p1y) ||
                        !coords.TryGetValue("p2x", out double p2x) || !coords.TryGetValue("p2y", out double p2y))
                    {
                        errores.Add("Entrada sin 'p1x'/'p1y'/'p2x'/'p2y', omitida.");
                        continue;
                    }

                    Line geomLine = Line.CreateBound(new XYZ(p1x * m2ft, p1y * m2ft, 0), new XYZ(p2x * m2ft, p2y * m2ft, 0));
                    var wall = Wall.Create(doc, geomLine, levelId, false);

                    if (tipoMuroId > 0)
                    {
                        wall.ChangeTypeId(new ElementId(tipoMuroId));
                    }

                    if (nivelSuperior != null)
                    {
                        wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE)?.Set(nivelSuperior.Id);
                    }
                    else
                    {
                        wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.Set(alturaMetros * m2ft);
                    }

                    creados++;
                    idsCreados.Add(wall.Id.Value);
                }
                catch (Exception ex)
                {
                    // Un muro degenerado (puntos coincidentes de CAD/VLM, p. ej.) no debe abortar
                    // el batch entero -- sin este catch, Line.CreateBound/Wall.Create lanzaba fuera
                    // del using(tx) y el rollback implícito deshacía también los muros ya creados.
                    errores.Add(ex.Message);
                }
            }
            tx.Commit();
        }

        return new { ElementosCreados = creados, Ids = idsCreados, ErroresOmitidos = errores.Count, Errores = errores };
    }

    [ComandoRevit("CrearForjadosMasivo")]
    public static object CrearForjadosMasivo(Document doc, int nivelId, string jsonCoordenadasPoligonos, int tipoSueloId = 0)
    {
        var levelId = new ElementId(nivelId);
        var nivelActual = doc.GetElement(levelId) as Level;
        if (nivelActual == null) throw new ArgumentException("Nivel no encontrado.");

        // Bug conocido (DOCUMENTACION.md §9, confirmado en Revit vivo): pasar
        // ElementId.InvalidElementId a Floor.Create lanza una excepción -- quien no revisa la
        // respuesta del pipe (p. ej. un script que descarta la respuesta) lo ve como "no se genera
        // nada" en silencio. Resolver un FloorType por defecto si no se especifica uno válido.
        ElementId floorTypeId;
        if (tipoSueloId > 0)
        {
            floorTypeId = new ElementId(tipoSueloId);
        }
        else
        {
            floorTypeId = new FilteredElementCollector(doc).OfClass(typeof(FloorType)).FirstElementId();
            if (floorTypeId == ElementId.InvalidElementId)
                throw new InvalidOperationException("No hay ningún FloorType disponible en el documento y no se especificó tipoSueloId.");
        }

        double m2ft = 1.0 / 0.3048;

        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        // Estructura esperada: array de poligonos, cada poligono es array de puntos {"x":0,"y":0}
        var listaPoligonos = System.Text.Json.JsonSerializer.Deserialize<List<List<Dictionary<string, double>>>>(jsonCoordenadasPoligonos, opciones);

        if (listaPoligonos == null || listaPoligonos.Count == 0) return new { Creados = 0 };

        // Misma salvaguarda que CrearMurosMasivo (ver comentario arriba).
        if (UmbralAprobacionCreacion.RequiereAprobacion(listaPoligonos.Count))
        {
            var resumenCreacion = DeletionPreview.ConstruirResumen(
                $"crear en el nivel '{nivelActual.Name}'", Enumerable.Repeat("Forjado", listaPoligonos.Count));
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Creación masiva de forjados cancelada por el usuario.");
        }

        int creados = 0;
        var idsCreados = new List<long>();
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Crear {listaPoligonos.Count} Forjados (VLM)"))
        {
            tx.Start();
            foreach (var poligono in listaPoligonos)
            {
                if (poligono.Count < 3) continue;

                try
                {
                    // Line.CreateBound también puede lanzar (vértices coincidentes/degenerados, p.
                    // ej. de CAD o VLM) -- tiene que vivir DENTRO de este try, no antes: si lanza
                    // fuera del try aborta el using(tx) entero y el rollback implícito deshace
                    // también los forjados de iteraciones anteriores ya creados.
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

                    // Polígono degenerado (puntos con claves x/y ausentes dejaron menos segmentos
                    // que vértices): saltar en vez de dejar que Floor.Create lance sobre un
                    // CurveLoop inválido.
                    if (curveLoop.NumberOfCurves() < 3)
                    {
                        errores.Add("Polígono con menos de 3 segmentos válidos, omitido.");
                        continue;
                    }

                    var floor = Floor.Create(doc, new List<CurveLoop> { curveLoop }, floorTypeId, levelId);
                    creados++;
                    idsCreados.Add(floor.Id.Value);
                }
                catch (Exception ex)
                {
                    // Un polígono inválido (autointersección, no coplanar, vértices degenerados) no
                    // debe abortar el resto del lote -- se agrega el error, no se lista uno por uno
                    // (revit_api_knowledge.md, patrón de módulos estrechos).
                    errores.Add(ex.Message);
                }
            }
            tx.Commit();
        }

        return new { ElementosCreados = creados, Ids = idsCreados, ErroresOmitidos = errores.Count, Errores = errores };
    }

    [ComandoRevit("CrearAberturasMasivo")]
    public static object CrearAberturasMasivo(Document doc, int muroId, string jsonAberturas)
    {
        // F3.7 (backlog "casa completa"): hueco de catálogo real -- no existía ningún comando de
        // colocación de puertas/ventanas (confirmado por las dos sesiones de architect de ADR-012).
        var muro = doc.GetElement(new ElementId(muroId)) as Wall;
        if (muro == null) throw new ArgumentException("Muro no encontrado.");

        var locationCurve = muro.Location as LocationCurve;
        if (locationCurve == null) throw new InvalidOperationException("El muro no tiene una curva de ubicación válida (¿es un muro curvo o de perfil?).");
        var curva = locationCurve.Curve;

        double m2ft = 1.0 / 0.3048;

        // Estructura esperada: [{"tipoId": 123, "distanciaMetros": 1.5}, ...] -- distanciaMetros es
        // la posición a lo largo del muro desde su punto de inicio (usar ObtenerTiposCargadosPorCategoria
        // para resolver tipoId real de puerta/ventana antes de llamar, §5.A.1: nunca adivinar un tipo)
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var listaAberturas = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, double>>>(jsonAberturas, opciones);

        if (listaAberturas == null || listaAberturas.Count == 0) return new { Creados = 0 };

        if (UmbralAprobacionCreacion.RequiereAprobacion(listaAberturas.Count))
        {
            var resumenCreacion = DeletionPreview.ConstruirResumen(
                $"crear en el muro (ID {muroId})", Enumerable.Repeat("Abertura", listaAberturas.Count));
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Creación masiva de aberturas cancelada por el usuario.");
        }

        int creados = 0;
        var idsCreados = new List<long>();
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Crear {listaAberturas.Count} Aberturas"))
        {
            tx.Start();
            foreach (var abertura in listaAberturas)
            {
                try
                {
                    if (!abertura.TryGetValue("tipoId", out double tipoIdRaw) || !abertura.TryGetValue("distanciaMetros", out double distanciaMetros))
                    {
                        errores.Add("Entrada sin 'tipoId' o 'distanciaMetros', omitida.");
                        continue;
                    }

                    var symbol = doc.GetElement(new ElementId((int)tipoIdRaw)) as FamilySymbol;
                    if (symbol == null)
                    {
                        errores.Add($"El tipo {(int)tipoIdRaw} no es un FamilySymbol válido (¿lo obtuviste de ObtenerTiposCargadosPorCategoria?).");
                        continue;
                    }

                    if (!symbol.IsActive) symbol.Activate();

                    var normalizado = curva.Length > 0 ? (distanciaMetros * m2ft) / curva.Length : 0.0;
                    normalizado = Math.Clamp(normalizado, 0.0, 1.0);
                    var punto = curva.Evaluate(normalizado, true);

                    var instancia = doc.Create.NewFamilyInstance(punto, symbol, muro, StructuralType.NonStructural);

                    creados++;
                    idsCreados.Add(instancia.Id.Value);
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

    [ComandoRevit("ColocarMobiliarioMasivo")]
    public static object ColocarMobiliarioMasivo(Document doc, int nivelId, string jsonMobiliario)
    {
        // F3.7: family-based, no host-based (a diferencia de CrearAberturasMasivo) -- mobiliario se
        // apoya en el nivel, no en un muro.
        var nivel = doc.GetElement(new ElementId(nivelId)) as Level;
        if (nivel == null) throw new ArgumentException("Nivel no encontrado.");

        double m2ft = 1.0 / 0.3048;

        // Estructura esperada: [{"tipoId": 123, "x": 1.0, "y": 2.0, "rotacionGrados": 90}, ...]
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var listaMobiliario = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, double>>>(jsonMobiliario, opciones);

        if (listaMobiliario == null || listaMobiliario.Count == 0) return new { Creados = 0 };

        if (UmbralAprobacionCreacion.RequiereAprobacion(listaMobiliario.Count))
        {
            var resumenCreacion = DeletionPreview.ConstruirResumen(
                $"crear en el nivel '{nivel.Name}'", Enumerable.Repeat("Mobiliario", listaMobiliario.Count));
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Colocación masiva de mobiliario cancelada por el usuario.");
        }

        int creados = 0;
        var idsCreados = new List<long>();
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Colocar {listaMobiliario.Count} Mobiliario"))
        {
            tx.Start();
            foreach (var item in listaMobiliario)
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
                        errores.Add($"El tipo {(int)tipoIdRaw} no es un FamilySymbol válido (¿lo obtuviste de ObtenerTiposCargadosPorCategoria?).");
                        continue;
                    }

                    if (!symbol.IsActive) symbol.Activate();

                    var punto = new XYZ(x * m2ft, y * m2ft, nivel.Elevation);
                    var instancia = doc.Create.NewFamilyInstance(punto, symbol, nivel, StructuralType.NonStructural);

                    if (item.TryGetValue("rotacionGrados", out double rotacionGrados) && rotacionGrados != 0)
                    {
                        var eje = Line.CreateBound(punto, punto + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(doc, instancia.Id, eje, rotacionGrados * Math.PI / 180.0);
                    }

                    creados++;
                    idsCreados.Add(instancia.Id.Value);
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

    [ComandoRevit("CrearBarandillasMasivo")]
    public static object CrearBarandillasMasivo(Document doc, int nivelId, string jsonRecorridos, int tipoBarandillaId = 0)
    {
        // F3.7: barandillas independientes (balcones, huecos de escalera) -- no las asociadas a una
        // escalera/rampa, que es un overload distinto de Railing.Create no cubierto aquí. Firma
        // verificada con MetadataLoadContext: Railing.Create(Document, CurveLoop, ElementId
        // railingTypeId, ElementId baseLevelId) es real en 2026.4.10; CurveLoop puede ser un
        // recorrido ABIERTO, no hace falta cerrarlo (mismo hallazgo que la fibra neutra de
        // CreateViaThicken en revit_api_knowledge.md).
        var levelId = new ElementId(nivelId);
        var nivel = doc.GetElement(levelId) as Level;
        if (nivel == null) throw new ArgumentException("Nivel no encontrado.");

        ElementId railingTypeId;
        if (tipoBarandillaId > 0)
        {
            railingTypeId = new ElementId(tipoBarandillaId);
        }
        else
        {
            railingTypeId = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Architecture.RailingType)).FirstElementId();
            if (railingTypeId == ElementId.InvalidElementId)
                throw new InvalidOperationException("No hay ningún RailingType disponible en el documento y no se especificó tipoBarandillaId.");
        }

        double m2ft = 1.0 / 0.3048;

        // Estructura esperada: array de recorridos, cada uno una lista de puntos {"x":0,"y":0}
        // ABIERTA (no se cierra el bucle, a diferencia de CrearForjadosMasivo/CrearTechosMasivo).
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var listaRecorridos = System.Text.Json.JsonSerializer.Deserialize<List<List<Dictionary<string, double>>>>(jsonRecorridos, opciones);

        if (listaRecorridos == null || listaRecorridos.Count == 0) return new { Creadas = 0 };

        if (UmbralAprobacionCreacion.RequiereAprobacion(listaRecorridos.Count))
        {
            var resumenCreacion = DeletionPreview.ConstruirResumen(
                $"crear en el nivel '{nivel.Name}'", Enumerable.Repeat("Barandilla", listaRecorridos.Count));
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Creación masiva de barandillas cancelada por el usuario.");
        }

        int creadas = 0;
        var idsCreadas = new List<long>();
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Crear {listaRecorridos.Count} Barandillas"))
        {
            tx.Start();
            foreach (var recorrido in listaRecorridos)
            {
                if (recorrido.Count < 2) continue;

                try
                {
                    // Igual que CrearForjadosMasivo/CrearTechosMasivo (hallazgo judge 2026-08-18):
                    // construir el CurveLoop DENTRO del try, no antes.
                    var curveLoop = new CurveLoop();
                    for (int i = 0; i < recorrido.Count - 1; i++)
                    {
                        var p1 = recorrido[i];
                        var p2 = recorrido[i + 1];

                        if (p1.TryGetValue("x", out double x1) && p1.TryGetValue("y", out double y1) &&
                            p2.TryGetValue("x", out double x2) && p2.TryGetValue("y", out double y2))
                        {
                            curveLoop.Append(Line.CreateBound(new XYZ(x1 * m2ft, y1 * m2ft, 0), new XYZ(x2 * m2ft, y2 * m2ft, 0)));
                        }
                    }

                    if (curveLoop.NumberOfCurves() < 1)
                    {
                        errores.Add("Recorrido con menos de 2 puntos válidos, omitido.");
                        continue;
                    }

                    var barandilla = Autodesk.Revit.DB.Architecture.Railing.Create(doc, curveLoop, railingTypeId, levelId);
                    creadas++;
                    idsCreadas.Add(barandilla.Id.Value);
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

    [ComandoRevit("ModificarParametro")]
    public static object ModificarParametro(Document doc, int elementoId, string parametroNombre, string valor)
    {
        var elem = doc.GetElement(new ElementId(elementoId));
        if (elem == null) throw new ArgumentException("Elemento no encontrado");

        Parameter? param = elem.LookupParameter(parametroNombre);
        if (param == null) throw new ArgumentException($"El parámetro '{parametroNombre}' no existe en este elemento.");
        if (param.IsReadOnly) throw new InvalidOperationException($"El parámetro '{parametroNombre}' es de solo lectura.");

        // F2.5 Protección de elementos preexistentes (helper compartido en Core: RevitBridge.Core.PreexistingElementGuard)
        if (PreexistingElementGuard.RequiereAprobacion(new[] { (long)elementoId }, App.ElementosCreadosEnSesion))
        {
            var approval = new RevitBridge.Addin.UI.ApprovalService();
            if (!approval.SolicitarAprobacion($"ATENCIÓN: Vas a modificar el parámetro '{parametroNombre}' de un elemento preexistente (ID {elementoId}). ¿Aprobar?"))
                throw new InvalidOperationException("Modificación de elemento preexistente cancelada por el usuario.");
        }

        using (var tx = new Transaction(doc, "Modificar Parámetro MCP"))
        {
            tx.Start();
            EstablecerValorParametro(param, valor);
            tx.Commit();
        }

        return new { Id = elem.Id.Value, Parametro = parametroNombre, NuevoValor = param.AsValueString() ?? valor };
    }

    [ComandoRevit("ModificarParametrosMasivo")]
    public static object ModificarParametrosMasivo(Document doc, string jsonModificaciones)
    {
        // Composición de lo ya existente (ModificarParametro), no una capacidad nueva de Revit:
        // el hueco real era la UX -- rellenar N elementos desde datos externos (p. ej. un PDF de
        // materiales que Claude ya sabe leer) significaba N ventanas de aprobación sueltas, que es
        // exactamente el riesgo de "aprobar mecánicamente sin mirar" que DOCUMENTACION.md señala
        // para la revisión humana. Una única aprobación con el resumen agregado del lote.
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var modificaciones = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, System.Text.Json.JsonElement>>>(jsonModificaciones, opciones);

        if (modificaciones == null || modificaciones.Count == 0) return new { Modificados = 0 };

        var entradas = new List<(ElementId ElemId, string Parametro, string Valor)>();
        foreach (var mod in modificaciones)
        {
            if (mod.TryGetValue("elementoId", out var idEl) &&
                mod.TryGetValue("parametro", out var paramEl) &&
                mod.TryGetValue("valor", out var valorEl))
            {
                entradas.Add((new ElementId(idEl.GetInt32()), paramEl.GetString() ?? "", valorEl.ToString()));
            }
        }

        if (entradas.Count == 0) return new { Modificados = 0 };

        // F2.5, vía el mismo helper compartido que el resto del catálogo.
        var idsLong = entradas.Select(e => (long)e.ElemId.Value);
        if (PreexistingElementGuard.RequiereAprobacion(idsLong, App.ElementosCreadosEnSesion))
        {
            var categorias = entradas.Select(e => doc.GetElement(e.ElemId)?.Category?.Name ?? "Desconocido");
            var resumen = DeletionPreview.ConstruirResumen("modificar parámetros de", categorias);
            var approval = new RevitBridge.Addin.UI.ApprovalService();
            if (!approval.SolicitarAprobacion(resumen))
                throw new InvalidOperationException("Modificación masiva de parámetros cancelada por el usuario.");
        }

        int modificados = 0;
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Modificar {entradas.Count} Parámetros MCP"))
        {
            tx.Start();
            foreach (var (elemId, parametroNombre, valor) in entradas)
            {
                try
                {
                    var elem = doc.GetElement(elemId);
                    if (elem == null) { errores.Add($"Elemento {elemId.Value} no encontrado."); continue; }

                    var param = elem.LookupParameter(parametroNombre);
                    if (param == null) { errores.Add($"'{parametroNombre}' no existe en el elemento {elemId.Value}."); continue; }
                    if (param.IsReadOnly) { errores.Add($"'{parametroNombre}' es de solo lectura en el elemento {elemId.Value}."); continue; }

                    EstablecerValorParametro(param, valor);
                    modificados++;
                }
                catch (Exception ex)
                {
                    errores.Add(ex.Message);
                }
            }
            tx.Commit();
        }

        return new { ElementosModificados = modificados, ErroresOmitidos = errores.Count, Errores = errores };
    }

    private static void EstablecerValorParametro(Parameter param, string valor)
    {
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

    [ComandoRevit("CrearNivelesMasivo")]
    public static object CrearNivelesMasivo(Document doc, string jsonNiveles)
    {
        // Extensión de CrearNivel para edificios de varias plantas (F3.7): en vez de N llamadas
        // sueltas a CrearNivel, una lista y una sola aprobación agregada -- mismo motivo que
        // ModificarParametrosMasivo sobre ModificarParametro.
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        // Estructura esperada: [{"nombre":"Planta 1","elevacionMetros":0}, ...]
        var listaNiveles = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, System.Text.Json.JsonElement>>>(jsonNiveles, opciones);

        if (listaNiveles == null || listaNiveles.Count == 0) return new { Creados = 0 };

        if (UmbralAprobacionCreacion.RequiereAprobacion(listaNiveles.Count))
        {
            var resumenCreacion = DeletionPreview.ConstruirResumen(
                "crear en el documento", Enumerable.Repeat("Nivel", listaNiveles.Count));
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Creación masiva de niveles cancelada por el usuario.");
        }

        int creados = 0;
        var idsCreados = new List<long>();
        var errores = new List<string>();
        using (var tx = new Transaction(doc, $"Batch Crear {listaNiveles.Count} Niveles"))
        {
            tx.Start();
            foreach (var item in listaNiveles)
            {
                try
                {
                    if (!item.TryGetValue("elevacionMetros", out var elevacionEl))
                    {
                        errores.Add("Entrada sin 'elevacionMetros', omitida.");
                        continue;
                    }

                    var nivel = Level.Create(doc, elevacionEl.GetDouble() / 0.3048);

                    if (item.TryGetValue("nombre", out var nombreEl) && nombreEl.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var nombre = nombreEl.GetString();
                        if (!string.IsNullOrWhiteSpace(nombre)) nivel.Name = nombre;
                    }

                    creados++;
                    idsCreados.Add(nivel.Id.Value);
                }
                catch (Exception ex)
                {
                    // Nivel con la misma elevación (o nombre duplicado) es el fallo más probable --
                    // Level.Name exige unicidad igual que Grid.Name.
                    errores.Add(ex.Message);
                }
            }
            tx.Commit();
        }

        return new { ElementosCreados = creados, Ids = idsCreados, ErroresOmitidos = errores.Count, Errores = errores };
    }

    [ComandoRevit("BorrarElementosMasivo")]
    public static object BorrarElementosMasivo(Document doc, List<int> elementoIds)
    {
        var ids = elementoIds.Select(id => new ElementId(id)).ToList();
        
        // F2.4 Previsualización de borrado (helper compartido en Core: RevitBridge.Core.DeletionPreview)
        var elems = ids.Select(id => doc.GetElement(id)).Where(e => e != null).ToList();
        if (elems.Count > 0)
        {
            var resumen = DeletionPreview.ConstruirResumen("borrar", elems.Select(e => e!.Category?.Name ?? "Desconocido"));

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

    [ComandoRevit("CargarFamilia")]
    public static object CargarFamilia(Document doc, string rutaArchivo)
    {
        // Backlog "casa completa": pregunta abierta resuelta -- la ruta la decide siempre quien
        // llama (Claude/usuario) en cada invocación, nunca una convención de carpeta fija embebida
        // aquí. Fijar una ruta por defecto contradiría R6 ("sin rutas fijas, sin supuestos sobre
        // esta máquina concreta"); pedirla explícitamente en cada llamada es más fricción pero es
        // la única opción que no asume nada del entorno de quien use el bridge.
        if (!File.Exists(rutaArchivo))
            throw new ArgumentException($"No existe el fichero '{rutaArchivo}'.");
        if (!rutaArchivo.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Solo se admiten ficheros .rfa.");

        Family? familia;
        using (var tx = new Transaction(doc, "Cargar Familia MCP"))
        {
            tx.Start();
            var cargada = doc.LoadFamily(rutaArchivo, out familia);
            tx.Commit();

            if (!cargada || familia == null)
                throw new InvalidOperationException(
                    $"No se pudo cargar la familia desde '{rutaArchivo}' (¿ya estaba cargada con otra versión, o el fichero no es una familia válida?).");
        }

        var tipos = familia.GetFamilySymbolIds()
            .Select(id => doc.GetElement(id) as FamilySymbol)
            .Where(simbolo => simbolo != null)
            .Select(simbolo => new { Id = simbolo!.Id.Value, Nombre = simbolo.Name })
            .ToList();

        return new { FamiliaId = familia.Id.Value, Nombre = familia.Name, Tipos = tipos };
    }

    [ComandoRevit("CrearTejadoExtrusion")]
    public static object CrearTejadoExtrusion(Document doc, int nivelId, string jsonPerfil, double profundidadMetros, int tipoTejadoId = 0)
    {
        // F3.7 (backlog "casa completa"): variante simple de tejado (perfil 2D + extrusión), antes
        // de CrearTejadoPorHuella. Firma de Document.Create.NewExtrusionRoof verificada por
        // reflexión contra el propio paquete NuGet antes de escribir esto (Nice3point.Revit.Api.
        // RevitAPI 2026.4.10), no supuesta de memoria -- sigue sin verificar en Revit vivo.
        var nivel = doc.GetElement(new ElementId(nivelId)) as Level;
        if (nivel == null) throw new ArgumentException("Nivel no encontrado.");
        if (doc.ActiveView == null) throw new InvalidOperationException("No hay ninguna vista activa para crear el plano de referencia.");

        var roofTypeId = ResolverTipoPorDefecto<RoofType>(doc, tipoTejadoId);
        var roofType = doc.GetElement(roofTypeId) as RoofType;

        double m2ft = 1.0 / 0.3048;

        // Estructura esperada: [{"x":0,"z":0}, {"x":5,"z":3}, {"x":10,"z":0}] -- perfil 2D abierto
        // (X = ancho horizontal del faldón, Z = altura sobre el nivel), en metros. Ej.: un tejado a
        // dos aguas es un perfil de 3 puntos formando una "V" invertida, no un polígono cerrado.
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var perfilPuntos = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, double>>>(jsonPerfil, opciones);
        if (perfilPuntos == null || perfilPuntos.Count < 2)
            throw new ArgumentException("El perfil necesita al menos 2 puntos.");

        if (UmbralAprobacionCreacion.RequiereAprobacion(1))
        {
            var resumenCreacion = DeletionPreview.ConstruirResumen($"crear en el nivel '{nivel.Name}'", new[] { "Tejado (extrusión)" });
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Creación de tejado cancelada por el usuario.");
        }

        ExtrusionRoof? tejado = null;
        using (var tx = new Transaction(doc, "Crear Tejado (Extrusión) MCP"))
        {
            tx.Start();

            // Plano de referencia vertical en Y=0 (sketch en X-Z), la extrusión avanza a lo largo de +Y.
            var origen = new XYZ(0, 0, nivel.Elevation);
            var refPlane = doc.Create.NewReferencePlane(origen, origen + XYZ.BasisX, XYZ.BasisZ, doc.ActiveView);

            var profile = new CurveArray();
            for (int i = 0; i < perfilPuntos.Count - 1; i++)
            {
                var p1 = perfilPuntos[i];
                var p2 = perfilPuntos[i + 1];
                if (p1.TryGetValue("x", out double x1) && p1.TryGetValue("z", out double z1) &&
                    p2.TryGetValue("x", out double x2) && p2.TryGetValue("z", out double z2))
                {
                    profile.Append(Line.CreateBound(
                        new XYZ(x1 * m2ft, 0, nivel.Elevation + z1 * m2ft),
                        new XYZ(x2 * m2ft, 0, nivel.Elevation + z2 * m2ft)));
                }
            }

            tejado = doc.Create.NewExtrusionRoof(profile, refPlane, nivel, roofType, 0, profundidadMetros * m2ft);
            tx.Commit();
        }

        return new { Id = tejado!.Id.Value, Tipo = roofType?.Name };
    }

    [ComandoRevit("CrearTejadoPorHuella")]
    public static object CrearTejadoPorHuella(Document doc, int nivelId, string jsonHuella, double pendienteGrados = 30, int tipoTejadoId = 0)
    {
        // Firma de NewFootPrintRoof y los indexadores DefinesSlope/SlopeAngle[ModelCurve] verificados
        // por reflexión contra el paquete NuGet (misma sesión que CrearTejadoExtrusion) -- son
        // indexadores por segmento del ModelCurveArray devuelto, no una propiedad única del tejado.
        var nivel = doc.GetElement(new ElementId(nivelId)) as Level;
        if (nivel == null) throw new ArgumentException("Nivel no encontrado.");

        var roofTypeId = ResolverTipoPorDefecto<RoofType>(doc, tipoTejadoId);
        var roofType = doc.GetElement(roofTypeId) as RoofType;

        double m2ft = 1.0 / 0.3048;

        // Estructura esperada: [{"x":0,"y":0}, {"x":10,"y":0}, {"x":10,"y":8}, {"x":0,"y":8}] --
        // huella cerrada en planta, en metros.
        var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var puntos = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, double>>>(jsonHuella, opciones);
        if (puntos == null || puntos.Count < 3)
            throw new ArgumentException("La huella necesita al menos 3 puntos.");

        if (UmbralAprobacionCreacion.RequiereAprobacion(1))
        {
            var resumenCreacion = DeletionPreview.ConstruirResumen($"crear en el nivel '{nivel.Name}'", new[] { "Tejado (huella)" });
            var approvalCreacion = new RevitBridge.Addin.UI.ApprovalService();
            if (!approvalCreacion.SolicitarAprobacion(resumenCreacion))
                throw new InvalidOperationException("Creación de tejado cancelada por el usuario.");
        }

        FootPrintRoof? tejado = null;
        using (var tx = new Transaction(doc, "Crear Tejado (Huella) MCP"))
        {
            tx.Start();

            var footprint = new CurveArray();
            for (int i = 0; i < puntos.Count; i++)
            {
                var p1 = puntos[i];
                var p2 = puntos[(i + 1) % puntos.Count];
                if (p1.TryGetValue("x", out double x1) && p1.TryGetValue("y", out double y1) &&
                    p2.TryGetValue("x", out double x2) && p2.TryGetValue("y", out double y2))
                {
                    footprint.Append(Line.CreateBound(
                        new XYZ(x1 * m2ft, y1 * m2ft, nivel.Elevation),
                        new XYZ(x2 * m2ft, y2 * m2ft, nivel.Elevation)));
                }
            }

            if (footprint.Size == 0)
                throw new ArgumentException($"El footprint generado está vacío. json: {jsonHuella}");

            tejado = doc.Create.NewFootPrintRoof(footprint, nivel, roofType, out ModelCurveArray mapeo);

            double pendienteRad = pendienteGrados * Math.PI / 180.0;
            foreach (ModelCurve mc in mapeo)
            {
                tejado.set_DefinesSlope(mc, true);
                tejado.set_SlopeAngle(mc, pendienteRad);
            }

            tx.Commit();
        }

        return new { Id = tejado!.Id.Value, Tipo = roofType?.Name };
    }

    [ComandoRevit("CrearTablaPlanificacion")]
    public static object CrearTablaPlanificacion(Document doc, string categoriaBuiltIn, string? camposNombres = null)
    {
        // Firma de ViewSchedule.CreateSchedule/ScheduleDefinition.AddField verificada por reflexión
        // contra el paquete NuGet antes de escribir esto, mismo criterio que los comandos de tejado.
        // Vía dedicada de consulta primero: ObtenerCamposDisponiblesParaTabla (BaseCommands.cs)
        // resuelve los nombres reales de campo antes de llamar aquí (§5.A.1, no adivinar nombres).
        if (!Enum.TryParse(categoriaBuiltIn, out BuiltInCategory catEnum))
            throw new ArgumentException($"La categoría '{categoriaBuiltIn}' no es válida.");

        List<string>? nombresPedidos = null;
        if (!string.IsNullOrWhiteSpace(camposNombres))
        {
            var opciones = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            nombresPedidos = System.Text.Json.JsonSerializer.Deserialize<List<string>>(camposNombres, opciones);
        }

        ViewSchedule? tabla = null;
        var camposAnadidos = new List<string>();
        using (var tx = new Transaction(doc, $"Crear Tabla {categoriaBuiltIn} MCP"))
        {
            tx.Start();

            tabla = ViewSchedule.CreateSchedule(doc, new ElementId(catEnum));
            var definicion = tabla.Definition;

            foreach (var campo in definicion.GetSchedulableFields())
            {
                var nombre = campo.GetName(doc);
                if (nombresPedidos == null || nombresPedidos.Contains(nombre, StringComparer.OrdinalIgnoreCase))
                {
                    definicion.AddField(campo);
                    camposAnadidos.Add(nombre);
                }
            }

            tx.Commit();
        }

        return new { Id = tabla!.Id.Value, Nombre = tabla.Name, Campos = camposAnadidos };
    }

    private static ElementId ResolverTipoPorDefecto<T>(Document doc, int tipoId) where T : ElementType
    {
        if (tipoId > 0) return new ElementId(tipoId);

        var id = new FilteredElementCollector(doc).OfClass(typeof(T)).FirstElementId();
        if (id == ElementId.InvalidElementId)
            throw new InvalidOperationException($"No hay ningún {typeof(T).Name} disponible en el documento y no se especificó un tipo.");
        return id;
    }
}
