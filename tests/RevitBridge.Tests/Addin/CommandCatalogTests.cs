using RevitBridge.Addin.Bridge;
using RevitBridge.Utils;
using Xunit;

namespace RevitBridge.Tests.Addin;

/// <summary>
/// Descubrimiento por reflexión del catálogo de comandos (F0.9, ADR-005). El caso positivo
/// escanea el ensamblado REAL de RevitBridge.Utils; los demás casos usan clases locales de este
/// mismo ensamblado de test, marcadas con el mismo atributo público que usaría un comando
/// graduado de verdad — así no hace falta compilar un ensamblado de prueba aparte.
/// </summary>
public class CommandCatalogTests
{
    [Fact]
    public void Descubrir_Encuentra_Los_Comandos_De_Ejemplo_De_Utils()
    {
        var catalogo = CommandCatalog.Descubrir(typeof(SampleCommands).Assembly);

        Assert.Contains(catalogo, c => c.Nombre == "ping");
        Assert.Contains(catalogo, c => c.Nombre == "eco");
    }

    [Fact]
    public void Descubrir_Construye_El_Esquema_A_Partir_De_Los_Parametros()
    {
        var catalogo = CommandCatalog.Descubrir(typeof(SampleCommands).Assembly);

        var eco = catalogo.Single(c => c.Nombre == "eco");

        Assert.True(eco.Esquema.ContainsKey("mensaje"));
        Assert.Equal("String", eco.Esquema["mensaje"]);
    }

    [Fact]
    public void Descubrir_Ignora_Metodos_Publicos_Sin_El_Atributo()
    {
        // Tipos explícitos, no el ensamblado entero: aísla de cualquier otro comando de prueba
        // marcado en el resto del ensamblado de tests (p. ej. los duplicados de abajo).
        var catalogo = CommandCatalog.Descubrir(new[] { typeof(ComandoUnico) });

        Assert.Single(catalogo, c => c.Nombre == "comando-unico-de-test");
        Assert.DoesNotContain(catalogo, c => c.Nombre.Contains("SinMarcar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Descubrir_Lanza_Si_Hay_Nombres_De_Comando_Duplicados()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CommandCatalog.Descubrir(new[] { typeof(ComandoDuplicadoA), typeof(ComandoDuplicadoB) }));

        Assert.Contains("duplicado-de-test", ex.Message);
    }

    public static class ComandoUnico
    {
        [ComandoRevit("comando-unico-de-test")]
        public static object Marcado() => new { };

        public static object SinMarcarNoDebeAparecer() => new { };
    }

    public static class ComandoDuplicadoA
    {
        [ComandoRevit("duplicado-de-test")]
        public static object Uno() => new { };
    }

    public static class ComandoDuplicadoB
    {
        [ComandoRevit("duplicado-de-test")]
        public static object Dos() => new { };
    }
}
