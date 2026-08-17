# Decisión de peldaño — SDK oficial de MCP para .NET

PoC: `001-poc-1-sdk-oficial-de-mcp-para-net` · Lote 1, tarea 2 (Determinar el peldaño)
Entrada: `pocs/001-poc-1-sdk-oficial-de-mcp-para-net/RECONOCIMIENTO.md`
Plan: `specs/001-poc-1-sdk-oficial-de-mcp-para-net/plan.md` · Requirements: `specs/001-poc-1-sdk-oficial-de-mcp-para-net/requirements.md`
Fecha de consulta de las fuentes nuevas de este documento: **2026-08-17**

> Alcance: se decide el peldaño y se fija la API concreta que usará el Lote 2. **No se ha escrito
> código de aplicación ni se ha creado el proyecto del PoC.**

---

## 1. Decisión

> **PELDAÑO 1.** El paquete `ModelContextProtocol` **2.2.0** es estable y cubre las dos capacidades
> exigidas — transporte **stdio** y **declaración de herramientas con esquema tipado**. El Lote 2 se
> construye **sobre el SDK oficial**. No se implementa a mano el subconjunto JSON-RPC.
>
> **ADR-001 queda confirmado en su eje documental.** Su eje empírico (que Claude Code hable con el
> servidor) es el Lote 3 y **no lo puede cerrar ningún agente**.

Consecuencias procedimentales:

- **FR-011 no se dispara**: la versión no es preview. 2.2.0 es estable, y la línea estable arrancó en
  `1.0.0` el **2026-02-25**, hace ~6 meses. No es una estabilidad de días.
- **FR-012 no se dispara**: el esquema tipado existe y se genera desde la firma del método .NET.
- El **peldaño 2 no se descarta, se aparca**: sigue siendo el destino si el Lote 3 falla por una causa
  atribuible al SDK. La instrumentación de métodos JSON-RPC del Lote 2 (tarea ya prevista en el plan)
  es lo que mantiene ese peldaño como cantidad conocida.
- El **peldaño 3 (Node/TypeScript) sigue siendo alcanzable** solo por la vía que define la escalera:
  que ni el SDK ni una implementación propia consigan la conversación. Nada de lo verificado aquí lo
  hace más probable.

### Por qué, en una línea por criterio

| Criterio de la escalera | Veredicto | Evidencia decisiva |
|---|---|---|
| Paquete estable (no preview) | **Sí** | 2.2.0 sin sufijo de prerelease; `prerelease: false` en la release de GitHub (RECONOCIMIENTO §3.1, §3.2) |
| Transporte stdio | **Sí** | `WithStdioServerTransport()` en los samples del repo **en el tag `v2.2.0`** y en la doc `/v2/` |
| Esquema tipado | **Sí** | `[McpServerToolType]` + `[McpServerTool]` + `[Description]`, JSON Schema 2020-12 generado desde la firma, en la doc `/v2/` y en los samples del tag `v2.2.0` |
| Compatible con `net8.0` | **Sí** | los `.csproj` de los samples del tag `v2.2.0` targetean `net8.0` |

---

## 2. Salvedad (a) resuelta — la API de la línea 2.x es la misma

El reconocimiento dejó abierto que la documentación citada vivía bajo `/v1/` mientras el paquete iba
por 2.x, sin verificar si `WithStdioServerTransport()` y los atributos sobreviven en 2.x.

**Resuelto: existe un set de documentación `/v2/` y la API de stdio + herramientas tipadas NO cambió.**
Verificado por tres fuentes independientes que concuerdan:

1. **Existe `https://csharp.sdk.modelcontextprotocol.io/v2/...`** y sirve contenido distinto del `/v1/`
   (p. ej. el cliente pasa a `McpClient.CreateAsync`, y el servidor HTTP usa
   `HttpServerSessionMode.Stateless`). No es un espejo del `/v1/`: es documentación propia de 2.x.
2. **Los samples del repositorio en el tag `v2.2.0`** (no en `main`, para que la evidencia esté clavada
   a la versión que se va a fijar) usan exactamente la misma forma.
3. **Las release notes de 2.0.0 y el anuncio oficial de Microsoft** no listan ningún breaking change que
   toque el registro de stdio ni los atributos de herramienta. El blog es explícito: *"Stable,
   non-deprecated 1.x APIs continue to compile and run in 2.0. The deprecations introduced in this
   release... are **warnings, not removals**."*

