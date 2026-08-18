using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RevitBridge.Core;

namespace RevitBridge.Addin.Bridge;

/// <summary>
/// Una línea de <c>%APPDATA%\RevitBridge\log\YYYY-MM.jsonl</c>. El fichero es append-only, así
/// que "inicio" y "fin" de una misma ejecución son DOS líneas correlacionadas por <see cref="Id"/>,
/// no una única línea reescrita (no se puede mutar una línea ya escrita en un fichero de solo
/// append). Los campos que no aplican a un evento quedan a <c>null</c>.
/// </summary>
public sealed record SessionLogEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("ts")]
    public DateTimeOffset Ts { get; init; }

    [JsonPropertyName("evento")]
    public string Evento { get; init; } = "";

    [JsonPropertyName("intencion")]
    public string? Intencion { get; init; }

    [JsonPropertyName("via")]
    public string? Via { get; init; }

    [JsonPropertyName("fuente")]
    public string? Fuente { get; init; }

    [JsonPropertyName("sesion")]
    public string? Sesion { get; init; }

    [JsonPropertyName("fase")]
    public Fase? Fase { get; init; }

    [JsonPropertyName("resultado")]
    public object? Resultado { get; init; }

    [JsonPropertyName("ids_creados")]
    public IReadOnlyList<long>? IdsCreados { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("traza")]
    public string? Traza { get; init; }

    [JsonPropertyName("duracion_ms")]
    public long? DuracionMs { get; init; }
}

/// <summary>
/// Registro JSONL de sesión (specs/tech-spec.md §Logging, F0.7, ADR-006). La línea se escribe
/// ANTES de ejecutar y se completa DESPUÉS: si Revit cae a media ejecución queda constancia de qué
/// se estaba intentando. Es I/O puro, sin ningún tipo de <c>Autodesk.Revit.*</c>, para que sea
/// testeable sin Revit pese a vivir en el proyecto del Addin.
/// </summary>
public sealed class SessionLog
{
    public const string EventoInicio = "inicio";
    public const string EventoFin = "fin";

    private readonly string _logDirectory;
    private static readonly JsonSerializerOptions JsonOptions = new();

    public SessionLog(string logDirectory)
    {
        _logDirectory = logDirectory;
    }

    /// <summary>Ruta por defecto en producción; inyectable para que los tests usen un directorio temporal.</summary>
    public static string DirectorioPorDefecto() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RevitBridge", "log");

    /// <summary>
    /// Escribe la línea de "inicio" antes de ejecutar. Devuelve el id de ejecución que hay que
    /// pasar a <see cref="CompletarEntrada"/> para correlacionar la línea de "fin".
    /// </summary>
    public string IniciarEntrada(string intencion, string via, string fuente, string sesion)
    {
        var id = Guid.NewGuid().ToString("N");
        var ts = DateTimeOffset.UtcNow;

        EscribirLinea(ts, new SessionLogEntry
        {
            Id = id,
            Ts = ts,
            Evento = EventoInicio,
            Intencion = intencion,
            Via = via,
            Fuente = fuente,
            Sesion = sesion
        });

        return id;
    }

    /// <summary>Escribe la línea de "fin" con el resultado, después de ejecutar.</summary>
    public void CompletarEntrada(
        string id,
        Fase fase,
        object? resultado,
        IReadOnlyList<long> idsCreados,
        string? error,
        string? traza,
        long duracionMs)
    {
        var ts = DateTimeOffset.UtcNow;

        EscribirLinea(ts, new SessionLogEntry
        {
            Id = id,
            Ts = ts,
            Evento = EventoFin,
            Fase = fase,
            Resultado = resultado,
            IdsCreados = idsCreados,
            Error = error,
            Traza = traza,
            DuracionMs = duracionMs
        });
    }

    /// <summary>
    /// Reconstruye los ids creados por una sesión leyendo el/los JSONL del directorio de log
    /// (base de <c>/rollback</c>, F1.9). Tolera ficheros truncados o con una línea corrupta al
    /// final: la línea rota se ignora, no se lanza excepción.
    /// </summary>
    public IReadOnlyList<long> ReconstruirIdsCreados(string sesion)
    {
        var sesionPorId = new Dictionary<string, string>();
        var idsCreados = new List<long>();

        foreach (var entrada in LeerLineasValidas())
        {
            if (entrada.Evento == EventoInicio && entrada.Sesion is not null)
            {
                sesionPorId[entrada.Id] = entrada.Sesion;
                continue;
            }

            if (entrada.Evento == EventoFin && entrada.IdsCreados is { Count: > 0 })
            {
                if (sesionPorId.TryGetValue(entrada.Id, out var sesionEjecucion) && sesionEjecucion == sesion)
                {
                    idsCreados.AddRange(entrada.IdsCreados);
                }
            }
        }

        return idsCreados;
    }

    private void EscribirLinea(DateTimeOffset ts, SessionLogEntry entrada)
    {
        Directory.CreateDirectory(_logDirectory);
        var linea = JsonSerializer.Serialize(entrada, JsonOptions);

        using var stream = new FileStream(RutaFichero(ts), FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream);
        writer.WriteLine(linea);
    }

    private string RutaFichero(DateTimeOffset ts) =>
        Path.Combine(_logDirectory, $"{ts:yyyy-MM}.jsonl");

    private IEnumerable<SessionLogEntry> LeerLineasValidas()
    {
        if (!Directory.Exists(_logDirectory))
        {
            yield break;
        }

        var ficheros = Directory.GetFiles(_logDirectory, "*.jsonl").OrderBy(f => f, StringComparer.Ordinal);

        foreach (var fichero in ficheros)
        {
            string[] lineas;
            try
            {
                lineas = File.ReadAllLines(fichero);
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var linea in lineas)
            {
                if (string.IsNullOrWhiteSpace(linea))
                {
                    continue;
                }

                SessionLogEntry? entrada = null;
                try
                {
                    entrada = JsonSerializer.Deserialize<SessionLogEntry>(linea, JsonOptions);
                }
                catch (JsonException)
                {
                    // Línea corrupta o truncada (p. ej. Revit cerrado a media escritura): se ignora.
                }

                if (entrada is not null)
                {
                    yield return entrada;
                }
            }
        }
    }
}
