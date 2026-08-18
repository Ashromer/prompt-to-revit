namespace RevitBridge.Core;

/// <summary>
/// Umbral de aprobación para creación masiva (hallazgo de las sesiones de <c>architect</c> de
/// ADR-012 CAD y ADR-013 PDF/VLM, 2026-08-18). Controlado por el usuario vía variable de entorno,
/// NUNCA por un parámetro del propio comando: si el modelo pudiera decidir el umbral en cada
/// llamada, podría subirlo para evitarse la aprobación, que es justo lo que la salvaguarda existe
/// para impedir (mismo principio que <c>DOCUMENTACION.md</c> §5.D.15). Por defecto 1 -- pide
/// aprobación siempre, sin excepción -- hasta que haya uso real que justifique subirlo. El valor
/// concreto queda sin calibrar a propósito (ver Discovery en <c>specs/tech-spec.md</c>).
/// </summary>
public static class UmbralAprobacionCreacion
{
    public const string VariableDeEntorno = "REVITBRIDGE_UMBRAL_APROBACION_CREACION";
    public const int PorDefecto = 1;

    public static int Obtener()
    {
        var valor = Environment.GetEnvironmentVariable(VariableDeEntorno);
        return int.TryParse(valor, out var umbral) && umbral > 0 ? umbral : PorDefecto;
    }

    public static bool RequiereAprobacion(int cantidadElementos) => cantidadElementos >= Obtener();
}
