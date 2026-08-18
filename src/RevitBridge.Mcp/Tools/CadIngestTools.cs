using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitBridge.CadIngest;

namespace RevitBridge.Mcp.Tools;

/// <summary>
/// Herramientas de ingesta CAD (ADR-012, mitad CAD de F3.2). No pasan por el named pipe: operan
/// enteramente en el proceso del puente sobre un fichero local, sin tocar Revit en ningún momento
/// -- el resultado se convierte en el JSON que ya aceptan los comandos del catálogo
/// (<c>run_command</c> con <c>CrearMurosMasivo</c>/<c>CrearForjadosMasivo</c>) solo cuando Claude
/// decide invocarlos, tras confirmar mapeo de capas y escala en la conversación (ADR-012 D2/D3).
/// Deliberadamente no incluye persistencia de perfil de mapeo (<c>%APPDATA%\RevitBridge\
/// cad-profiles\</c> del borrador de ADR-012) todavía -- es una mejora de UX para repetir importes
/// del mismo despacho, no bloqueante para el primer uso.
/// </summary>
[McpServerToolType]
public sealed class CadIngestTools
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [McpServerTool(Name = "cad_list_layers", Title = "Listar capas de un fichero CAD")]
    [Description("Abre un fichero DXF o DWG local y resume sus capas (nombre, nº de lineas/polilineas/inserts). Primer paso antes de mapear que capa es que cosa (muros, puertas...) -- ese mapeo lo confirma el usuario en la conversacion, esta herramienta no lo adivina.")]
    public string ListarCapas([Description("Ruta local al fichero .dxf o .dwg")] string ruta)
    {
        var documento = CadDocumentReader.Abrir(ruta);
        var capas = CadDocumentReader.ListarCapas(documento);
        return JsonSerializer.Serialize(capas, JsonOptions);
    }

    [McpServerTool(Name = "cad_calibrate_scale", Title = "Calibrar la escala de un fichero CAD")]
    [Description("Lee las unidades de la cabecera del fichero CAD. Si el fichero es Unitless o las unidades no tienen un factor conocido, hay que pedir al usuario que confirme la escala antes de extraer ninguna geometria (nunca asumir metros/mm por defecto).")]
    public string CalibrarEscala([Description("Ruta local al fichero .dxf o .dwg")] string ruta)
    {
        var documento = CadDocumentReader.Abrir(ruta);
        var resultado = CadScaleCalibrator.CalibrarPorCabecera(documento);
        return JsonSerializer.Serialize(resultado, JsonOptions);
    }

    [McpServerTool(Name = "cad_extract_geometry", Title = "Extraer geometria de una capa CAD a JSON")]
    [Description("Extrae los segmentos rectos de una capa del CAD (teselando arcos si los hay) ya convertidos a metros, en el mismo formato JSON que aceptan los comandos CrearMuroRecto/CrearMurosMasivo del catalogo. Requiere haber confirmado antes el nombre de capa (cad_list_layers) y el factor de escala (cad_calibrate_scale o confirmacion manual del usuario).")]
    public string ExtraerGeometria(
        [Description("Ruta local al fichero .dxf o .dwg")] string ruta,
        [Description("Nombre exacto de la capa a extraer")] string nombreCapa,
        [Description("Factor para convertir unidades de dibujo a metros (de cad_calibrate_scale, o confirmado por el usuario)")] double factorAMetros)
    {
        var documento = CadDocumentReader.Abrir(ruta);
        var segmentos = CadGeometryExtractor.ExtraerSegmentosDeCapa(documento, nombreCapa, factorAMetros);
        return JsonSerializer.Serialize(segmentos, JsonOptions);
    }
}
