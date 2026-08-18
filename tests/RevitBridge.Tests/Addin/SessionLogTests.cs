using System.Text.Json;
using RevitBridge.Addin.Bridge;
using RevitBridge.Core;
using Xunit;

namespace RevitBridge.Tests.Addin;

/// <summary>
/// specs/tech-spec.md §Testing Strategy, fila SessionLog: escritura antes de ejecutar, formato
/// JSONL, reconstrucción de ids de sesión, ficheros corruptos o truncados. Sistema de ficheros
/// temporal, sin Revit.
/// </summary>
public class SessionLogTests : IDisposable
{
    private readonly string _directorio;

    public SessionLogTests()
    {
        _directorio = Path.Combine(Path.GetTempPath(), "RevitBridge.Tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directorio))
        {
            Directory.Delete(_directorio, recursive: true);
        }
    }

    [Fact]
    public void IniciarEntrada_Escribe_La_Linea_Antes_De_Ejecutar()
    {
        var log = new SessionLog(_directorio);

        var id = log.IniciarEntrada("crear un muro", "exec", "public static class Script { }", "sesion-1");

        var ficheros = Directory.GetFiles(_directorio, "*.jsonl");
        Assert.Single(ficheros);

        var linea = File.ReadAllLines(ficheros[0]).Single();
        var entrada = JsonSerializer.Deserialize<SessionLogEntry>(linea)!;

        Assert.Equal(id, entrada.Id);
        Assert.Equal(SessionLog.EventoInicio, entrada.Evento);
        Assert.Equal("crear un muro", entrada.Intencion);
        Assert.Equal("exec", entrada.Via);
        Assert.Equal("public static class Script { }", entrada.Fuente);
        Assert.Equal("sesion-1", entrada.Sesion);
        Assert.Null(entrada.Fase);
    }

    [Fact]
    public void CompletarEntrada_Anade_Una_Segunda_Linea_Correlacionada_Por_Id()
    {
        var log = new SessionLog(_directorio);

        var id = log.IniciarEntrada("crear un muro", "exec", "fuente", "sesion-1");
        log.CompletarEntrada(id, Fase.Ok, new { ids = new[] { 1L } }, new long[] { 1 }, null, null, 42);

        var ficheros = Directory.GetFiles(_directorio, "*.jsonl");
        var lineas = File.ReadAllLines(ficheros[0]);
        Assert.Equal(2, lineas.Length);

        var fin = JsonSerializer.Deserialize<SessionLogEntry>(lineas[1])!;
        Assert.Equal(id, fin.Id);
        Assert.Equal(SessionLog.EventoFin, fin.Evento);
        Assert.Equal(Fase.Ok, fin.Fase);
        Assert.Equal(42, fin.DuracionMs);
        Assert.Equal(new long[] { 1 }, fin.IdsCreados);
    }

    [Fact]
    public void Fichero_Se_Nombra_Por_El_Mes_Del_Ts()
    {
        var log = new SessionLog(_directorio);

        log.IniciarEntrada("intencion", "exec", "fuente", "sesion-1");

        var nombreEsperado = $"{DateTimeOffset.UtcNow:yyyy-MM}.jsonl";
        Assert.True(File.Exists(Path.Combine(_directorio, nombreEsperado)));
    }

    [Fact]
    public void ReconstruirIdsCreados_Agrega_Los_Ids_De_La_Sesion_Pedida()
    {
        var log = new SessionLog(_directorio);

        var id1 = log.IniciarEntrada("intencion 1", "exec", "fuente", "sesion-A");
        log.CompletarEntrada(id1, Fase.Ok, null, new long[] { 1, 2 }, null, null, 10);

        var id2 = log.IniciarEntrada("intencion 2", "exec", "fuente", "sesion-A");
        log.CompletarEntrada(id2, Fase.Ok, null, new long[] { 3 }, null, null, 10);

        var id3 = log.IniciarEntrada("intencion 3", "exec", "fuente", "sesion-B");
        log.CompletarEntrada(id3, Fase.Ok, null, new long[] { 99 }, null, null, 10);

        var ids = log.ReconstruirIdsCreados("sesion-A");

        Assert.Equal(new long[] { 1, 2, 3 }, ids);
    }

    [Fact]
    public void ReconstruirIdsCreados_Ignora_Fallos_Sin_Ids()
    {
        var log = new SessionLog(_directorio);

        var id = log.IniciarEntrada("intencion", "exec", "fuente", "sesion-A");
        log.CompletarEntrada(id, Fase.Runtime, null, Array.Empty<long>(), "algo falló", "traza", 5);

        var ids = log.ReconstruirIdsCreados("sesion-A");

        Assert.Empty(ids);
    }

    [Fact]
    public void Tolera_Fichero_Truncado_Con_Linea_Corrupta_Al_Final()
    {
        var log = new SessionLog(_directorio);

        var id = log.IniciarEntrada("intencion", "exec", "fuente", "sesion-A");
        log.CompletarEntrada(id, Fase.Ok, null, new long[] { 7 }, null, null, 5);

        var ruta = Directory.GetFiles(_directorio, "*.jsonl").Single();
        File.AppendAllText(ruta, "{\"id\":\"roto\",\"evento\":\"fin\",\"ids_crea"); // línea a medio escribir, sin salto de línea final

        var ids = log.ReconstruirIdsCreados("sesion-A");

        Assert.Equal(new long[] { 7 }, ids);
    }

    [Fact]
    public void ReconstruirIdsCreados_Sin_Directorio_De_Log_Devuelve_Vacio()
    {
        var log = new SessionLog(Path.Combine(_directorio, "no-existe"));

        var ids = log.ReconstruirIdsCreados("sesion-A");

        Assert.Empty(ids);
    }

    [Fact]
    public void DirectorioPorDefecto_Vive_Bajo_AppData_RevitBridge_Log()
    {
        var ruta = SessionLog.DirectorioPorDefecto();

        Assert.EndsWith(Path.Combine("RevitBridge", "log"), ruta);
    }
}
