# PoC #1 — SDK oficial de MCP para .NET

Servidor MCP mínimo en C# / .NET 8 que se sirve por **stdio** y se registra a mano en Claude Code.

- Requisitos: `specs/001-poc-1-sdk-oficial-de-mcp-para-net/requirements.md`
- Plan: `specs/001-poc-1-sdk-oficial-de-mcp-para-net/plan.md`
- Decisión de partida (peldaño, API y versión del SDK): `DECISION-PELDANO.md` en esta misma carpeta

## Qué decide este PoC

Responde a una sola pregunta: **¿puede Claude Code hablar de verdad con un servidor MCP escrito en C#?**
De la respuesta depende **ADR-001** (proceso puente en C#/.NET en lugar de Node/TypeScript) y, en cascada,
ADR-004 (contrato compartido como código compilado) y ADR-007 (un fallo viaja dentro de una respuesta
correcta, con `ok`, `fase`, `error` y `traza`).

Lo que ya está verificado por los agentes: el proyecto compila, arranca, inicializa el transporte stdio y
responde al protocolo cuando se le envía JSON-RPC por stdin (`initialize`, `tools/list`, `tools/call`).

Lo que **no** puede verificar ningún agente y da el veredicto: que las herramientas **aparezcan y se
invoquen desde una sesión real de Claude Code** (SC-001, SC-002, SC-003). Eso lo confirma el usuario
siguiendo este README. Sin esa confirmación, el PoC no está cerrado.

El PoC es **desechable** (FR-010): vive fuera de `src/`, no se arrastra al producto, y hay que
**desregistrarlo al terminar** (sección "Cómo desregistrarlo").

### Datos fijos del experimento

| Dato | Valor |
|---|---|
| Paquete del SDK | `ModelContextProtocol` **2.2.0** (versión exacta, sin rango flotante) |
| Hosting | `Microsoft.Extensions.Hosting` **8.0.0** (dato que `DECISION-PELDANO.md` §4 dejaba pendiente de anotar aquí) |
| TargetFramework | `net8.0` |
| Transporte | stdio (`WithStdioServerTransport()`) |
| `ProtocolVersion` | **sin tocar** (`null`) — es lo que activa la negociación con las cinco revisiones soportadas |

### Herramientas que expone

| Nombre | Parámetros | Qué devuelve |
|---|---|---|
| `poc_ping` | ninguno | Texto fijo `pong - PocMcpSdk (PoC 001, ...)` |
| `poc_echo_params` | `mensaje` (string), `repeticiones` (int) | Los dos valores dentro de un texto fijo |
| `poc_error` | ninguno | JSON con `ok:false`, `fase:"runtime"`, `error` y una `traza` multilínea de ~1850 caracteres, delimitada por `===TRAZA-INICIO-POC-ERROR===` y `===TRAZA-FIN-POC-ERROR===`. Es una **respuesta correcta**, no un error de protocolo |

---

## Cómo compilarlo

Requisitos: SDK de .NET 8 instalado (`dotnet --version` debe responder). No hace falta Revit ni Node.

En PowerShell:

```powershell
cd "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net\pocs\001-poc-1-sdk-oficial-de-mcp-para-net"
dotnet build -c Release
```

El ejecutable queda en:

```
D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net\pocs\001-poc-1-sdk-oficial-de-mcp-para-net\bin\Release\net8.0\PocMcpSdk.exe
```

Esa es la ruta que se registra en Claude Code. Si compilas en Debug, la ruta equivalente es
`bin\Debug\net8.0\PocMcpSdk.exe`; funciona igual, pero usa Release para no mezclar.

> La publicación autocontenida (`dotnet publish -r win-x64 --self-contained`) es una tarea posterior del
> Lote 2 y **no** hace falta para registrar el servidor: el `.exe` de `bin\Release\net8.0\` ya arranca
> mientras haya .NET 8 en la máquina.

---

## Cómo registrarlo en Claude Code

Esta es la parte que ejecuta una persona a mano. Los comandos salen de la documentación oficial de Claude
Code consultada el **2026-08-17** (fuentes al final). No los he podido ejecutar yo: verifica cada paso con
la salida que el propio comando imprime.

### Vía A — `claude mcp add` (recomendada)

Es la vía más corta y la que documenta oficialmente Anthropic. **Ejecútala en tu terminal, no dentro de una
sesión `claude`.**

**Paso 1.** Sitúate en la raíz del proyecto. Importa: por defecto el servidor se registra en ámbito
`local`, que lo ata **al proyecto desde el que lo añades**.

```powershell
cd "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net"
```

**Paso 2.** Registra el servidor:

```powershell
claude mcp add poc-mcp-sdk -- "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net\pocs\001-poc-1-sdk-oficial-de-mcp-para-net\bin\Release\net8.0\PocMcpSdk.exe"
```

Desglose, según la doc oficial:

- `claude mcp add` registra un servidor. La sintaxis general es `claude mcp add [options] <name> -- <command> [args...]`.
- `poc-mcp-sdk` es el nombre que eliges tú. Con los comandos `claude mcp`, el nombre solo admite letras,
  números, guiones y guiones bajos.
- **No lleva `--transport`**: los servidores locales usan stdio por defecto. `claude mcp add --transport stdio poc-mcp-sdk -- "<ruta>"` es equivalente.
- El `--` separa las opciones de Claude Code del comando que arranca el servidor. Todo lo que va detrás se
  pasa al servidor tal cual. Aquí no hay argumentos: solo la ruta del `.exe`.
- La ruta es **absoluta y entre comillas**. La doc avisa de que las rutas relativas en `command`/`args` son
  una causa frecuente de que el servidor no arranque, porque se resuelven contra el directorio desde el que
  lanzaste Claude Code.

Debe imprimir algo del estilo `Added stdio MCP server poc-mcp-sdk with command: ... to local config`, seguido
de una línea `File modified:` con el fichero de configuración que ha escrito.

> La doc es explícita: `claude mcp add` funciona igual en cualquier shell, incluidos PowerShell y símbolo del
> sistema. No hace falta envolver nada en `cmd /c`: eso es un apaño para servidores que arrancan con `npx`
> (un `.cmd`), no para un ejecutable nativo. **No verificado en ejecución en esta máquina.**

**Paso 3.** Comprueba que se ha registrado y que conecta:

```powershell
claude mcp list
```

Debe aparecer `poc-mcp-sdk` con estado `✔ Connected`. En consolas antiguas de Windows 10 los glifos salen
como `√` y `×` en lugar de `✔` y `✘`.

Si el estado no es `Connected`, ve directo a "Solución de problemas". **`Added` significa que se guardó la
configuración, no que el proceso arranque.**

### Ámbito: `local` (por defecto) frente a `user`

| Ámbito | Fichero | Alcance |
|---|---|---|
| `local` (por defecto) | `%USERPROFILE%\.claude.json`, dentro de la entrada de este proyecto | Solo tú, solo este proyecto |
| `project` | `.mcp.json` en la raíz del proyecto | Todo el que clone el repositorio |
| `user` | `%USERPROFILE%\.claude.json`, bajo la clave `mcpServers` de primer nivel | Solo tú, en todos tus proyectos |

Para este PoC, **usa `local`** (no pases `--scope`): el servidor queda atado a este worktree y desaparece del
resto de proyectos por sí solo. Si prefieres poder probarlo desde cualquier directorio, añade `--scope user`
al comando del Paso 2 — pero entonces **acuérdate de borrarlo al terminar**, o se queda cargando contexto en
todas tus sesiones.

No uses `--scope project` aquí: escribiría `.mcp.json` en la raíz del repositorio, que va a control de
versiones, y este PoC es desechable.

### Vía B — `claude mcp add-json` (si la Vía A te da problemas de comillas)

Misma operación, pasando la entrada de configuración como JSON. En PowerShell, las comillas simples pasan la
cadena literal, y dentro del JSON las barras invertidas van **duplicadas**:

```powershell
claude mcp add-json poc-mcp-sdk '{"type":"stdio","command":"D:\\Arquitectura\\W_TRABAJOS\\12_IA_OPT\\2605_PROMPT_TO_REVIT\\.worktrees\\001-poc-sdk-mcp-net\\pocs\\001-poc-1-sdk-oficial-de-mcp-para-net\\bin\\Release\\net8.0\\PocMcpSdk.exe","args":[]}'
```

Verifícalo igual: `claude mcp get poc-mcp-sdk`.

### Vía C — escribir `.mcp.json` a mano (no recomendada aquí)

Es la vía documentada para compartir servidores con un equipo. Se crea `.mcp.json` en la raíz del proyecto:

```json
{
  "mcpServers": {
    "poc-mcp-sdk": {
      "type": "stdio",
      "command": "D:\\Arquitectura\\W_TRABAJOS\\12_IA_OPT\\2605_PROMPT_TO_REVIT\\.worktrees\\001-poc-sdk-mcp-net\\pocs\\001-poc-1-sdk-oficial-de-mcp-para-net\\bin\\Release\\net8.0\\PocMcpSdk.exe",
      "args": []
    }
  }
}
```

Detalles que se cobran caro si se ignoran:

- El fichero va en la **raíz del repositorio**, no dentro de `.claude/`. Claude Code no lee
  `~/.claude/mcp.json`, `~/.claude/.mcp.json`, `~/.claude/config/mcp.json` ni `%APPDATA%\Claude\mcp.json`.
- `settings.json` **no** lee una clave `mcpServers`. Ahí no funciona.
- Claude Code lee `.mcp.json` **al arrancar la sesión**: hay que salir y volver a entrar tras editarlo.
- Un servidor de ámbito `project` requiere una **aprobación única**. Si descartaste el aviso, aparece como
  `⏸ Pending approval`; se aprueba desde `/mcp`, o se reinicia el estado con `claude mcp reset-project-choices`.

Para este PoC prefiere la Vía A: menos piezas que puedan fallar y se borra con un comando.

---

## Cómo comprobar que funciona

**1. Desde la terminal**, con el servidor ya registrado:

```powershell
claude mcp list
claude mcp get poc-mcp-sdk
```

`get` muestra el comando registrado y el ámbito en el que vive. Compara la ruta que imprime con la ruta real
del `.exe`: si no coinciden, el fallo es de registro y no del servidor.

**2. Desde una sesión de Claude Code.** Arranca `claude` **en el mismo directorio** desde el que registraste
el servidor (si usaste el ámbito `local`, fuera de ahí no existe):

```powershell
cd "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net"
claude
```

Dentro de la sesión:

```
/mcp
```

Selecciona `poc-mcp-sdk`. Debes ver **las tres herramientas**:

- `poc_ping`
- `poc_echo_params`
- `poc_error`

Ese es el criterio SC-001. Si el servidor aparece conectado pero con **cero herramientas**, elige
**Reconnect** en el propio panel `/mcp`; si sigue en cero, ve a "Solución de problemas".

**3. Invócalas.** Nombrar el servidor en la petición evita que Claude conteste con otra herramienta:

| Qué pedir | Qué debe salir |
|---|---|
| `Usa el servidor poc-mcp-sdk y llama a poc_ping` | `pong - PocMcpSdk (PoC 001, SDK oficial de MCP para .NET), herramienta sin parametros` |
| `Usa poc_echo_params del servidor poc-mcp-sdk con mensaje="hola" y repeticiones=3` | `eco - PocMcpSdk (PoC 001) recibio mensaje='hola' repeticiones=3` |
| `Usa poc_echo_params con repeticiones="tres"` (viola el esquema) | Un error **diagnosticable**: hay que anotar cómo se manifiesta, no que "falla" |
| `Usa poc_error del servidor poc-mcp-sdk y muéstrame la respuesta entera` | Un JSON con `ok: false`, `fase: "runtime"` y la traza entre los dos marcadores |

En la salida, cada llamada aparece etiquetada con el nombre del servidor: así confirmas que la respuesta vino
del PoC y no del conocimiento de Claude. El nombre invocable completo de una herramienta MCP tiene la forma
`mcp__<servidor>__<herramienta>`, es decir `mcp__poc-mcp-sdk__poc_ping`.

**4. La traza (SC-003).** Lee la nota sobre doble escapado más abajo **antes** de comparar nada.

---

## Cómo desregistrarlo

Hazlo al cerrar el PoC. Cada servidor conectado ocupa espacio en la ventana de contexto de **todas** las
sesiones donde esté activo.

```powershell
cd "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net"
claude mcp remove poc-mcp-sdk
```

Debe confirmar con `Removed MCP server "poc-mcp-sdk" from local config` y una línea `File modified:`.

Si lo registraste en más de un ámbito, `remove` responde `exists in multiple scopes`. Entonces hay que indicar
cuál:

```powershell
claude mcp remove poc-mcp-sdk --scope local
claude mcp remove poc-mcp-sdk --scope user
```

Comprueba que ya no está:

```powershell
claude mcp list
```

Si usaste la Vía C, borra además el bloque `poc-mcp-sdk` de `.mcp.json` (o el fichero entero si era lo único
que contenía) y **no lo commitees**.

---

## Solución de problemas

### El servidor no aparece o no conecta: tres causas que desde fuera se ven idénticas

Es la *Edge Case* abierta del PoC (requirements.md) y volverá a aparecer en el Tier 0. Desde la sesión, un
fallo del SDK, un fallo de registro y un fallo del propio PoC producen **el mismo síntoma**: la herramienta no
está. Se separan en este orden, y cada paso descarta una causa:

**Paso 1 — ¿el ejecutable arranca? (descarta "fallo del PoC")**

Ejecuta el `.exe` directamente:

```powershell
& "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net\pocs\001-poc-1-sdk-oficial-de-mcp-para-net\bin\Release\net8.0\PocMcpSdk.exe"
```

- **Se queda esperando, sin escribir nada en pantalla**: correcto. El binario funciona; el problema está en el
  registro o en la conversación. Sal con `Ctrl+C`.
- **Devuelve un error inmediato** (falta el runtime, falta un DLL, excepción al arrancar): el fallo es del PoC
  o de la compilación, y ocurre **antes** de que Claude Code entre en juego. Recompila y vuelve a mirar.

La doc oficial recoge exactamente este criterio: si el comando arranca y espera entrada, el servidor funciona
y hay que revisar lo que muestra `claude mcp get <nombre>`; si el comando da error, el mensaje nombra lo que
falta.

Opcional, para ir más allá: con el proceso arrancado, pega esta línea y pulsa Enter. Debe responder **una
línea JSON** por stdout.

```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"manual","version":"0"}}}
```

**Paso 2 — ¿está bien registrado? (descarta "fallo de registro")**

```powershell
claude mcp get poc-mcp-sdk
```

- Si dice que no existe: el `add` no llegó a escribirse, o lo lanzaste desde otro directorio y quedó en ámbito
  `local` de otro proyecto. Vuelve a añadirlo desde la raíz correcta, o usa `--scope user`.
- Si el **comando que muestra no coincide** con la ruta real del `.exe`: es fallo de registro. Bórralo y
  vuelve a añadirlo. Si al comando le falta parte de la ruta, lo más probable es que faltase el separador
  `--` antes de la ruta.
- Si `/mcp` dice `No MCP servers configured`: casi siempre es que estás en un directorio distinto de aquel en
  el que hiciste el `add` (los servidores `local` van por proyecto), o que editaste un fichero en una ruta que
  Claude Code no lee.

**Paso 3 — ¿qué pasa en la conversación? (aísla "fallo del SDK")**

Arranca Claude Code con la depuración de MCP activada y lee el log:

```powershell
claude --debug=mcp
```

El log de la sesión, **con el stderr del servidor incluido**, queda en:

```
C:\Users\Usuario\.claude\debug\<session-id>.txt
```

Aquí es donde se ven las excepciones del servidor, porque **todos los logs del PoC van a stderr** a propósito
(`LogToStandardErrorThreshold = LogLevel.Trace` en `Program.cs`). Si el proceso arranca (Paso 1 ✓), está bien
registrado (Paso 2 ✓) y aun así muere durante el intercambio, el fallo es de protocolo o del SDK, y este log
es la evidencia que hay que anotar en el veredicto.

### "Lo arranco a mano en una consola y se cierra" — no es un fallo

Un servidor stdio vive mientras su **stdin** esté abierto: termina solo cuando quien lo lanzó cierra ese canal.
Si lo abres con doble clic, o si la consola cierra stdin al terminar de leer, el proceso sale al instante y
parece que se ha caído. **No lo es.** El comportamiento correcto arrancándolo desde una terminal interactiva es
quedarse callado esperando. Que Claude Code cierre el proceso al terminar la sesión también es lo normal.

### stdout es el canal JSON-RPC

En stdio, **stdout no es la consola: es el canal del protocolo**. Cualquier byte que no sea JSON-RPC lo
corrompe, y el resultado es un servidor que Claude Code ve como caído o vacío — indistinguible de "el SDK no
funciona". Por eso en este PoC:

- Todo el logging va a **stderr**.
- Está prohibido `Console.WriteLine` en cualquier parte del proyecto.
- Cualquier instrumentación futura escribe a **fichero**, nunca a stdout.

Si tocas el código y el servidor deja de aparecer, sospecha de esto lo primero.

### Se agota el tiempo al arrancar

El arranque tiene un límite por defecto de 30 segundos. Se sube con la variable `MCP_TIMEOUT`, en
milisegundos. En PowerShell, en la misma línea:

```powershell
$env:MCP_TIMEOUT = "60000"; claude
```

Para un `.exe` .NET local no debería hacer falta, pero la primera ejecución tras compilar puede ser lenta.

### Otros avisos útiles

- `Server already exists`: ya hay un servidor con ese nombre en ese ámbito. Bórralo o usa otro nombre.
- Servidor conectado pero **cero herramientas**: prueba **Reconnect** desde `/mcp`; si sigue en cero, lee el
  stderr en el log de `--debug=mcp`.
- Si sospechas que el problema no es el PoC sino tu configuración, `claude --safe-mode` arranca sin
  personalizaciones (incluidos los servidores MCP) y sirve para comparar.

---

## Nota sobre la traza: viaja con doble escapado

La herramienta `poc_error` devuelve un **string** que contiene un JSON. Ese string viaja, a su vez, dentro del
JSON-RPC del protocolo. Resultado: **doble escapado**. Lo que ves en la sesión no es byte a byte lo que emitió
el servidor.

Qué implica al verificar SC-003:

- Los marcadores `===TRAZA-INICIO-POC-ERROR===` y `===TRAZA-FIN-POC-ERROR===` **sí aparecen literales**: solo
  usan ASCII alfanumérico, `=`, `-` y `_`, que el serializador no escapa. Están puestos exactamente para esto.
- Otros caracteres **sí van escapados**: `<` puede llegar como `\u003C`, `'` como `\u0027`, y los saltos de
  línea como `\n` dentro del string. Las barras invertidas de las rutas Windows de la traza aparecen dobladas.
