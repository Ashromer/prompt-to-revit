using System.Collections.Generic;
using System.Linq;

namespace RevitBridge.Core;

/// <summary>
/// Texto de previsualización para operaciones masivas -- borrado/modificación de preexistentes
/// (§5.C.9/§5.D.15) o creación en lote (hallazgo de las sesiones de <c>architect</c> de ADR-012
/// CAD y PDF/VLM, 2026-08-18: <c>CrearMurosMasivo</c>/<c>CrearForjadosMasivo</c> creaban sin
/// ningún punto de revisión humana, rompiendo en espíritu §5.D.14). Vive en Core, sin Revit, para
/// que el formato sea consistente entre comandos y testeable sin depender de
/// <c>FilteredElementCollector</c>. No asume que los elementos sean preexistentes: ese matiz lo
/// aporta <paramref name="accion"/>, no la plantilla.
/// </summary>
public static class DeletionPreview
{
    /// <summary>
    /// <paramref name="categorias"/> es una secuencia de nombres de categoría, una entrada por
    /// elemento afectado (se agrupan aquí). <paramref name="accion"/> describe la operación en
    /// infinitivo/gerundio ("borrar", "modificar el parámetro 'X' de", "crear en el nivel 'X'").
    /// </summary>
    public static string ConstruirResumen(string accion, IEnumerable<string> categorias)
    {
        var lista = categorias.ToList();
        var conteos = lista
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Count()} de {g.Key}");

        return $"ATENCIÓN: vas a {accion} {lista.Count} elemento(s):\n- " + string.Join("\n- ", conteos);
    }
}
