namespace RevitBridge.Core;

/// <summary>
/// Nombres de operación del contrato de la pasarela (DOCUMENTACION.md §4). Son literales dentro
/// del mensaje JSON del named pipe, no rutas HTTP — el transporte es un named pipe, no hay
/// servidor web (CLAUDE.md, "Los nombres de las operaciones... son nombres de operación").
/// </summary>
public static class Operaciones
{
    public const string Commands = "commands";
    public const string Query = "query";
    public const string Compile = "compile";
    public const string Exec = "exec";
    public const string Command = "command";
    public const string Rollback = "rollback";
}
