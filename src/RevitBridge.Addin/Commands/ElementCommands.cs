using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using RevitBridge.Core;
using RevitBridge.Utils;

namespace RevitBridge.Addin.Commands;

/// <summary>
/// Operaciones genéricas sobre un elemento ya existente: renombrar, leer sus parámetros, duplicar
/// su tipo (F3.7, extensión "casa completa"). No son "masivo" -- un elemento a la vez, mismo
/// registro de riesgo que ModificarParametro. Firmas verificadas con MetadataLoadContext:
/// ElementType.Duplicate(string) y Element.GetOrderedParameters() son reales en 2026.4.10.
/// </summary>
public static class ElementCommands
{
    [ComandoRevit("RenombrarElemento")]
    public static object RenombrarElemento(Document doc, int elementoId, string nuevoNombre)
    {
        var id = new ElementId(elementoId);
        var elem = doc.GetElement(id);
        if (elem == null) throw new ArgumentException("Elemento no encontrado.");

        // §5.C.10/§5.D.15: renombrar un preexistente (no creado en esta sesión) pide aprobación,
        // igual que ModificarParametro.
        if (PreexistingElementGuard.RequiereAprobacion(new[] { (long)id.Value }, App.ElementosCreadosEnSesion))
        {
            var resumen = DeletionPreview.ConstruirResumen(
                $"renombrar a '{nuevoNombre}'", new[] { elem.Category?.Name ?? "Desconocido" });
            var approval = new RevitBridge.Addin.UI.ApprovalService();
            if (!approval.SolicitarAprobacion(resumen))
                throw new InvalidOperationException("Renombrado de elemento cancelado por el usuario.");
        }

        var nombreAnterior = elem.Name;
        using (var tx = new Transaction(doc, "Renombrar Elemento MCP"))
        {
            tx.Start();
            elem.Name = nuevoNombre;
            tx.Commit();
        }

        return new { Id = id.Value, NombreAnterior = nombreAnterior, NombreNuevo = elem.Name };
    }

    [ComandoRevit("DuplicarTipoDeElemento")]
    public static object DuplicarTipoDeElemento(Document doc, int tipoId, string nuevoNombre)
    {
        // Creación pura (el tipo original queda intacto) -- sin aprobación, mismo régimen que
        // CrearMuroRecto/CrearNivel/CargarFamilia (creación única, no masiva).
        var tipo = doc.GetElement(new ElementId(tipoId)) as ElementType;
        if (tipo == null) throw new ArgumentException("El id no corresponde a un ElementType (¿lo obtuviste de ObtenerTiposCargadosPorCategoria?).");

        ElementType? nuevoTipo = null;
        using (var tx = new Transaction(doc, $"Duplicar Tipo '{nuevoNombre}' MCP"))
        {
            tx.Start();
            nuevoTipo = tipo.Duplicate(nuevoNombre);
            tx.Commit();
        }

        return new { Id = nuevoTipo!.Id.Value, Nombre = nuevoTipo.Name, TipoOrigenId = tipoId };
    }

    [ComandoRevit("ObtenerParametrosDeElemento")]
    public static object ObtenerParametrosDeElemento(Document doc, int elementoId)
    {
        // Puramente de lectura -- sin Transaction. Complementa ModificarParametro/
        // ModificarParametrosMasivo: antes de adivinar un nombre de parámetro (prohibido por
        // §5.A.1), se listan los reales de ESTE elemento en ESTE documento.
        var elem = doc.GetElement(new ElementId(elementoId));
        if (elem == null) throw new ArgumentException("Elemento no encontrado.");

        var parametros = elem.GetOrderedParameters()
            .Select(p => new
            {
                Nombre = p.Definition?.Name ?? "?",
                Valor = p.HasValue ? (p.AsValueString() ?? SegunTipoDeAlmacenamiento(p)) : null,
                TipoAlmacenamiento = p.StorageType.ToString(),
                SoloLectura = p.IsReadOnly,
            })
            .ToList();

        return new { Id = elementoId, Categoria = elem.Category?.Name, Parametros = parametros };
    }

    [ComandoRevit("CrearGrupoDeElementos")]
    public static object CrearGrupoDeElementos(Document doc, List<int> elementoIds, string nombreGrupo = "")
    {
        // Agrupar SÍ modifica los elementos originales (les asigna GroupId) -- misma salvaguarda de
        // preexistentes que mover/rotar (§5.C.10/§5.D.15), no la de creación pura. Firma verificada
        // con MetadataLoadContext: Document.Create.NewGroup(ICollection<ElementId>) es real en
        // 2026.4.10.
        if (elementoIds == null || elementoIds.Count < 2)
            throw new ArgumentException("Hacen falta al menos 2 elementos para formar un grupo.");

        var ids = elementoIds.Select(id => new ElementId(id)).ToList();

        if (PreexistingElementGuard.RequiereAprobacion(ids.Select(id => (long)id.Value), App.ElementosCreadosEnSesion))
        {
            var categorias = ids.Select(id => doc.GetElement(id)?.Category?.Name ?? "Desconocido");
            var resumen = DeletionPreview.ConstruirResumen("agrupar", categorias);
            var approval = new RevitBridge.Addin.UI.ApprovalService();
            if (!approval.SolicitarAprobacion(resumen))
                throw new InvalidOperationException("Agrupación de elementos cancelada por el usuario.");
        }

        Group? grupo = null;
        using (var tx = new Transaction(doc, $"Agrupar {ids.Count} Elementos MCP"))
        {
            tx.Start();
            grupo = doc.Create.NewGroup(ids);
            if (!string.IsNullOrWhiteSpace(nombreGrupo)) grupo.GroupType.Name = nombreGrupo;
            tx.Commit();
        }

        return new { Id = grupo.Id.Value, TipoGrupoId = grupo.GroupType.Id.Value, Nombre = grupo.GroupType.Name };
    }

    [ComandoRevit("ColocarGrupoEnPunto")]
    public static object ColocarGrupoEnPunto(Document doc, int tipoGrupoId, double xMetros, double yMetros, double zMetros = 0)
    {
        // Creación pura de una nueva instancia de un GroupType ya existente -- sin aprobación,
        // mismo régimen que CrearMuroRecto/DuplicarTipoDeElemento (creación única, no masiva).
        var tipoGrupo = doc.GetElement(new ElementId(tipoGrupoId)) as GroupType;
        if (tipoGrupo == null) throw new ArgumentException("El id no corresponde a un GroupType (¿lo obtuviste de CrearGrupoDeElementos?).");

        double m2ft = 1.0 / 0.3048;
        var punto = new XYZ(xMetros * m2ft, yMetros * m2ft, zMetros * m2ft);

        Group? grupo = null;
        using (var tx = new Transaction(doc, "Colocar Grupo MCP"))
        {
            tx.Start();
            grupo = doc.Create.PlaceGroup(punto, tipoGrupo);
            tx.Commit();
        }

        return new { Id = grupo!.Id.Value, Nombre = grupo.GroupType.Name };
    }

    private static string? SegunTipoDeAlmacenamiento(Parameter p) => p.StorageType switch
    {
        StorageType.String => p.AsString(),
        StorageType.Integer => p.AsInteger().ToString(),
        StorageType.Double => p.AsDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
        StorageType.ElementId => p.AsElementId()?.Value.ToString(),
        _ => null,
    };
}
