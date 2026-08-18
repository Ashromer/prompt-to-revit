using System.Diagnostics;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBridge.Core;

namespace RevitBridge.Addin;

/// <summary>
/// Adaptador de la API de Revit para consultas de solo lectura (F0.6, F0.8).
/// Mantiene la costura de abstracción implementando <see cref="IRevitQueryContext"/>.
/// </summary>
public sealed class RevitContext : IRevitQueryContext
{
    private readonly UIApplication _uiapp;
    private readonly Document? _doc;

    public RevitContext(UIApplication uiapp)
    {
        _uiapp = uiapp;
        _doc = uiapp.ActiveUIDocument?.Document;
    }

    public object? Consultar(ConsultaRequest peticion)
    {
        if (_doc is null)
        {
            throw new InvalidOperationException("No hay ningún documento activo en Revit.");
        }

        // Lógica trivial inicial para F0.8 (Consulta del modelo).
        // Se puede ampliar para soportar filtros específicos u otros datos en base a peticion.Consulta
        
        var queryName = peticion.Consulta.ToLowerInvariant();
        
        return queryName switch
        {
            "niveles" => ConsultarNiveles(),
            "tipos" => ConsultarTipos(),
            "seleccion" => ConsultarSeleccion(),
            "documento" => ConsultarDocumento(),
            _ => throw new ArgumentException($"Consulta '{peticion.Consulta}' no soportada por el adaptador actual.")
        };
    }

    private object ConsultarDocumento()
    {
        return new
        {
            Titulo = _doc!.Title,
            Path = _doc.PathName,
            Unidades = _doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId().TypeId
        };
    }

    private object ConsultarNiveles()
    {
        var niveles = new FilteredElementCollector(_doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .Select(l => new { Id = l.Id.Value, Nombre = l.Name, Elevacion = l.Elevation })
            .ToList();
            
        return niveles;
    }

    private object ConsultarTipos()
    {
        var tipos = new FilteredElementCollector(_doc)
            .WhereElementIsElementType()
            .ToElements()
            .Select(e => new { Id = e.Id.Value, Nombre = e.Name, Categoria = e.Category?.Name })
            .Take(100) // Limitamos para no ahogar JSON
            .ToList();
            
        return tipos;
    }

    private object ConsultarSeleccion()
    {
        var uiDoc = _uiapp.ActiveUIDocument;
        if (uiDoc is null) return Array.Empty<object>();

        var selection = uiDoc.Selection.GetElementIds();
        return selection.Select(id => 
        {
            var elem = _doc!.GetElement(id);
            return new { Id = id.Value, Nombre = elem?.Name, Tipo = elem?.GetType().Name };
        }).ToList();
    }

    /// <summary>
    /// Procesador principal de peticiones que se inyecta en la cola de ejecución.
    /// Recibe un <see cref="PeticionPipe"/> y rutea a la operación correspondiente.
    /// </summary>
    public RespuestaOperacion Procesar(PeticionPipe peticion)
    {
        var sw = Stopwatch.StartNew();
        
        if (peticion.Operacion == Operaciones.Query)
        {
            var req = peticion.Datos.Deserialize<ConsultaRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req is null) throw new InvalidOperationException("El payload de ConsultaRequest es inválido o nulo.");
            
            var resultado = Consultar(req);
            
            return new RespuestaOperacion(
                Ok: true,
                Fase: Fase.Ok,
                Resultado: resultado,
                IdsCreados: Array.Empty<long>(),
                Error: null,
                Traza: null,
                DuracionMs: sw.ElapsedMilliseconds);
        }
        else if (peticion.Operacion == Operaciones.Commands)
        {
            // F0.9 (Catálogo)
            var catalogo = Bridge.CommandCatalog.Descubrir(typeof(Utils.ComandoRevitAttribute).Assembly);
            return new RespuestaOperacion(
                Ok: true,
                Fase: Fase.Ok,
                Resultado: catalogo,
                IdsCreados: Array.Empty<long>(),
                Error: null,
                Traza: null,
                DuracionMs: sw.ElapsedMilliseconds);
        }
        else if (peticion.Operacion == Operaciones.Compile)
        {
            // F1.3 (Dry-run de compilación)
            var req = peticion.Datos.Deserialize<CompileRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req is null) throw new InvalidOperationException("El payload de CompileRequest es inválido o nulo.");

            var compiler = new RevitBridge.Core.Compiler.RoslynCompiler();
            var result = compiler.Compile(req.Fuente);

            if (!result.Success)
            {
                var errores = result.Diagnostics.Select(d => d.ToString()).ToList();
                return new RespuestaOperacion(
                    Ok: false,
                    Fase: Fase.Compilacion,
                    Resultado: errores,
                    IdsCreados: Array.Empty<long>(),
                    Error: "Error de compilación",
                    Traza: string.Join(Environment.NewLine, errores),
                    DuracionMs: sw.ElapsedMilliseconds);
            }

