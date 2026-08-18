using System.Text.Json;
using System.Text.Json.Serialization;

namespace RevitBridge.Core;

/// <summary>
/// Envoltorio de transporte de cualquier petición del named pipe (F0.3): <see cref="Operacion"/>
/// es uno de los literales de <see cref="Operaciones"/>, y <see cref="Datos"/> lleva el payload
/// específico de esa operación (<see cref="ConsultaRequest"/>, <see cref="ExecRequest"/>, etc.)
/// sin parsear todavía. Quien ejecuta la petición (<see cref="IPeticionExecutor"/>) decide cómo
/// deserializar <see cref="Datos"/> según <see cref="Operacion"/>.
/// </summary>
public sealed record PeticionPipe(
    [property: JsonPropertyName("operacion")] string Operacion,
    [property: JsonPropertyName("datos"), JsonConverter(typeof(JsonElementOrNullConverter))] JsonElement Datos = default);