Los breaking changes reales de 2.0.0 caen **fuera** de la superficie que usa este PoC: HTTP stateless
por defecto, deprecación de Roots/Sampling/Logging (`MCP9005`), migración de Tasks a un paquete aparte,
endurecimiento de OAuth. Ninguno afecta a un servidor stdio con herramientas por atributos.

---

## 3. La API 2.x concreta que usará el Lote 2

Todos los snippets de esta sección son **citas literales de fuentes oficiales**, no reconstrucciones.
Se indica la fuente exacta de cada uno.

### 3.1 Servidor stdio — forma canónica

Fuente: `https://csharp.sdk.modelcontextprotocol.io/v2/concepts/getting-started.html` (doc de la línea 2.x):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();

[McpServerToolType]
public static class EchoTool
{
    [McpServerTool, Description("Echoes the message back to the client.")]
    public static string Echo(string message) => $"hello {message}";
}
```

Confirmación cruzada en el repositorio, `samples/QuickstartWeatherServer/Program.cs` **en el tag `v2.2.0`**
(aquí con registro explícito de tipo en lugar de escaneo de ensamblado):

```csharp
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<WeatherTools>();
```

- Las **dos formas de registro son válidas en 2.2.0**: `WithToolsFromAssembly()` (descubre todo lo
  marcado con `[McpServerToolType]`) y `WithTools<T>()` (explícito, encadenable). Para un PoC con 3
  herramientas en una clase, **`WithTools<T>()` es preferible**: hace el registro explícito y elimina
  el escaneo de ensamblado como posible causa de "la herramienta no aparece", que es precisamente la
  Edge Case abierta de este PoC.
- La página de transportes de la doc `/v2/` repite la misma cadena
  (`AddMcpServer()` → `WithStdioServerTransport()` → `WithTools<MyTools>()`) y remite al tipo
  `StdioServerTransport`.

### 3.2 Herramienta con esquema tipado

Fuente: `samples/QuickstartWeatherServer/Tools/WeatherTools.cs` en el tag `v2.2.0` (extracto literal):

```csharp
[McpServerToolType]
public sealed class WeatherTools
{
    [McpServerTool, Description("Get weather forecast for a location.")]
    public static async Task<string> GetForecast(
        HttpClient client,
        [Description("Latitude of the location.")] double latitude,
        [Description("Longitude of the location.")] double longitude)
    { /* ... */ }
}
```

- Atributos vigentes en 2.x: **`[McpServerToolType]`** en la clase, **`[McpServerTool]`** en el método,
  **`[Description]`** (`System.ComponentModel`) en método y parámetros.
- La doc `/v2/` mantiene la afirmación de esquema: *"JSON schemas are automatically generated from .NET
  method signatures when the `[McpServerTool]` attribute is applied"*, con mapeo **JSON Schema 2020-12**.
- Detalle útil y no obvio, visible en el sample: los parámetros que son **servicios inyectados por DI**
  (`HttpClient client`) conviven con los parámetros de la herramienta y no forman parte del esquema
  expuesto. El PoC no necesita DI, pero conviene saberlo para no leerlo como una anomalía del esquema.

### 3.3 Tipo de retorno — decisión que condiciona FR-004 y SC-003

Fuente: `https://csharp.sdk.modelcontextprotocol.io/v2/concepts/tools/tools.html`.

- Un retorno **`string`** *"is automatically wrapped in a `TextContentBlock`"*.
- El contenido **estructurado es opt-in**: *"Set `UseStructuredContent = true` on
  `McpServerToolAttribute`... to advertise an output schema and serialize the return value into
  `StructuredContent`"*. Si no se activa, no hay `structuredContent`.
- Un fallo se señaliza con `IsError = true` en el `CallToolResult`; las excepciones normales se
  convierten en tool error result, y **`McpProtocolException` se re-lanza como error JSON-RPC**.

**Decisión para el Lote 2:** la herramienta de error (FR-004) **devuelve un `string`** con el JSON del
contrato (`ok`, `fase`, `error`, `traza`) serializado, **sin `UseStructuredContent`, sin `IsError`, y sin
lanzar excepción**.

