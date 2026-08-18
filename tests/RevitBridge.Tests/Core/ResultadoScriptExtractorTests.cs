using RevitBridge.Core;
using Xunit;

namespace RevitBridge.Tests.Core;

/// <summary>
/// Auditoría 2026-08-18: antes de esta corrección, RevitContext descartaba el valor de retorno
/// real del script Roslyn y la respuesta de /exec siempre mandaba ids_creados vacío. Este
/// extractor vive en Core (sin Revit) precisamente para poder testear esa lógica sin Revit.
/// </summary>
public class ResultadoScriptExtractorTests
{
    [Fact]
    public void ExtraeIdsDelContratoDocumentado()
    {
        var resultado = new { ids = new[] { 12345L, 67890L } };

        var ids = ResultadoScriptExtractor.ExtraerIdsCreados(resultado);

        Assert.Equal(new long[] { 12345, 67890 }, ids);
    }

    [Fact]
    public void EsInsensibleAMayusculasEnElNombreDeLaPropiedad()
    {
        var resultado = new { Ids = new[] { 1L } };

        var ids = ResultadoScriptExtractor.ExtraerIdsCreados(resultado);

        Assert.Equal(new long[] { 1 }, ids);
    }

    [Fact]
    public void DevuelveVacioSiElResultadoEsNulo()
    {
        var ids = ResultadoScriptExtractor.ExtraerIdsCreados(null);

        Assert.Empty(ids);
    }

    [Fact]
    public void DevuelveVacioSiNoHayPropiedadIds()
    {
        var resultado = new { mensaje = "ok, sin ids" };

        var ids = ResultadoScriptExtractor.ExtraerIdsCreados(resultado);

        Assert.Empty(ids);
    }

    [Fact]
    public void DevuelveVacioSiElResultadoEsUnEscalar()
    {
        var ids = ResultadoScriptExtractor.ExtraerIdsCreados("solo texto");

        Assert.Empty(ids);
    }

    [Fact]
    public void IgnoraElementosNoNumericosDelArray()
    {
        var resultado = new { ids = new object[] { 1L, "no-es-un-id", 2L } };

        var ids = ResultadoScriptExtractor.ExtraerIdsCreados(resultado);

        Assert.Equal(new long[] { 1, 2 }, ids);
    }
}
