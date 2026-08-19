using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RevitBridge.Mcp.Bridge;

var builder = Host.CreateApplicationBuilder(args);

// Higiene de stdout (regla no negociable, DECISION-PELDANO.md §3.4): en stdio,
// stdout ES el canal JSON-RPC. Todo el logging va a stderr; nunca Console.WriteLine.
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Registrar el cliente de pipe en DI para que las herramientas lo usen. Sin REVITBRIDGE_PIPE,
// PipeClient descubre el PID de Revit en vivo en cada envío — no hay fallback fijo que pueda
// quedar obsoleto.
var nombrePipe = Environment.GetEnvironmentVariable("REVITBRIDGE_PIPE");
builder.Services.AddSingleton(new PipeClient(nombrePipe));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
