using System.Diagnostics;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBridge.Core;

namespace RevitBridge.Addin;

/// <summary>
/// Adaptador de la API de Revit para consultas de solo lectura (F0.6, F0.8).
/// Mantiene la costura de abstracción implementando <see cref="IRevitQueryContext"/>.
/// </summary>
public sealed class RevitContext : IRevitQueryContext
{
    private readonly UIApplication _uiapp;
    private readonly Document? _doc;

    public RevitContext(UIApplication uiapp)
    {
        _uiapp = uiapp;
        _doc = uiapp.ActiveUIDocument?.Document;
    }

    public object? Consultar(ConsultaRequest peticion)
    {
        if (_doc is null)
        {
            throw new InvalidOperationException("No hay ningún documento activo en Revit.");
        }

        // Lógica trivial inicial para F0.8 (Consulta del modelo).
        // Se puede ampliar para soportar filtros específicos u otros datos en base a peticion.Consulta
        
        var queryName = peticion.Consulta.ToLowerInvariant();
        
        return queryName switch
        {
            "niveles" => ConsultarNiveles(),
            "tipos" => ConsultarTipos(),
            "seleccion" => ConsultarSeleccion(),
            "documento" => ConsultarDocumento(),
            _ => throw new ArgumentException($"Consulta '{peticion.Consulta}' no soportada por el adaptador actual.")
        };
    }

    private object ConsultarDocumento()
    {
        return new
        {
            Titulo = _doc!.Title,
            Path = _doc.PathName,
            Unidades = _doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId().TypeId
        };
    }

    private object ConsultarNiveles()
    {
        var niveles = new FilteredElementCollector(_doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .Select(l => new { Id = l.Id.Value, Nombre = l.Name, Elevacion = l.Elevation })
            .ToList();
            
        return niveles;
    }

    private object ConsultarTipos()
    {
        var tipos = new FilteredElementCollector(_doc)
            .WhereElementIsElementType()
            .ToElements()
            .Select(e => new { Id = e.Id.Value, Nombre = e.Name, Categoria = e.Category?.Name })
            .Take(100) // Limitamos para no ahogar JSON
            .ToList();
            
        return tipos;
    }

    private object ConsultarSeleccion()
    {
        var uiDoc = _uiapp.ActiveUIDocument;
        if (uiDoc is null) return Array.Empty<object>();

        var selection = uiDoc.Selection.GetElementIds();
        return selection.Select(id => 
        {
            var elem = _doc!.GetElement(id);
            return new { Id = id.Value, Nombre = elem?.Name, Tipo = elem?.GetType().Name };
        }).ToList();
    }

    /// <summary>
    /// Procesador principal de peticiones que se inyecta en la cola de ejecución.
    /// Recibe un <see cref="PeticionPipe"/> y rutea a la operación correspondiente.
    /// </summary>
    public RespuestaOperacion Procesar(PeticionPipe peticion)
    {
        var sw = Stopwatch.StartNew();
        
        if (peticion.Operacion == Operaciones.Query)
        {
            var req = peticion.Datos.Deserialize<ConsultaRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req is null) throw new InvalidOperationException("El payload de ConsultaRequest es inválido o nulo.");
            
            var resultado = Consultar(req);
            
            return new RespuestaOperacion(
                Ok: true,
                Fase: Fase.Ok,
                Resultado: resultado,
                IdsCreados: Array.Empty<long>(),
                Error: null,
                Traza: null,
                DuracionMs: sw.ElapsedMilliseconds);
        }
        else if (peticion.Operacion == Operaciones.Commands)
        {
            // F0.9 (Catálogo)
            var catalogo = Bridge.CommandCatalog.Descubrir(typeof(Utils.ComandoRevitAttribute).Assembly);
            return new RespuestaOperacion(
                Ok: true,
                Fase: Fase.Ok,
                Resultado: catalogo,
                IdsCreados: Array.Empty<long>(),
                Error: null,
                Traza: null,
                DuracionMs: sw.ElapsedMilliseconds);
        }
        
        // Operaciones de escritura (Exec, Command, Rollback) van en Tier 1 y 2.
        throw new NotSupportedException($"La operación '{peticion.Operacion}' todavía no está implementada en Tier 0.");
    }
}
