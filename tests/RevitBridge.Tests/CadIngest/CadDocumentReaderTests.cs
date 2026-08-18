using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using RevitBridge.CadIngest;
using Xunit;

namespace RevitBridge.Tests.CadIngest;

/// <summary>
/// Round-trip contra un DXF sintético escrito por el propio ACadSharp (no depende de ningún
/// fichero real del usuario, tal como pedía el paso 3 del plan del borrador de ADR-012).
/// </summary>
public class CadDocumentReaderTests : IDisposable
{
    private readonly string _rutaDxf;

    public CadDocumentReaderTests()
    {
        _rutaDxf = Path.Combine(Path.GetTempPath(), $"RevitBridge.Tests.CadIngest.{Guid.NewGuid():N}.dxf");

        var doc = new CadDocument();
        var capaMuros = new Layer("MUROS");
        var capaPuertas = new Layer("PUERTAS");
        doc.Layers.Add(capaMuros);
        doc.Layers.Add(capaPuertas);

        doc.Entities.Add(new Line(new XYZ(0, 0, 0), new XYZ(5000, 0, 0)) { Layer = capaMuros });
        doc.Entities.Add(new Line(new XYZ(5000, 0, 0), new XYZ(5000, 3000, 0)) { Layer = capaMuros });

        var poly = new LwPolyline { Layer = capaMuros, IsClosed = false };
        poly.Vertices.Add(new LwPolyline.Vertex(new XY(0, 0)));
        poly.Vertices.Add(new LwPolyline.Vertex(new XY(0, 3000)));
        doc.Entities.Add(poly);

        doc.Header.InsUnits = ACadSharp.Types.Units.UnitsType.Millimeters;

        DxfWriter.Write(_rutaDxf, doc, false);
    }

    public void Dispose()
    {
        if (File.Exists(_rutaDxf)) File.Delete(_rutaDxf);
    }

    [Fact]
    public void Abrir_Lee_El_Dxf_Sintetico_Sin_Excepcion()
    {
        var doc = CadDocumentReader.Abrir(_rutaDxf);

        Assert.NotNull(doc);
    }

    [Fact]
    public void Abrir_Rechaza_Extension_No_Soportada()
    {
        Assert.Throws<NotSupportedException>(() => CadDocumentReader.Abrir("plano.pdf"));
    }

    [Fact]
    public void ListarCapas_Cuenta_Lineas_Y_Polilineas_Por_Capa()
    {
        var doc = CadDocumentReader.Abrir(_rutaDxf);

        var capas = CadDocumentReader.ListarCapas(doc);

        var muros = capas.Single(c => c.Nombre == "MUROS");
        Assert.Equal(2, muros.Lineas);
        Assert.Equal(1, muros.Polilineas);
    }

    [Fact]
    public void Pipeline_Completo_Calibra_Y_Extrae_En_Metros()
    {
        var doc = CadDocumentReader.Abrir(_rutaDxf);

        var calibracion = CadScaleCalibrator.CalibrarPorCabecera(doc);
        Assert.Equal(ConfianzaEscala.DesdeCabecera, calibracion.Confianza);

        var segmentos = CadGeometryExtractor.ExtraerSegmentosDeCapa(doc, "MUROS", calibracion.FactorAMetros);

        Assert.Equal(3, segmentos.Count); // 2 lineas + 1 tramo de la polilinea (2 vertices, sin cerrar)
        Assert.Contains(segmentos, s => Math.Abs(s.P2x - 5.0) < 1e-6); // 5000mm -> 5m
    }
}
