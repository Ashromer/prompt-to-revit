using RevitBridge.Core;
using Xunit;

namespace RevitBridge.Tests.Core;

/// <summary>F2.4 (§5.C.9): texto de previsualización compartido entre comandos de borrado/modificación masiva.</summary>
public class DeletionPreviewTests
{
    [Fact]
    public void Agrupa_Por_Categoria_Y_Cuenta_Cada_Grupo()
    {
        var resumen = DeletionPreview.ConstruirResumen("borrar", new[] { "Muros", "Muros", "Puertas" });

        Assert.Contains("3 elemento(s)", resumen);
        Assert.Contains("2 de Muros", resumen);
        Assert.Contains("1 de Puertas", resumen);
    }

    [Fact]
    public void Incluye_La_Accion_En_El_Texto()
    {
        var resumen = DeletionPreview.ConstruirResumen("modificar el parámetro 'Comments' de", new[] { "Ventanas" });

        Assert.Contains("modificar el parámetro 'Comments' de 1 elemento(s)", resumen);
    }

    [Fact]
    public void Sin_Categorias_Produce_Cero_Elementos()
    {
        var resumen = DeletionPreview.ConstruirResumen("borrar", System.Array.Empty<string>());

        Assert.Contains("0 elemento(s)", resumen);
    }
}
