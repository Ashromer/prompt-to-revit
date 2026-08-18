using System.Collections.Generic;
using System.Linq;

namespace RevitBridge.Core;

/// <summary>
/// Decisión de si borrar/modificar un conjunto de elementos requiere aprobación manual
/// (DOCUMENTACION.md §5.C.10/§5.D.15: "modificar preexistentes -- siempre manual, sin excepción").
/// Vive en Core, sin Revit, para que la decisión sea testeable y para que los comandos del
/// catálogo no reimplementen cada uno su propia comprobación -- eso es justo lo que permitió que
/// <c>ParamCommands.ModificarParametroTextoCategoria</c> se quedara sin ella (auditoría 2026-08-18):
/// <c>ModelingCommands.ModificarParametro</c> sí la tenía, el comando nuevo la olvidó porque no
/// había un único sitio que la centralizara.
/// </summary>
public static class PreexistingElementGuard
{
    /// <summary>
    /// True si CUALQUIERA de los ids afectados no está en el conjunto de ids creados en esta
    /// sesión -- es decir, si hay al menos un elemento preexistente entre los afectados.
    /// </summary>
    public static bool RequiereAprobacion(IEnumerable<long> idsAfectados, ISet<long> idsCreadosEnSesion) =>
        idsAfectados.Any(id => !idsCreadosEnSesion.Contains(id));
}
