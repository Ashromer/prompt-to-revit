using System.Text.Json;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using RevitBridge.Mcp.Tools;
using Xunit;

namespace RevitBridge.Tests.CadIngest;

/// <summary>
/// Regresión (revisión judge 2026-08-18): <c>cad_extract_geometry</c> serializaba
/// <c>SegmentoRecto</c> en PascalCase (P1x/P1y/P2x/P2y, los nombres literales de las propiedades
/// C#) mientras que <c>CrearMurosMasivo</c> lee sus argumentos como
/// <c>Dictionary&lt;string,double&gt;</c> con claves en minúscula -- la comparación de claves de
/// un diccionario es sensible a mayúsculas y <c>PropertyNameCaseInsensitive</c> no la afecta (solo
/// el binding a propiedades de un POCO). El resultado era 0 muros creados sin ningún error: el
/// pipeline CAD -> catálogo documentado en <c>CadIngestTools</c> nunca funcionaba de verdad.
/// </summary>
public class CadIngestToolsTests : IDisposable
{
    private readonly string _rutaDxf;

    public CadIngestToolsTests()
    {
        _rutaDxf = Path.Combine(Path.GetTempPath(), $"RevitBridge.Tests.CadIngestTools.{Guid.NewGuid():N}.dxf");

        var doc = new CadDocument();
        var capaMuros = new Layer("MUROS");
        doc.Layers.Add(capaMuros);
        doc.Entities.Add(new Line(new XYZ(0, 0, 0), new XYZ(5000, 0, 0)) { Layer = capaMuros });
        doc.Header.InsUnits = ACadSharp.Types.Units.UnitsType.Millimeters;

        DxfWriter.Write(_rutaDxf, doc, false);
    }

    public void Dispose()
    {
        if (File.Exists(_rutaDxf)) File.Delete(_rutaDxf);
    }

    [Fact]
    public void ExtraerGeometria_Serializa_Claves_En_Minuscula()
    {
        var herramienta = new CadIngestTools();

        var json = herramienta.ExtraerGeometria(_rutaDxf, "MUROS", factorAMetros: 0.001);

        Assert.Contains("\"p1x\"", json);
        Assert.DoesNotContain("\"P1x\"", json);
    }

    [Fact]
    public void ExtraerGeometria_Es_Consumible_Por_El_Mismo_Deserializador_Que_CrearMurosMasivo()
    {
        var herramienta = new CadIngestTools();
        var json = herramienta.ExtraerGeometria(_rutaDxf, "MUROS", factorAMetros: 0.001);

        // ExtraerGeometria ya devuelve un array de segmentos -> encaja directo en el tipo que
        // CrearMurosMasivo deserializa (List<Dictionary<string,double>>).
        var comoLista = JsonSerializer.Deserialize<List<Dictionary<string, double>>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(comoLista);
        var coords = Assert.Single(comoLista!);
        Assert.True(coords.TryGetValue("p1x", out var p1x));
        Assert.Equal(0.0, p1x, precision: 6);
        Assert.True(coords.TryGetValue("p2x", out var p2x));
        Assert.Equal(5.0, p2x, precision: 6); // 5000mm * 0.001 -> 5m
    }
}
