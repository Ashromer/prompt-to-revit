namespace PocRevitAddin.Tests
{
    // Réplica deliberada, en C# puro y sin ninguna referencia a RevitAPI/RevitAPIUI, de los
    // literales que Shared/PocRevitAddin.cs usa para el nombre del panel, el texto/tooltip del
    // botón y el mensaje del diálogo. No es una capa de abstracción que el addin real consuma
    // (eso violaría "no refactorizar / no abstraer" de plan.md): es solo el fragmento de lógica
    // de formateo de texto, aislado para poder testearlo fuera de un proceso Revit (FR-010).
    public static class AddinMetadata
    {
        public const string PanelName = "PoC #2 NuGet vs Local";

        public static string BuildButtonText(string buildLabel)
        {
            return "PoC #2\n" + buildLabel;
        }

        public static string BuildTooltip(string buildLabel)
        {
            return "PoC #2: comando trivial para comparar el build contra el paquete NuGet de metadatos "
                + "frente al build contra las DLL locales de Revit 2026. Assembly: " + buildLabel;
        }

        public static string BuildDialogMessage(string buildLabel)
        {
            return "El botón del addin PoC #2 se ha ejecutado correctamente.\nBuild: " + buildLabel;
        }
    }
}
