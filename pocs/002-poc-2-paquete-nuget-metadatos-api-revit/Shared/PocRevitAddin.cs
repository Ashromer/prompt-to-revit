using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace PocRevitAddin
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            string panelName = "PoC #2 NuGet vs Local";
            RibbonPanel panel = application.CreateRibbonPanel(panelName);

            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            string assemblyPath = executingAssembly.Location;
            string buildLabel = executingAssembly.GetName().Name;

            PushButtonData buttonData = new PushButtonData(
                "PocRevitAddinButton",
                "PoC #2\n" + buildLabel,
                assemblyPath,
                "PocRevitAddin.PocCommand");
            buttonData.ToolTip =
                "PoC #2: comando trivial para comparar el build contra el paquete NuGet de metadatos "
                + "frente al build contra las DLL locales de Revit 2026. Assembly: " + buildLabel;

            panel.AddItem(buttonData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.ReadOnly)]
    public class PocCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            string buildLabel = Assembly.GetExecutingAssembly().GetName().Name;
            TaskDialog.Show(
                "PoC #2 - Paquete NuGet de metadatos",
                "El botón del addin PoC #2 se ha ejecutado correctamente.\nBuild: " + buildLabel);
            return Result.Succeeded;
        }
    }
}
