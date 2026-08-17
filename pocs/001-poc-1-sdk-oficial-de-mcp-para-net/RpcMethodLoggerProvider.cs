using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PocMcpSdk;

// Instrumentación del Lote 2 ("El experimento"): registrar qué métodos JSON-RPC atiende de
// verdad el servidor durante las pruebas con Claude Code, con su frecuencia, para convertir el
// peldaño 2 (implementación manual del protocolo si el SDK oficial dejara de servir) en una
// cantidad medida en vez de una estimación a ciegas. Se aprovecha aunque nunca se llegue a usar.
//
// Enfoque elegido: el SDK YA emite por su propio ILogger, en la categoría
// "ModelContextProtocol.Server.McpServer", una línea por cada método atendido (capturada de
// stderr real, ver encabezado de la tarea / DECISION-PELDANO.md):
//   Server (PocMcpSdk 1.0.0.0) method 'initialize' request handler called.
//   Server (PocMcpSdk 1.0.0.0), Client (manual-test 1.0) method 'tools/list' request handler completed in 14.0756ms.
// No se encontró en el XML de ModelContextProtocol[.Core] 2.2.0 ningún middleware o evento de
// servidor para peticiones ENTRANTES (solo existe WithOutgoingRequestInterceptor, para peticiones
// que el servidor envía al cliente) — así que no hay una vía más directa que confirmar en el SDK.
// Esta clase es un ILoggerProvider adicional, registrado junto al logging a stderr existente, que
// escucha esa única categoría y no toca el protocolo ni el transporte para nada.
//
// Se cuenta cada método una sola vez por llamada, en el mensaje "... request handler called.":
// el mismo SDK emite después un segundo mensaje "... request handler completed in Xms." para la
// misma invocación, y contarlo también duplicaría la frecuencia.
internal sealed class RpcMethodLoggerProvider : ILoggerProvider
{
    private const string CategoriaObjetivo = "ModelContextProtocol.Server.McpServer";

    private static readonly Regex PatronMetodoLlamado =
        new(@"method '([^']+)' request handler called\.", RegexOptions.Compiled);

    private readonly string _rutaLog;
    private readonly object _cerrojoArchivo = new();
    private readonly ConcurrentDictionary<string, long> _recuento = new();

    // Longitud del fichero que se considera "definitiva" (todas las líneas por llamada
    // escritas hasta ahora, de esta sesión y de sesiones anteriores). El bloque de recuento
    // se escribe SIEMPRE por detrás de esta marca, y se trunca antes de cada reescritura: así
    // el recuento nunca se duplica ni se acumula al final del fichero. Se inicializa la primera
    // vez que se escribe algo en esta sesión, con el tamaño que el fichero tuviera ya en disco.
    private long _longitudSinRecuento;
    private bool _longitudInicializada;

    public RpcMethodLoggerProvider()
    {
        string carpetaBase = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _rutaLog = Path.Combine(carpetaBase, "PocMcpSdk", "rpc-methods.log");
    }

    public ILogger CreateLogger(string categoryName) =>
        categoryName == CategoriaObjetivo
            ? new RpcMethodLogger(this)
            : NullLogger.Instance;

    private void RegistrarSiEsLlamadaAMetodo(string mensaje)
    {
        Match match = PatronMetodoLlamado.Match(mensaje);
        if (!match.Success)
        {
            return;
        }

        string metodo = match.Groups[1].Value;
        _recuento.AddOrUpdate(metodo, 1, (_, actual) => actual + 1);

        EscribirLineaYRecuento($"{DateTimeOffset.Now:O}\t{metodo}");
    }