- **Comprobación correcta**: que estén **los dos marcadores**. Si está el de inicio y falta el de fin, hay
  truncado. Si faltan los dos, es fallo de transporte, no recorte.
- **Comprobación incorrecta**: comparar el texto completo carácter a carácter sin desescapar antes. Dará
  diferencias que **no** son truncado, y llevará a suspender el criterio por un motivo falso.

Si quieres la comparación exacta del campo, desescapa primero (parsea el JSON exterior y luego el interior) y
compara el resultado con el literal de `PocTools.cs`.

---

## Fuentes

Documentación oficial de Claude Code, consultada el **2026-08-17**:

- `https://code.claude.com/docs/en/mcp-quickstart` — flujo completo: `claude mcp add ... -- <comando>` para un
  servidor stdio local, `claude mcp list` y sus estados, `claude mcp remove`, tabla de ámbitos y ficheros
  (`~/.claude.json` y `.mcp.json`), nota de que `~/.claude.json` es `%USERPROFILE%\.claude.json` en Windows,
  formato de `.mcp.json`, `MCP_TIMEOUT` en PowerShell, y el criterio de diagnóstico "ejecuta el comando a mano
  y mira si espera entrada o da error".
- `https://code.claude.com/docs/en/mcp` — referencia: sintaxis
  `claude mcp add [options] <name> -- <command> [args...]`, papel del separador `--`, `claude mcp add-json`
  con ejemplo de entrada `stdio`, `claude mcp get`, ámbitos `local`/`project`/`user`, restricción de
  caracteres de los nombres de servidor, forma `mcp__<servidor>__<herramienta>`.
