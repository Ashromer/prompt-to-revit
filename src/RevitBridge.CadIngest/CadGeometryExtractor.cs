using ACadSharp;
using ACadSharp.Entities;
using CSMath;

namespace RevitBridge.CadIngest;

/// <summary>Segmento recto en metros, mismo esquema que ya aceptan <c>CrearMuroRecto</c>/
/// <c>CrearMurosMasivo</c> del catálogo (p1x/p1y/p2x/p2y).</summary>
public sealed record SegmentoRecto(double P1x, double P1y, double P2x, double P2y);

/// <summary>
/// Extrae geometría de una capa de un <see cref="CadDocument"/> ya calibrado a metros, teselando
/// arcos (bulge de <c>LwPolyline</c>) en segmentos rectos -- misma cota de 12° de barrido que
/// <c>revit_api_knowledge.md</c> usa para offset de perfiles de chapa, mismo dominio de problema
/// (bulge de polilínea DXF), aquí aplicado a plantas de edificio en vez de perfiles de lámina.
/// Función pura, sin Revit, testeable con xUnit.
/// </summary>
public static class CadGeometryExtractor
{
    public const double ToleranciaGradosPorDefecto = 12.0;

    public static IReadOnlyList<SegmentoRecto> ExtraerSegmentosDeCapa(
        CadDocument documento, string nombreCapa, double factorAMetros, double toleranciaGrados = ToleranciaGradosPorDefecto)
    {
        var segmentos = new List<SegmentoRecto>();

        var entidadesDeLaCapa = documento.Entities
            .Where(e => string.Equals(e.Layer?.Name, nombreCapa, StringComparison.OrdinalIgnoreCase));

        foreach (var entidad in entidadesDeLaCapa)
        {
            switch (entidad)
            {
                case Line linea:
                    segmentos.Add(new SegmentoRecto(
                        linea.StartPoint.X * factorAMetros, linea.StartPoint.Y * factorAMetros,
                        linea.EndPoint.X * factorAMetros, linea.EndPoint.Y * factorAMetros));
                    break;

                case LwPolyline polilinea:
                    segmentos.AddRange(TeselarPolilinea(polilinea, factorAMetros, toleranciaGrados));
                    break;
            }
        }

        return segmentos;
    }

    private static IEnumerable<SegmentoRecto> TeselarPolilinea(LwPolyline polilinea, double factorAMetros, double toleranciaGrados)
    {
        var vertices = polilinea.Vertices.ToList();
        if (vertices.Count < 2) yield break;

        int tramos = polilinea.IsClosed ? vertices.Count : vertices.Count - 1;

        for (int i = 0; i < tramos; i++)
        {
            var actual = vertices[i];
            var siguiente = vertices[(i + 1) % vertices.Count];

            var puntos = TeselarTramo(actual.Location, siguiente.Location, actual.Bulge, toleranciaGrados).ToList();
            for (int j = 0; j < puntos.Count - 1; j++)
            {
                yield return new SegmentoRecto(
                    puntos[j].X * factorAMetros, puntos[j].Y * factorAMetros,
                    puntos[j + 1].X * factorAMetros, puntos[j + 1].Y * factorAMetros);
            }
        }
    }

    /// <summary>
    /// Recorre un tramo p1→p2 con el <paramref name="bulge"/> de la convención DXF (bulge =
    /// tan(barrido/4); positivo = sentido antihorario). Si el barrido ya cabe bajo la tolerancia,
    /// devuelve el tramo recto tal cual. Si no, bisecciona recursivamente: cada mitad barre la
    /// mitad del ángulo, con <c>bulgeMitad = bulge / (1 + sqrt(1 + bulge²))</c> (identidad de
    /// tangente de ángulo mitad aplicada a bulge=tan(θ/4)) y el punto de bisección se calcula
    /// desplazando el punto medio de la cuerda por la perpendicular en <c>bulge·distancia/2</c>.
    /// </summary>
    public static IEnumerable<XY> TeselarTramo(XY p1, XY p2, double bulge, double toleranciaGrados)
    {
        if (Math.Abs(bulge) < 1e-9)
        {
            yield return p1;
            yield return p2;
            yield break;
        }

        var barridoGrados = Math.Abs(4 * Math.Atan(bulge)) * 180.0 / Math.PI;
        if (barridoGrados <= toleranciaGrados)
        {
            yield return p1;
            yield return p2;
            yield break;
        }

        var dx = p2.X - p1.X;
        var dy = p2.Y - p1.Y;
        var distancia = Math.Sqrt(dx * dx + dy * dy);
        if (distancia < 1e-9)
        {
            yield return p1;
            yield break;
        }

        var bulgeMitad = bulge / (1.0 + Math.Sqrt(1.0 + bulge * bulge));
        var desplazamiento = bulge * distancia / 2.0;
        var mx = (p1.X + p2.X) / 2.0;
        var my = (p1.Y + p2.Y) / 2.0;
        var perpX = -dy / distancia;
        var perpY = dx / distancia;
        var puntoMedio = new XY(mx + perpX * desplazamiento, my + perpY * desplazamiento);

        var primeraMitad = true;
        foreach (var punto in TeselarTramo(p1, puntoMedio, bulgeMitad, toleranciaGrados))
        {
            yield return punto;
        }

        foreach (var punto in TeselarTramo(puntoMedio, p2, bulgeMitad, toleranciaGrados))
        {
            if (primeraMitad) { primeraMitad = false; continue; } // no duplicar puntoMedio
            yield return punto;
        }
    }
}