    // No se confía en que el cierre del host llegue a ejecutarse (visto en la práctica: el
    // recuento colgado solo de Dispose() no llegaba a escribirse). En su lugar, cada llamada
    // trunca el bloque de recuento anterior y reescribe uno nuevo con los totales al día: el
    // fichero contiene un recuento válido incluso si el proceso muere sin apagado limpio.
    // Tolerante a fallos por diseño (regla no negociable): un error aquí no debe tumbar el
    // servidor ni, sobre todo, escribir nada en stdout (que es el canal JSON-RPC en stdio).
    // Pero tampoco se traga en silencio: el motivo va a stderr para poder diagnosticarlo.
    private void EscribirLineaYRecuento(string lineaLlamada)
    {
        try
        {
            lock (_cerrojoArchivo)
            {
                string? carpeta = Path.GetDirectoryName(_rutaLog);
                if (!string.IsNullOrEmpty(carpeta) && !Directory.Exists(carpeta))
                {
                    Directory.CreateDirectory(carpeta);
                }

                using var flujo = new FileStream(_rutaLog, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

                if (!_longitudInicializada)
                {
                    _longitudSinRecuento = flujo.Length;
                    _longitudInicializada = true;
                }

                if (flujo.Length > _longitudSinRecuento)
                {
                    flujo.SetLength(_longitudSinRecuento);
                }

                flujo.Seek(0, SeekOrigin.End);
                byte[] bytesLinea = Encoding.UTF8.GetBytes(lineaLlamada + Environment.NewLine);
                flujo.Write(bytesLinea, 0, bytesLinea.Length);
                flujo.Flush();
                _longitudSinRecuento = flujo.Length;

                byte[] bytesRecuento = Encoding.UTF8.GetBytes(
                    ConstruirBloqueRecuento("--- recuento de ESTA sesión del servidor (no del fichero completo; las líneas de arriba pueden ser de sesiones anteriores) ---"));
                flujo.Write(bytesRecuento, 0, bytesRecuento.Length);
                flujo.Flush();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[PocMcpSdk] RpcMethodLoggerProvider: fallo escribiendo en '{_rutaLog}': {ex}");
        }
    }

    private string ConstruirBloqueRecuento(string titulo)
    {
        var resumen = new StringBuilder();
        string marca = DateTimeOffset.Now.ToString("O");
        resumen.AppendLine($"{marca}\t{titulo}");
        foreach (KeyValuePair<string, long> par in _recuento.OrderByDescending(p => p.Value))
        {
            resumen.AppendLine($"{marca}\t{par.Key}\t{par.Value}");
        }

        return resumen.ToString();
    }

    // Se invoca al cerrar el ILoggerFactory (apagado limpio del host). El recuento ya está
    // escrito y al día tras cada llamada (ver EscribirLineaYRecuento); aquí solo se deja
    // constancia de que hubo un cierre limpio, reescribiendo el mismo bloque con otro título.
    public void Dispose()
    {
        if (!_longitudInicializada || _recuento.IsEmpty)
        {
            return;
        }

        try
        {
            lock (_cerrojoArchivo)
            {
                using var flujo = new FileStream(_rutaLog, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

                if (flujo.Length > _longitudSinRecuento)
                {
                    flujo.SetLength(_longitudSinRecuento);
                }

                flujo.Seek(0, SeekOrigin.End);
                byte[] bytesRecuento = Encoding.UTF8.GetBytes(
                    ConstruirBloqueRecuento("--- recuento de ESTA sesión del servidor, al cerrar (no del fichero completo; las líneas de arriba pueden ser de sesiones anteriores) ---"));
                flujo.Write(bytesRecuento, 0, bytesRecuento.Length);
                flujo.Flush();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[PocMcpSdk] RpcMethodLoggerProvider.Dispose: fallo escribiendo en '{_rutaLog}': {ex}");
        }
    }

    private sealed class RpcMethodLogger : ILogger
    {
        private readonly RpcMethodLoggerProvider _provider;

        public RpcMethodLogger(RpcMethodLoggerProvider provider)
        {
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            try
            {
                string mensaje = formatter(state, exception);
                _provider.RegistrarSiEsLlamadaAMetodo(mensaje);
            }
            catch (Exception ex)
            {
                // Nunca debe tumbar el pipeline de logging del SDK, pero el motivo del fallo
                // se deja en stderr (nunca en stdout: es el canal JSON-RPC en stdio).
                Console.Error.WriteLine(
                    $"[PocMcpSdk] RpcMethodLoggerProvider.Log: fallo procesando entrada de log: {ex}");
            }
        }
    }
}
