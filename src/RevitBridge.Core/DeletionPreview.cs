using System.Collections.Generic;
using System.Linq;

namespace RevitBridge.Core;

/// <summary>
/// Texto de previsualización para operaciones de borrado/modificación masiva sobre elementos
/// preexistentes (DOCUMENTACION.md §5.C.9: "cuántos elementos, de qué categorías" antes de pedir
/// confirmación). Vive en Core, sin Revit, para que el formato sea consistente entre comandos y
/// testeable sin depender de <c>FilteredElementCollector</c>.
/// </summary>
public static class DeletionPreview
{
    /// <summary>
    /// <paramref name="categorias"/> es una secuencia de nombres de categoría, una entrada por
    /// elemento afectado (se agrupan aquí). <paramref name="accion"/> describe la operación en
    /// infinitivo/gerundio ("borrar", "modificar el parámetro 'X' de").
    /// </summary>
    public static string ConstruirResumen(string accion, IEnumerable<string> categorias)
    {
        var lista = categorias.ToList();
        var conteos = lista
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Count()} de {g.Key}");

        return $"ATENCIÓN: vas a {accion} {lista.Count} elemento(s) preexistentes:\n- " + string.Join("\n- ", conteos);
    }
}