- *Descartado*: devolver un objeto C# tipado con `UseStructuredContent = true`. Es más elegante y más
  cercano al contrato real de la pasarela, pero mete un serializador y una negociación de output schema
  entre lo emitido y lo recibido, justo en el criterio que se mide por **comparación carácter por
  carácter** (SC-003). Un recorte o un reformateo del JSON se leería como truncado del transporte.
- *Descartado*: marcar `IsError = true` o lanzar excepción. Contradice FR-004 de raíz: el fallo debe
  viajar **dentro de una respuesta correcta**, no como error de protocolo.
- *Por qué esta*: `string` → `TextContentBlock` es el camino más corto y el único donde "lo emitido" y
  "lo recibido" son literalmente la misma cadena. La fidelidad del campo `traza` es medible sin
  intermediarios.

### 3.4 Regla de higiene de stdout — no es cosmética, es la causa de falso negativo más probable

En stdio, **stdout es el canal JSON-RPC**. Cualquier byte que no sea protocolo lo corrompe y el
servidor aparece como caído o vacío en Claude Code — indistinguible de "el SDK no funciona".

Por eso el sample oficial redirige *todos* los logs a stderr con
`consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace`. **El Lote 2 debe copiar esa línea y
prohibirse `Console.WriteLine` en todo el PoC**; la instrumentación de métodos JSON-RPC ya prevista en
el plan escribe a **fichero**, nunca a stdout. Es la regla que convierte un fallo mudo en un fallo
diagnosticable.

---

## 4. Versión exacta del paquete a fijar

```xml
<PackageReference Include="ModelContextProtocol" Version="2.2.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="..." />
```

- **`ModelContextProtocol` 2.2.0**, versión **exacta y clavada**. Publicada el 2026-08-13. Es el paquete
  correcto de la familia: el que trae *"Stdio server, hosting/DI, and attribute-based discovery"*. No
  `ModelContextProtocol.Core` (API de bajo nivel, sin hosting ni atributos) ni
  `ModelContextProtocol.AspNetCore` (HTTP, irrelevante aquí).
- **Sin rango flotante y sin `*`.** Si durante el Lote 3 falla algo, la versión debe ser un dato fijo del
  experimento, no una variable.
- `TargetFramework`: **`net8.0`**, respaldado por los `.csproj` de los samples en el tag `v2.2.0`.
- **`Microsoft.Extensions.Hosting`**: es necesario (lo instala la propia guía oficial:
  `dotnet add package Microsoft.Extensions.Hosting`), pero **no he verificado un número de versión
  concreto**: los `.csproj` de los samples lo referencian sin atributo `Version` (lo centralizan en el
  repo del SDK). El Lote 2 debe fijar la **última 8.0.x** que resuelva para `net8.0` y **anotar en el
  README del PoC la versión que quedó**. Marcado como dato pendiente, no inventado.

---

## 5. Salvedad (b) acotada — riesgo de versión de la especificación

Era el riesgo con más capacidad de producir un **falso negativo** en el Lote 3: un desajuste de versión
de protocolo haría que el servidor no apareciese y culparíamos al SDK. **Queda acotado a residual bajo.**

### 5.1 El SDK negocia; no exige coincidencia exacta

Verificado en la referencia de API de la línea 2.x (`McpServerOptions.ProtocolVersion`, tipo `string?`) y
en el código de `McpServerImpl.cs` del tag `v2.2.0`:

- Con `ProtocolVersion` en **`null` (el valor por defecto)**, el servidor **soporta las cinco revisiones**
  `2024-11-05`, `2025-03-26`, `2025-06-18`, `2025-11-25` y `2026-07-28`.
- Para clientes que usan el handshake `initialize`, **devuelve la versión solicitada si la soporta**, y en
  caso contrario `2025-11-25`.
- El anuncio oficial de Microsoft lo dice sin ambigüedad: *"A v2 client transparently uses the legacy
  `initialize` handshake when it talks to a down-level server, and a v2 server still accepts that
  handshake from a down-level client"*, y *"Upgrading the SDK does not strand you on either side of a
  connection"*.

**Decisión derivada, y es la que evita el problema: el Lote 2 NO debe tocar `ProtocolVersion`.** Dejarlo
en `null` es lo que activa la compatibilidad con las cinco revisiones. La propia doc advierte que
*fijarlo* es lo que rompe: *"Setting it to `2026-07-28` makes the server reject `initialize` handshakes;
setting it to an earlier value makes the server reject `2026-07-28` per-request metadata"*. Es una
trampa de configuración: la forma de maximizar compatibilidad es no configurar nada.

