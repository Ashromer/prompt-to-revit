using RevitBridge.Core;
using Xunit;

namespace RevitBridge.Tests.Core;

/// <summary>
/// El umbral se controla solo por variable de entorno (nunca por parámetro de comando, para que
/// el modelo no pueda subirlo por su cuenta) -- estos tests aíslan la variable con
/// try/finally para no interferir con otros tests del proceso.
/// </summary>
public class UmbralAprobacionCreacionTests
{
    [Fact]
    public void Sin_Variable_De_Entorno_El_Umbral_Es_1_Y_Siempre_Requiere_Aprobacion()
    {
        var original = Environment.GetEnvironmentVariable(UmbralAprobacionCreacion.VariableDeEntorno);
        try
        {
            Environment.SetEnvironmentVariable(UmbralAprobacionCreacion.VariableDeEntorno, null);

            Assert.Equal(1, UmbralAprobacionCreacion.Obtener());
            Assert.True(UmbralAprobacionCreacion.RequiereAprobacion(1));
        }
        finally
        {
            Environment.SetEnvironmentVariable(UmbralAprobacionCreacion.VariableDeEntorno, original);
        }
    }

    [Fact]
    public void Variable_De_Entorno_Valida_Sube_El_Umbral()
    {
        var original = Environment.GetEnvironmentVariable(UmbralAprobacionCreacion.VariableDeEntorno);
        try
        {
            Environment.SetEnvironmentVariable(UmbralAprobacionCreacion.VariableDeEntorno, "5");

            Assert.Equal(5, UmbralAprobacionCreacion.Obtener());
            Assert.False(UmbralAprobacionCreacion.RequiereAprobacion(4));
            Assert.True(UmbralAprobacionCreacion.RequiereAprobacion(5));
        }
        finally
        {
            Environment.SetEnvironmentVariable(UmbralAprobacionCreacion.VariableDeEntorno, original);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("no-es-un-numero")]
    public void Variable_De_Entorno_Invalida_Cae_Al_Valor_Por_Defecto(string valorInvalido)
    {
        var original = Environment.GetEnvironmentVariable(UmbralAprobacionCreacion.VariableDeEntorno);
        try
        {
            Environment.SetEnvironmentVariable(UmbralAprobacionCreacion.VariableDeEntorno, valorInvalido);

            Assert.Equal(UmbralAprobacionCreacion.PorDefecto, UmbralAprobacionCreacion.Obtener());
        }
        finally
        {
            Environment.SetEnvironmentVariable(UmbralAprobacionCreacion.VariableDeEntorno, original);
        }
    }
}
