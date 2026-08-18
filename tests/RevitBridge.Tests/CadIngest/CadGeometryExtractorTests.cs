using CSMath;
using RevitBridge.CadIngest;
using Xunit;

namespace RevitBridge.Tests.CadIngest;

/// <summary>
/// Teselado de arcos (bulge de LwPolyline) sin Revit -- el punto de mayor riesgo del pipeline CAD
/// según el propio borrador de ADR-012 ("firma exacta a verificar", aquí es lógica pura, no firma
/// de API, pero igual de crítico: un bulge mal interpretado produce geometría incorrecta en
/// silencio, no una excepción).
/// </summary>
public class CadGeometryExtractorTests
{
    [Fact]
    public void Bulge_Cero_Devuelve_El_Tramo_Recto_Sin_Subdividir()
    {
        var p1 = new XY(0, 0);
        var p2 = new XY(10, 0);

        var puntos = CadGeometryExtractor.TeselarTramo(p1, p2, bulge: 0, toleranciaGrados: 12).ToList();

        Assert.Equal(2, puntos.Count);
        Assert.Equal(p1.X, puntos[0].X, precision: 9);
        Assert.Equal(p1.Y, puntos[0].Y, precision: 9);
        Assert.Equal(p2.X, puntos[1].X, precision: 9);
        Assert.Equal(p2.Y, puntos[1].Y, precision: 9);
    }

    [Fact]
    public void Arco_Con_Barrido_Bajo_Tolerancia_No_Se_Subdivide()
    {
        // bulge = tan(theta/4); para un barrido de 8 grados, theta/4 = 2 grados
        var bulge = Math.Tan(2.0 * Math.PI / 180.0);
        var p1 = new XY(0, 0);
        var p2 = new XY(10, 0);

        var puntos = CadGeometryExtractor.TeselarTramo(p1, p2, bulge, toleranciaGrados: 12).ToList();

        Assert.Equal(2, puntos.Count);
    }

    [Fact]
    public void Semicirculo_Bulge_Uno_Pasa_Por_El_Apice_Esperado()
    {
        // bulge=1 => theta = 4*atan(1) = 180 grados (semicirculo). Con p1=(0,0), p2=(2,0), el
        // apice del arco (punto medio del barrido) cae en (1,1): desplazamiento = bulge*d/2 = 1,
        // perpendicular a (2,0) es (0,1).
        var p1 = new XY(0, 0);
        var p2 = new XY(2, 0);

        var puntos = CadGeometryExtractor.TeselarTramo(p1, p2, bulge: 1.0, toleranciaGrados: 1).ToList();

        Assert.Equal(p1.X, puntos.First().X, precision: 6);
        Assert.Equal(p1.Y, puntos.First().Y, precision: 6);
        Assert.Equal(p2.X, puntos.Last().X, precision: 6);
        Assert.Equal(p2.Y, puntos.Last().Y, precision: 6);
        Assert.Contains(puntos, p => Math.Abs(p.X - 1.0) < 1e-6 && Math.Abs(p.Y - 1.0) < 1e-6);
    }

    [Fact]
    public void Subdivision_Cubre_Todo_El_Barrido_Sin_Saltos_Ni_Duplicados()
    {
        var bulge = Math.Tan(90.0 / 4.0 * Math.PI / 180.0); // cuarto de circulo, 90 grados
        var p1 = new XY(0, 0);
        var p2 = new XY(4, 0);

        var puntos = CadGeometryExtractor.TeselarTramo(p1, p2, bulge, toleranciaGrados: 12).ToList();

        // Sin puntos consecutivos repetidos (el punto de bisección no debe duplicarse entre mitades)
        for (int i = 0; i < puntos.Count - 1; i++)
        {
            var mismoPunto = Math.Abs(puntos[i].X - puntos[i + 1].X) < 1e-9 && Math.Abs(puntos[i].Y - puntos[i + 1].Y) < 1e-9;
            Assert.False(mismoPunto, $"Punto duplicado en índice {i}");
        }
        Assert.True(puntos.Count >= 3, "Un arco de 90 grados con tolerancia de 12 debe subdividirse en más de un tramo");
    }

    [Fact]
    public void Extraer_Segmentos_De_Capa_Ignora_Otras_Capas()
    {
        var doc = new ACadSharp.CadDocument();
        var capaMuros = new ACadSharp.Tables.Layer("MUROS");
        var capaCotas = new ACadSharp.Tables.Layer("COTAS");
        doc.Layers.Add(capaMuros);
        doc.Layers.Add(capaCotas);

        doc.Entities.Add(new ACadSharp.Entities.Line(new XYZ(0, 0, 0), new XYZ(5, 0, 0)) { Layer = capaMuros });
        doc.Entities.Add(new ACadSharp.Entities.Line(new XYZ(0, 0, 0), new XYZ(1, 1, 0)) { Layer = capaCotas });

        var segmentos = CadGeometryExtractor.ExtraerSegmentosDeCapa(doc, "MUROS", factorAMetros: 1.0);

        var segmento = Assert.Single(segmentos);
        Assert.Equal(5.0, segmento.P2x, precision: 6);
    }
}
