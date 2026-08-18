using ACadSharp;
using ACadSharp.Types.Units;
using RevitBridge.CadIngest;
using Xunit;

namespace RevitBridge.Tests.CadIngest;

/// <summary>ADR-012 D3: nunca un valor de escala asumido en silencio.</summary>
public class CadScaleCalibratorTests
{
    [Fact]
    public void Milimetros_Da_Confianza_DesdeCabecera_Y_Factor_0_001()
    {
        var doc = new CadDocument();
        doc.Header.InsUnits = UnitsType.Millimeters;

        var resultado = CadScaleCalibrator.CalibrarPorCabecera(doc);

        Assert.Equal(ConfianzaEscala.DesdeCabecera, resultado.Confianza);
        Assert.Equal(0.001, resultado.FactorAMetros, precision: 9);
    }

    [Fact]
    public void Metros_Da_Factor_1()
    {
        var doc = new CadDocument();
        doc.Header.InsUnits = UnitsType.Meters;

        var resultado = CadScaleCalibrator.CalibrarPorCabecera(doc);

        Assert.Equal(1.0, resultado.FactorAMetros, precision: 9);
    }

    [Fact]
    public void Unitless_Se_Bloquea_Y_Pide_Confirmacion_Manual()
    {
        var doc = new CadDocument();
        doc.Header.InsUnits = UnitsType.Unitless;

        var resultado = CadScaleCalibrator.CalibrarPorCabecera(doc);

        Assert.Equal(ConfianzaEscala.SinDatos, resultado.Confianza);
        Assert.Equal(0, resultado.FactorAMetros);
        Assert.Contains("confirme", resultado.Detalle, StringComparison.OrdinalIgnoreCase);
    }
}