- `https://code.claude.com/docs/en/debug-your-config` — `/mcp`, `claude --debug=mcp`, log con el stderr del
  servidor en `~/.claude/debug/<session-id>.txt`, rutas relativas en `command`/`args` como causa frecuente de
  fallo, `.mcp.json` en la raíz y no bajo `.claude/`, `settings.json` no lee `mcpServers`.
- `https://code.claude.com/docs/en/troubleshooting` — `/doctor`, `claude doctor`, `--safe-mode`.

Lo **no verificado**, dicho claramente:

- No he ejecutado ninguno de estos comandos en esta máquina: no dispongo de shell. Están transcritos de la
  documentación oficial y adaptados a las rutas reales de este worktree.
- Que un ejecutable **nativo** de Windows no necesite envoltorio `cmd /c` es lo que se deduce de la
  documentación (el envoltorio aparece asociado a `npx`, que es un `.cmd`), pero la doc oficial no lo afirma
  para ejecutables nativos. Si el registro fallase de forma inexplicable, probar
  `claude mcp add poc-mcp-sdk -- cmd /c "<ruta al exe>"` es un experimento legítimo, no una recomendación.

---

## Instrumentación: qué métodos JSON-RPC invoca Claude Code de verdad

Este PoC registra en un fichero de texto, aparte, qué métodos del protocolo atiende el servidor y
con qué frecuencia (`RpcMethodLoggerProvider.cs`). No es parte del experimento en sí: es una
instrumentación barata para que el Lote 2 sepa, con datos reales y no a ciegas, qué subconjunto de
métodos habría que implementar a mano si algún día el SDK oficial dejara de servir (plan B).

