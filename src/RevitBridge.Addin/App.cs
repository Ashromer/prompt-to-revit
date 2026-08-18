using Autodesk.Revit.UI;
using RevitBridge.Addin.Bridge;
using RevitBridge.Utils;

namespace RevitBridge.Addin;

/// <summary>
/// Punto de entrada del addin (F0.4). <c>OnStartup</c> es el único momento con contexto de API
/// válido para <c>ExternalEvent.Create</c> (DOCUMENTACION.md §5.B.5); arranca ahí la cola de
/// ejecución y, en su propio hilo, el servidor del pipe. El listener del pipe (<see cref="PipeServer"/>)
/// nunca toca la API de Revit — solo conoce <see cref="ExecutionQueue"/> a través de
/// <c>IPeticionExecutor</c>. El <c>FullClassName</c> del <c>.addin</c> debe ser exactamente
/// <c>RevitBridge.Addin.App</c>.
/// </summary>
public sealed class App : IExternalApplication
{
    public static System.Windows.Threading.Dispatcher RevitDispatcher { get; private set; } = null!;
    public static System.Collections.Generic.HashSet<long> ElementosCreadosEnSesion { get; } = new();

    /// <summary>
    /// Identificador de esta sesión de Revit, generado una vez al arrancar. Es la clave de
    /// correlación que usa <c>/rollback</c> (ADR-006) para reconstruir qué se creó leyendo el
    /// JSONL, en vez de confiar en una lista en memoria del lado del puente.
    /// </summary>
    public static string SesionId { get; } = Guid.NewGuid().ToString("N");

    private ExecutionQueue? _cola;
    private ExternalEvent? _externalEvent;
    private PipeServer? _pipeServer;

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            RevitDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            application.ControlledApplication.DocumentChanged += OnDocumentChanged;

            CommandCatalog.Descubrir(typeof(ComandoRevitAttribute).Assembly);

            _cola = new ExecutionQueue();
            var handler = new ExecutionQueueEventHandler(_cola);
            _externalEvent = ExternalEvent.Create(handler);
            _cola.PeticionEncolada += () => _externalEvent?.Raise();

            var nombrePipe = Environment.GetEnvironmentVariable("REVITBRIDGE_PIPE");
            if (string.IsNullOrWhiteSpace(nombrePipe))
            {
                nombrePipe = PipeServer.NombrePorDefecto();
            }

            _pipeServer = new PipeServer(nombrePipe, _cola);
            _pipeServer.Iniciar();

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Autodesk.Revit.UI.TaskDialog.Show("RevitBridge", $"No se pudo arrancar RevitBridge: {ex.GetType().Name}: {ex.Message}");
            return Result.Failed;
        }
    }

    private void OnDocumentChanged(object? sender, Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
    {
        foreach (var id in e.GetAddedElementIds())
        {
            ElementosCreadosEnSesion.Add(id.Value);
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        application.ControlledApplication.DocumentChanged -= OnDocumentChanged;
        _pipeServer?.Dispose();
        _externalEvent?.Dispose();
        return Result.Succeeded;
    }
}
