using ACadSharp;
using ACadSharp.Types.Units;

namespace RevitBridge.CadIngest;

public enum ConfianzaEscala { DesdeCabecera, Ambigua, SinDatos }

/// <summary>Resultado de calibrar la escala de un CAD (ADR-012 D3): nunca un valor asumido en
/// silencio -- si <see cref="Confianza"/> no es <see cref="ConfianzaEscala.DesdeCabecera"/>, el
/// llamador debe pedir confirmación explícita antes de generar ninguna coordenada.</summary>
public sealed record ResultadoCalibracion(double FactorAMetros, ConfianzaEscala Confianza, string Detalle);

/// <summary>
/// Lee la unidad de la cabecera del documento CAD como fuente primaria de escala. El contraste
/// contra una cota (<c>Dimension</c>) del propio fichero, descrito en el borrador de ADR-012, queda
/// para cuando haya un fichero real con cotas contra el que verificarlo (paso 1 del plan del ADR,
/// spike no bloqueante) -- aquí solo la vía de cabecera, ya verificada por reflexión contra el
/// paquete real.
/// </summary>
public static class CadScaleCalibrator
{
    private static readonly Dictionary<UnitsType, double> FactoresConocidos = new()
    {
        [UnitsType.Millimeters] = 0.001,
        [UnitsType.Centimeters] = 0.01,
        [UnitsType.Decimeters] = 0.1,
        [UnitsType.Meters] = 1.0,
        [UnitsType.Inches] = 0.0254,
        [UnitsType.Feet] = 0.3048,
    };

    public static ResultadoCalibracion CalibrarPorCabecera(CadDocument documento)
    {
        var unidades = documento.Header.InsUnits;

        if (unidades == UnitsType.Unitless)
        {
            return new ResultadoCalibracion(0, ConfianzaEscala.SinDatos,
                "El fichero no declara unidades (Unitless). Pedir al usuario que confirme la escala explícitamente antes de generar coordenadas.");
        }

        if (FactoresConocidos.TryGetValue(unidades, out var factor))
        {
            return new ResultadoCalibracion(factor, ConfianzaEscala.DesdeCabecera,
                $"Unidades de cabecera: {unidades} (1 unidad de dibujo = {factor} m).");
        }

        return new ResultadoCalibracion(0, ConfianzaEscala.Ambigua,
            $"Unidades de cabecera '{unidades}' sin factor conocido en este proyecto. Pedir confirmación manual.");
    }
}