La ruta es fija y queda fuera de `bin/`, así que sobrevive a recompilar:

```
%LOCALAPPDATA%\PocMcpSdk\rpc-methods.log
```

Es decir, en esta máquina: `C:\Users\Usuario\AppData\Local\PocMcpSdk\rpc-methods.log`.

Cómo leerlo:

- El fichero **conserva entre sesiones** todas las líneas de llamada ya escritas (nunca se borra al
  arrancar), pero dentro de una misma sesión el bloque de recuento de detrás se trunca y se
  reescribe en cada llamada — no depende de que el servidor cierre limpio.
- Cada línea de una llamada tiene el formato `<timestamp ISO 8601>\t<método>` (una línea por
  invocación real, p. ej. `initialize`, `tools/list`, `tools/call`).
- Después de cada llamada se reescribe, al final del fichero, un bloque de recuento **de la sesión
  en curso del servidor** (no del fichero completo: las líneas de llamada de arriba pueden venir de
  sesiones anteriores) con el formato `<timestamp>\t<método>\t<nº de llamadas>`, ordenado de más a
  menos frecuente, precedido de una línea `--- recuento de ESTA sesión del servidor (no del fichero
  completo; las líneas de arriba pueden ser de sesiones anteriores) ---`. Si además el servidor llega
  a cerrarse limpio, ese mismo bloque se reescribe una última vez con el título `--- recuento de ESTA
  sesión del servidor, al cerrar (no del fichero completo; las líneas de arriba pueden ser de
  sesiones anteriores) ---`. En ambos casos el bloque final del fichero cuenta solo lo que ejercitó
  esa sesión, no el histórico acumulado en el fichero.
- Si el fichero no existe o está vacío tras probar el PoC, revisa stderr del proceso: cualquier
  fallo de la instrumentación (permisos de `%LOCALAPPDATA%`, fichero bloqueado, etc.) se reporta ahí
  con el prefijo `[PocMcpSdk] RpcMethodLoggerProvider`; nunca se traga en silencio y nunca escribe
  en stdout (es el canal JSON-RPC).

No toca `PocTools.cs` ni el transporte: escucha los mensajes que el propio SDK ya emite por su
`ILogger` en la categoría `ModelContextProtocol.Server.McpServer` (los mismos que se ven por
stderr), filtra el patrón `method '<nombre>' request handler called.` y cuenta una vez por
invocación real (el segundo mensaje del SDK, el de "completed", no se cuenta aparte para no
duplicar la frecuencia).
