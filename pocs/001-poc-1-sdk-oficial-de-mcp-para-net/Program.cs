using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PocMcpSdk;

var builder = Host.CreateApplicationBuilder(args);

// Higiene de stdout (regla no negociable, DECISION-PELDANO.md §3.4): en stdio,
// stdout ES el canal JSON-RPC. Todo el logging va a stderr; nunca Console.WriteLine.
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Instrumentación desechable del Lote 2 (peldaño 2 medido, no adivinado): registra a fichero
// qué métodos JSON-RPC atiende de verdad el servidor, con su frecuencia. Escucha por ILogger,
// nunca toca stdout. Ver RpcMethodLoggerProvider.cs para el porqué del enfoque.
builder.Logging.AddProvider(new RpcMethodLoggerProvider());

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
