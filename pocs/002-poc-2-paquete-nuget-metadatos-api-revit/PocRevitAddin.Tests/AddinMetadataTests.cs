using Xunit;

namespace PocRevitAddin.Tests
{
    public class AddinMetadataTests
    {
        [Fact]
        public void PanelName_MatchesConstantUsedByTheAddin()
        {
            // Shared/PocRevitAddin.cs crea el RibbonPanel con este literal exacto en OnStartup.
            // Si alguien lo cambia sin querer (typo, reformateo), este test debe romperse.
            Assert.Equal("PoC #2 NuGet vs Local", AddinMetadata.PanelName);
        }

        [Fact]
        public void BuildButtonText_PutsBuildLabelOnSecondLine()
        {
            string result = AddinMetadata.BuildButtonText("PocRevitAddin.Nuget");

            Assert.Equal("PoC #2\nPocRevitAddin.Nuget", result);
            Assert.StartsWith("PoC #2\n", result);
        }

        [Fact]
        public void TooltipAndDialogMessage_BothEndWithTheAssemblyLabelThatDistinguishesTheBuild()
        {
            // El propósito real de estos dos textos en el addin es distinguir a simple vista, en
            // el ribbon y en el diálogo del comando, si el DLL cargado viene del build NuGet o del
            // build Local (ver plan.md, tarea de PocRevitAddin.Local.csproj). Ambos deben seguir
            // terminando con el mismo label de ensamblado que reciben.
            string tooltip = AddinMetadata.BuildTooltip("PocRevitAddin.Local");
            string dialogMessage = AddinMetadata.BuildDialogMessage("PocRevitAddin.Local");

            Assert.EndsWith("Assembly: PocRevitAddin.Local", tooltip);
            Assert.Contains("PoC #2", tooltip);

            Assert.EndsWith("Build: PocRevitAddin.Local", dialogMessage);
            Assert.Contains("se ha ejecutado correctamente", dialogMessage);
        }
    }
}
