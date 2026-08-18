using Autodesk.Revit.UI;

namespace RevitBridge.Addin.Bridge;

/// <summary>
/// Adaptador <c>IExternalEventHandler</c> para <see cref="ExecutionQueue"/> (F0.5). Deliberadamente
/// trivial: solo reenvía al hilo de la API cuando Revit queda ocioso. Sin lógica propia que
/// testear — toda la lógica de cola/espera vive en <see cref="ExecutionQueue"/>, que sí tiene
/// tests (DOCUMENTACION.md §8: "si aparece lógica que merece test dentro del adaptador, se saca").
/// </summary>
public sealed class ExecutionQueueEventHandler : IExternalEventHandler
{
    private readonly ExecutionQueue _cola;

    public ExecutionQueueEventHandler(ExecutionQueue cola)
    {
        _cola = cola;
    }

    public void Execute(UIApplication app)
    {
        try
        {
            var contexto = new RevitContext(app);
            _cola.Procesar(contexto.Procesar);
        }
        catch (Exception ex)
        {
            // Falla silenciosamente o loggea, pero no crashea la aplicación host
            System.Diagnostics.Trace.WriteLine($"Error en ExecutionQueueEventHandler: {ex}");
        }
    }

    public string GetName() => "RevitBridge.ExecutionQueue";
}