            return new RespuestaOperacion(
                Ok: true,
                Fase: Fase.Ok,
                Resultado: "Compilación exitosa",
                IdsCreados: Array.Empty<long>(),
                Error: null,
                Traza: null,
                DuracionMs: sw.ElapsedMilliseconds);
        }
        else if (peticion.Operacion == Operaciones.Exec)
        {
            var req = peticion.Datos.Deserialize<ExecRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req is null) throw new InvalidOperationException("El payload de ExecRequest es nulo.");

            // a) Compilar
            var compiler = new RevitBridge.Core.Compiler.RoslynCompiler();
            var compileResult = compiler.Compile(req.Fuente);
            if (!compileResult.Success)
            {
                var errores = compileResult.Diagnostics.Select(d => d.ToString()).ToList();
                return new RespuestaOperacion(
                    Ok: false,
                    Fase: Fase.Compilacion,
                    Resultado: errores,
                    IdsCreados: Array.Empty<long>(),
                    Error: "Error de compilación",
                    Traza: string.Join(Environment.NewLine, errores),
                    DuracionMs: sw.ElapsedMilliseconds);
            }

            // b) Solicitar aprobación (Síncrono)
            var approvalService = new RevitBridge.Addin.UI.ApprovalService();
            bool isApproved = approvalService.SolicitarAprobacion(req.Fuente);

            if (!isApproved)
            {
                return new RespuestaOperacion(
                    Ok: false,
                    Fase: Fase.Runtime,
                    Resultado: null,
                    IdsCreados: Array.Empty<long>(),
                    Error: "Ejecución rechazada por el usuario.",
                    Traza: null,
                    DuracionMs: sw.ElapsedMilliseconds);
            }

            // d) Ejecutar en transacción
            using (var tx = new Transaction(_doc, "Claude: " + (req.Intencion ?? "ejecución")))
            {
                // e) FailuresPreprocessor
                var failureOptions = tx.GetFailureHandlingOptions();
                failureOptions.SetFailuresPreprocessor(new Bridge.FailuresPreprocessor());
                tx.SetFailureHandlingOptions(failureOptions);

                tx.Start();

                try
                {
                    // f) Invocar assembly
                    var assembly = compileResult.Assembly;
                    var scriptType = assembly?.GetType("Script");
                    if (scriptType == null) throw new InvalidOperationException("No se encontró la clase 'Script' en el código compilado.");

                    var executeMethod = scriptType.GetMethod("Execute", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (executeMethod == null) throw new InvalidOperationException("No se encontró el método estático 'Execute(UIApplication)' en la clase 'Script'.");

                    executeMethod.Invoke(null, new object[] { _uiapp });

                    // g) Commit
                    tx.Commit();

                    return new RespuestaOperacion(
                        Ok: true,
                        Fase: Fase.Ok,
                        Resultado: "Ejecución finalizada con éxito.",
                        IdsCreados: Array.Empty<long>(),
                        Error: null,
                        Traza: null,
                        DuracionMs: sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                    {
                        tx.RollBack();
                    }

                    return new RespuestaOperacion(
                        Ok: false,
                        Fase: Fase.Runtime,
                        Resultado: null,
                        IdsCreados: Array.Empty<long>(),
                        Error: ex.InnerException?.Message ?? ex.Message,
                        Traza: ex.ToString(),
                        DuracionMs: sw.ElapsedMilliseconds);
                }
            }
        }
        
        else if (peticion.Operacion == Operaciones.Rollback)
        {
            var req = peticion.Datos.Deserialize<RollbackRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req is null) throw new InvalidOperationException("El payload de RollbackRequest es nulo.");

            using (var tx = new Transaction(_doc, "Claude: rollback"))
            {
                tx.Start();
                try
                {
                    if (req.Ids != null)
                    {
                        foreach (var idValue in req.Ids)
                        {
#if REVIT2024_OR_GREATER
                            var elementId = new ElementId(idValue);
#else
                            // Asumiendo que idValue cabe en int (aunque idValue es long)
                            var elementId = new ElementId((int)idValue);
#endif
                            _doc!.Delete(elementId);
                        }
                    }
                    tx.Commit();

                    return new RespuestaOperacion(
                        Ok: true,
                        Fase: Fase.Ok,
                        Resultado: "Rollback ejecutado con éxito.",
                        IdsCreados: Array.Empty<long>(),
                        Error: null,
                        Traza: null,
                        DuracionMs: sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    if (tx.GetStatus() == TransactionStatus.Started)
                    {
                        tx.RollBack();
                    }

                    return new RespuestaOperacion(
                        Ok: false,
                        Fase: Fase.Runtime,
                        Resultado: null,
                        IdsCreados: Array.Empty<long>(),
                        Error: ex.InnerException?.Message ?? ex.Message,
                        Traza: ex.ToString(),
                        DuracionMs: sw.ElapsedMilliseconds);
                }
            }
        }
        
        // Operaciones de escritura (Command, Rollback) van en Tier 1 y 2.
        throw new NotSupportedException($"La operación '{peticion.Operacion}' todavía no está implementada en Tier 0.");
    }

    private T WaitSync<T>(Task<T> task)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        task.ContinueWith(_ => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        return task.GetAwaiter().GetResult();
    }
}
