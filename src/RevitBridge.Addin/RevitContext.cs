using System.Diagnostics;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitBridge.Core;
using System.Reflection;

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

            // c) Log ANTES de ejecutar (§5.D.17, ADR-006): si Revit cae a media ejecución, esta
            // línea de "inicio" huérfana es la evidencia de qué se estaba intentando. También es
            // la fuente que /rollback reconstruye después.
            var sessionLog = new Bridge.SessionLog(Bridge.SessionLog.DirectorioPorDefecto());
            var logId = sessionLog.IniciarEntrada(req.Intencion ?? "", "roslyn", req.Fuente, App.SesionId);

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

                    // El valor de retorno del script ES el contrato de §4 (`return new { ids = ... }`):
                    // antes de esta corrección se descartaba y la respuesta siempre mandaba
                    // ids_creados vacío y un string fijo, aunque el script sí hubiera creado geometría.
                    var resultadoScript = executeMethod.Invoke(null, new object[] { _uiapp });

                    // g) Commit
                    tx.Commit();

                    var idsCreados = ResultadoScriptExtractor.ExtraerIdsCreados(resultadoScript);
                    sessionLog.CompletarEntrada(logId, Fase.Ok, resultadoScript, idsCreados, null, null, sw.ElapsedMilliseconds);

                    return new RespuestaOperacion(
                        Ok: true,
                        Fase: Fase.Ok,
                        Resultado: resultadoScript,
                        IdsCreados: idsCreados,
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

                    var errorMsg = ex.InnerException?.Message ?? ex.Message;
                    sessionLog.CompletarEntrada(logId, Fase.Runtime, null, Array.Empty<long>(), errorMsg, ex.ToString(), sw.ElapsedMilliseconds);

                    return new RespuestaOperacion(
                        Ok: false,
                        Fase: Fase.Runtime,
                        Resultado: null,
                        IdsCreados: Array.Empty<long>(),
                        Error: errorMsg,
                        Traza: ex.ToString(),
                        DuracionMs: sw.ElapsedMilliseconds);
                }
            }
        }
        
        else if (peticion.Operacion == Operaciones.Command)
        {
            var req = peticion.Datos.Deserialize<CommandRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req is null) throw new InvalidOperationException("El payload de CommandRequest es nulo.");

            var assemblyUtils = typeof(Utils.ComandoRevitAttribute).Assembly;
            var assemblyAddin = typeof(RevitContext).Assembly;
            var metodos = new[] { assemblyUtils, assemblyAddin }
                .SelectMany(a => a.GetTypes())
                .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                .Where(m => m.GetCustomAttribute<Utils.ComandoRevitAttribute>()?.Nombre.Equals(req.Nombre, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (metodos.Count == 0) throw new InvalidOperationException($"Comando '{req.Nombre}' no encontrado.");
            var metodo = metodos.First();

            var parameters = metodo.GetParameters();
            var args = new object?[parameters.Length];
            
            for(int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.ParameterType == typeof(UIApplication)) { args[i] = _uiapp; continue; }
                if (p.ParameterType == typeof(Document)) { args[i] = _doc; continue; }
                
                if (req.Argumentos is JsonElement argsDict && argsDict.ValueKind == JsonValueKind.Object && argsDict.TryGetProperty(p.Name ?? "", out var je))
                {
                    args[i] = JsonSerializer.Deserialize(je.GetRawText(), p.ParameterType);
                }
                else
                {
                    args[i] = p.HasDefaultValue ? p.DefaultValue : null;
                }
            }

            // Log ANTES de invocar (§5.D.17 / ADR-006, mismo criterio que /exec). "via": "command"
            // en vez de "roslyn" -- es lo que permite a F2.3 (cosecha del log) calcular el reparto
            // Roslyn-vs-comando-compilado que el propio DOCUMENTACION.md §6 usa como señal de salud
            // del catálogo. Antes de este fix, /command no escribía nada: esa señal era imposible de
            // calcular porque faltaba la mitad de los datos.
            var sessionLogComando = new Bridge.SessionLog(Bridge.SessionLog.DirectorioPorDefecto());
            var logIdComando = sessionLogComando.IniciarEntrada(req.Nombre, "command", JsonSerializer.Serialize(req), App.SesionId);

            try
            {
                var resultado = metodo.Invoke(null, args);
                var idsCreadosComando = ResultadoScriptExtractor.ExtraerIdsCreados(resultado);
                sessionLogComando.CompletarEntrada(logIdComando, Fase.Ok, resultado, idsCreadosComando, null, null, sw.ElapsedMilliseconds);

                return new RespuestaOperacion(Ok: true, Fase: Fase.Ok, Resultado: resultado, IdsCreados: idsCreadosComando, Error: null, Traza: null, DuracionMs: sw.ElapsedMilliseconds);
            }
            catch(Exception ex)
            {
                var errorMsgComando = ex.InnerException?.Message ?? ex.Message;
                sessionLogComando.CompletarEntrada(logIdComando, Fase.Runtime, null, Array.Empty<long>(), errorMsgComando, ex.ToString(), sw.ElapsedMilliseconds);

                return new RespuestaOperacion(Ok: false, Fase: Fase.Runtime, Resultado: null, IdsCreados: Array.Empty<long>(), Error: errorMsgComando, Traza: ex.ToString(), DuracionMs: sw.ElapsedMilliseconds);
            }
        }
        else if (peticion.Operacion == Operaciones.Rollback)
        {
            var req = peticion.Datos.Deserialize<RollbackRequest>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (req is null) throw new InvalidOperationException("El payload de RollbackRequest es nulo.");

            // ADR-006 / F1.9: por defecto se reconstruye desde el JSONL de la sesión, no de una
            // lista en memoria del lado del puente -- así sobrevive a una caída de Revit, que es
            // precisamente cuando más falta hace poder deshacer. Ids explícitos en la petición son
            // un override manual (rollback parcial).
            var sessionLog = new Bridge.SessionLog(Bridge.SessionLog.DirectorioPorDefecto());
            IReadOnlyList<long> idsABorrar = (req.Ids is { Count: > 0 })
                ? req.Ids
                : sessionLog.ReconstruirIdsCreados(App.SesionId);

            if (idsABorrar.Count == 0)
            {
                return new RespuestaOperacion(
                    Ok: true,
                    Fase: Fase.Ok,
                    Resultado: "No hay elementos que revertir en esta sesión.",
                    IdsCreados: Array.Empty<long>(),
                    Error: null,
                    Traza: null,
                    DuracionMs: sw.ElapsedMilliseconds);
            }

            // §5.C.9 / F1.9: previsualización (cuántos, de qué categorías) y confirmación manual
            // obligatoria antes de borrar -- el rollback anterior borraba directo, sin previsualizar.
            var elementosAPrevisualizar = idsABorrar
                .Select(idValue => _doc!.GetElement(ToElementId(idValue)))
                .Where(e => e != null)
                .ToList();

            var categorias = elementosAPrevisualizar
                .GroupBy(e => e!.Category?.Name ?? "Desconocido")
                .Select(g => $"{g.Count()} de {g.Key}");
            var resumen = $"ROLLBACK: vas a borrar {elementosAPrevisualizar.Count} elemento(s) creados en esta sesión:\n- "
                + string.Join("\n- ", categorias);

            var approval = new RevitBridge.Addin.UI.ApprovalService();
            if (!approval.SolicitarAprobacion(resumen))
            {
                return new RespuestaOperacion(
                    Ok: false,
                    Fase: Fase.Runtime,
                    Resultado: null,
                    IdsCreados: Array.Empty<long>(),
                    Error: "Rollback cancelado por el usuario.",
                    Traza: null,
                    DuracionMs: sw.ElapsedMilliseconds);
            }

            using (var tx = new Transaction(_doc, "Claude: rollback"))
            {
                tx.Start();
                try
                {
                    var idsBorrados = new List<long>();
                    foreach (var idValue in idsABorrar)
                    {
                        var borrados = _doc!.Delete(ToElementId(idValue));
                        if (borrados != null)
                        {
                            idsBorrados.AddRange(borrados.Select(i => (long)i.Value));
                        }
                    }
                    tx.Commit();

                    return new RespuestaOperacion(
                        Ok: true,
                        Fase: Fase.Ok,
                        Resultado: $"Rollback ejecutado: {idsBorrados.Count} elemento(s) borrados.",
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

    private static ElementId ToElementId(long idValue)
    {
#if REVIT2024_OR_GREATER
        return new ElementId(idValue);
#else
        return new ElementId((int)idValue);
#endif
    }
}
