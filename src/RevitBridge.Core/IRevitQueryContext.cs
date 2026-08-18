namespace RevitBridge.Core;

/// <summary>
/// Costura de abstracción entre <c>RevitBridge.Core</c> y el documento activo de Revit. Solo
/// lectura, sin transacción (DOCUMENTACION.md §4, operación <c>/query</c>). La implementación
/// real contra la API (F0.6, <c>RevitContext</c>) es de otro lote; aquí solo la interfaz.
/// </summary>
public interface IRevitQueryContext
{
    /// <summary>
    /// Resuelve una consulta de solo lectura contra el documento activo: niveles, tipos,
    /// símbolos, parámetros, selección actual, mediciones.
    /// </summary>
    object? Consultar(ConsultaRequest peticion);
}