### 5.2 Qué habla el Claude Code instalado en esta máquina

No hay `Bash` en mi conjunto de herramientas, así que **no he podido ejecutar `claude --version`**. Lo que
sí hice fue inspeccionar el binario instalado, `C:\Users\Usuario\.local\bin\claude.exe`, con búsqueda de
cadenas. Contiene **las cuatro cadenas** `2024-11-05`, `2025-06-18`, `2025-11-25` y `2026-07-28`, y además
tanto `tools/list` como `server/discover`.

- **Interpretación**: es un cliente reciente, que conoce el flujo *discovery-first* de la especificación
  `2026-07-28` y conserva las revisiones antiguas para negociar hacia abajo. El conjunto de revisiones que
  reconoce **se solapa ampliamente** con las cinco que soporta el servidor del SDK.
- **Honestidad sobre esta evidencia**: la presencia de una cadena en un binario es **circunstancial**. No
  prueba qué versión pide el cliente en la práctica, ni en qué orden. No la presento como verificación del
  comportamiento, solo como indicio consistente con la conclusión. La prueba real es el Lote 3, y la
  instrumentación de métodos JSON-RPC del Lote 2 es lo que la convertirá en dato.

### 5.3 Conclusión del riesgo

Con `ProtocolVersion = null`, para que el handshake fracasara por versión haría falta que el cliente
exigiera una revisión fuera de esas cinco. Nada de lo observado apunta a eso. **El riesgo baja de "real y
no acotado" a "residual con mitigación conocida"**, y deja de ser una razón para dudar del peldaño 1.

---

## 6. Riesgos abiertos para el Lote 3, con mitigación

Ordenados por probabilidad de causar un falso negativo, que es el fallo que más daño hace aquí: haría
retroceder el proyecto a un peldaño que no le corresponde.

| # | Riesgo | Probabilidad | Mitigación propuesta | Cómo se distingue del fallo del SDK |
|---|---|---|---|---|
| R1 | **Contaminación de stdout** (un log, un `Console.WriteLine`, un banner de .NET) corrompe el canal JSON-RPC | **Media-alta** — es el fallo clásico de stdio | Logs a stderr con `LogToStandardErrorThreshold = LogLevel.Trace` (§3.4); prohibido escribir a stdout; instrumentación a fichero | Arrancar el `.exe` a mano y comprobar que stdout está **limpio** hasta recibir la primera petición |
| R2 | **Registro mal hecho en Claude Code** (ruta, argumentos, working directory, comillas en Windows) | **Media** | README del PoC con los pasos exactos y ruta absoluta al ejecutable publicado; probar antes con el proyecto sin publicar | Es la Edge Case abierta. Si el proceso **ni siquiera arranca**, es registro; si arranca y muere en el handshake, es protocolo. El log a fichero fecha el arranque |
| R3 | **El cliente envía `initialize` con `2026-07-28`** → el servidor lo rechaza por diseño (`UnsupportedProtocolVersionException`: *"not available through the initialize handshake"*) | **Baja** — por especificación, un cliente `2026-07-28` usa `server/discover` y, si falla, cae a `initialize` con `2025-11-25`, que sí está soportada | No fijar `ProtocolVersion` (§5.1). Si aparece, es un fallo **nombrado y con arreglo conocido**, no un misterio | El log de instrumentación mostrará el método y la versión pedida. Un `UnsupportedProtocolVersionException` es inequívoco |
| R4 | **`server/discover` sobre stdio**: la doc `/v2/` no documenta el flujo discovery-first **por transporte**; lo verificado del stateless por defecto es de HTTP | **Baja** | Ninguna acción previa: ambos caminos (discover e initialize) están implementados en el servidor. La instrumentación dirá cuál se usó | Si Claude Code prueba `server/discover` y el servidor responde `MethodNotFound`, el propio cliente cae a `initialize`. Queda registrado |
| R5 | **Truncado del campo `traza`** (SC-003) | **Baja** | Retorno `string` (§3.3) + marcadores único al principio y al final de la traza, como ya prevé el plan | Un recorte con marcador inicial presente y final ausente es truncado; ausencia de los dos es fallo de transporte |
| R6 | **Versión de `Microsoft.Extensions.Hosting` no fijada** (§4) | **Baja** | Fijar la última 8.0.x que restaure y anotarla en el README | Un fallo aquí es de **restore/compilación**, ocurre antes de Claude Code y no puede confundirse con un fallo de protocolo |
| R7 | **Avisos de deprecación** `MCP9005`/`MCP9007` si alguien usa Roots/Sampling/Logging o el OAuth antiguo | **Muy baja** en este PoC | No usar esas APIs. Son **warnings, no errores**: no rompen la compilación | Irrelevante para el veredicto; anotarlo si aparece |

**Riesgo residual que ningún agente puede cerrar** (ya declarado en el plan y se reafirma aquí): que
Claude Code liste e invoque las herramientas (SC-001, SC-002, SC-003) lo confirma **el usuario**. Este
documento no adelanta ese veredicto: decide el peldaño de partida, no el resultado.

---

## 7. Qué queda desbloqueado

El Lote 1 se cierra. El Lote 2 puede arrancar con estas cinco cosas ya decididas, sin ambigüedad:

1. Peldaño **1**: se usa el SDK.
2. Paquete y versión: **`ModelContextProtocol` 2.2.0**, exacta, sobre **`net8.0`**.
3. Forma de registro: `AddMcpServer()` → `WithStdioServerTransport()` → `WithTools<T>()`.
4. Forma de declaración: `[McpServerToolType]` / `[McpServerTool]` / `[Description]`.
5. Dos reglas no negociables: **stdout limpio** (logs a stderr) y **`ProtocolVersion` sin tocar**.

---

## 8. Fuentes consultadas en esta tarea

Todas primarias u oficiales. Consultadas el 2026-08-17.

- `https://csharp.sdk.modelcontextprotocol.io/v2/concepts/getting-started.html` — snippet de servidor
  stdio de la línea 2.x
- `https://csharp.sdk.modelcontextprotocol.io/v2/concepts/tools/tools.html` — atributos, JSON Schema
  2020-12, tipos de retorno, `UseStructuredContent`, `IsError`
- `https://csharp.sdk.modelcontextprotocol.io/v2/concepts/transports/transports.html` — stdio y
  `StdioServerTransport`
- `https://csharp.sdk.modelcontextprotocol.io/v2/concepts/capabilities/capabilities.html` —
  *"the client and server negotiate a mutually supported MCP protocol version"*
- `https://csharp.sdk.modelcontextprotocol.io/v2/api/ModelContextProtocol.Server.McpServerOptions.html` —
  `ProtocolVersion`, las cinco revisiones soportadas, efecto de fijarla
- `https://api.github.com/repos/modelcontextprotocol/csharp-sdk/releases/tags/v2.0.0` — breaking changes
  de 2.0.0, alineación con la especificación `2026-07-28`
- `https://raw.githubusercontent.com/modelcontextprotocol/csharp-sdk/v2.2.0/samples/QuickstartWeatherServer/Program.cs`
  y `.../Tools/WeatherTools.cs` y `.../QuickstartWeatherServer.csproj` — API y `net8.0` en el tag exacto
- `https://raw.githubusercontent.com/modelcontextprotocol/csharp-sdk/main/samples/TestServerWithHosting/Program.cs`
  — segunda confirmación de la cadena de registro stdio
- `https://raw.githubusercontent.com/modelcontextprotocol/csharp-sdk/v2.2.0/src/ModelContextProtocol.Core/Server/McpServerImpl.cs`
  — lógica de negociación en `initialize`
- `https://devblogs.microsoft.com/dotnet/announcing-v20-of-the-official-mcp-csharp-sdk/` —
  retrocompatibilidad y continuidad de las APIs 1.x en 2.0
- Inspección local de `C:\Users\Usuario\.local\bin\claude.exe` — indicio (circunstancial, §5.2) sobre las
  revisiones de protocolo que reconoce el cliente instalado

**Descartadas como evidencia**: resultados de buscador y blogs de terceros. En esta tarea al menos un
resumen de buscador se contradecía a sí mismo sobre la negociación ("rejects mismatched initialize
protocol versions" frente a "servers continue to accept initialize requests from older clients"); se
resolvió acudiendo a `McpServerImpl.cs` y a la referencia de API. Nada de este documento depende de una
fuente secundaria.
