# Guion de verificación — PoC #1 SDK oficial de MCP para .NET

Checklist para ejecutar a mano, en orden, dentro de una sesión real de Claude Code. Es el único
camino por el que SC-001, SC-002 y SC-003 pueden darse por cumplidos: ningún agente puede abrir esa
sesión. Rellena cada `Observado:` con lo que veas literalmente (pega texto/capturas si hace falta);
no marques nada como OK de memoria.

Lo que ya está verificado a nivel de protocolo (compilación, `initialize`, `tools/list`, `tools/call`,
esquema, traza) está en `README.md` y no se repite aquí. Este guion cubre solo lo que falta: que
**Claude Code, como cliente real**, lo haga igual de bien.

---

## 0. Preparación

**P1 — Arranque en frío antes de registrar.** El `.exe` autocontenido (73,6 MB) tarda más en la
primera ejecución que en las siguientes. Evita que ese arranque en frío se lo coma el timeout de
Claude Code.

```powershell
& "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net\pocs\001-poc-1-sdk-oficial-de-mcp-para-net\bin\Release\net8.0\win-x64\publish\PocMcpSdk.exe"
```

Resultado esperado (medido, esto es literalmente lo que verás): unas ocho líneas de log que empiezan
por `info:`, entre ellas `transport reading messages`, `Application started. Press Ctrl+C to shut
down.` y `Content root path: ...`. **Eso es correcto y es la señal de que arrancó bien**: todos los
logs del servidor van a stderr, nunca a stdout, porque stdout es el canal JSON-RPC. Después el
proceso se queda esperando entrada. Sal con `Ctrl+C`.

Si en vez de eso da un error inmediato o una excepción, no sigas: es un fallo del PoC, no de Claude
Code.

`Observado:` Funciona exactamente como se describe - USUARIO LO HA COMPROBADO

**P2 — Registrar el servidor** (ejecútalo desde la raíz del worktree, en tu terminal, no dentro de
una sesión `claude`):

```powershell
cd "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net"
claude mcp add poc-mcp-sdk -- "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net\pocs\001-poc-1-sdk-oficial-de-mcp-para-net\bin\Release\net8.0\win-x64\publish\PocMcpSdk.exe"
```

Resultado esperado: línea `Added stdio MCP server poc-mcp-sdk with command: ...` + una línea
`File modified:`.

`Observado:` Added stdio MCP server poc-mcp-sdk with command: D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net\pocs\001-poc-1-sdk-oficial-de-mcp-para-net\bin\Release\net8.0\win-x64\publish\PocMcpSdk.exe  to local config
File modified: C:\Users\Usuario\.claude.json [project: D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net] - USUARIO LO HA COMPROBADO

**P3 — Confirmar el registro:**

```powershell
claude mcp list
```

Resultado esperado: `poc-mcp-sdk` con estado `✔ Connected` (o `√ Connected` en consolas antiguas).

`Observado:` PS D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net> claude mcp list
claude.ai Google Calendar: https://calendarmcp.googleapis.com/mcp/v1 - √ Connected
claude.ai Google Drive: https://drivemcp.googleapis.com/mcp/v1 - √ Connected
plugin:context7:context7: https://mcp.context7.com/mcp (HTTP) - √ Connected
plugin:github:github: https://api.githubcopilot.com/mcp/ (HTTP) - × Failed to connect — HTTP 400: Error POSTing to
endpoint: bad request: Authorization header is badly formatted
poc-mcp-sdk: D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net\pocs\001-poc-1-sdk
-oficial-de-mcp-para-net\bin\Release\net8.0\win-x64\publish\PocMcpSdk.exe  - √ Connected
 - USUARIO LO HA COMPROBADO
Si P3 no da `Connected`, no continúes con las comprobaciones: ve directo a la sección "Triaje" (§4).

---

## 1. SC-001 — las herramientas aparecen con su esquema

Abre la sesión desde el mismo directorio donde hiciste `claude mcp add` (ámbito `local`):

```powershell
cd "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net"
claude
```

**C1.1 — Panel `/mcp`.** Dentro de la sesión, escribe `/mcp` y selecciona `poc-mcp-sdk`.

Resultado esperado: aparecen exactamente tres herramientas: `poc_ping`, `poc_echo_params`,
`poc_error`.

`Observado:` Así es, están las 3 herramientas
**C1.2 — El esquema, no solo el nombre.** En la misma sesión, pide textualmente:

```
Usa el servidor poc-mcp-sdk y dime qué parámetros tiene la herramienta poc_echo_params, con su
nombre, su tipo y si son obligatorios, tal y como los ves en su definición.
```

Resultado esperado: Claude describe `mensaje` como string y `repeticiones` como integer, ambos
obligatorios (`required`), sin inventar parámetros que no existan ni omitir el tipo. Que los
nombre bien de memoria no vale: tiene que decir que los ve en la herramienta del servidor.

`Observado:`Perfecto. La herramienta poc_echo_params del servidor poc-mcp-sdk tiene estos parámetros:

┌──────────────┬─────────┬─────────────┬────────────────────────────────────────────────────┐
│  Parámetro   │  Tipo   │ Obligatorio │                    Descripción                     │
├──────────────┼─────────┼─────────────┼────────────────────────────────────────────────────┤
│ mensaje      │ string  │ Sí          │ Texto de prueba a incluir en la respuesta.         │
├──────────────┼─────────┼─────────────┼────────────────────────────────────────────────────┤
│ repeticiones │ integer │ Sí          │ Número entero de prueba a incluir en la respuesta. │
└──────────────┴─────────┴─────────────┴────────────────────────────────────────────────────┘

Ambos parámetros son obligatorios.

Si C1.1 muestra el servidor conectado pero **cero herramientas**: pulsa **Reconnect** en el propio
panel `/mcp` y repite C1.1 una vez. Si sigue en cero, ve a "Triaje" (§4) antes de continuar.

---

## 2. SC-002 — invocación con los tres casos de parámetros

**C2.1 — Sin parámetros (`poc_ping`).** Pide:

```
Usa el servidor poc-mcp-sdk y llama a poc_ping.
```

Resultado esperado: texto `pong - PocMcpSdk (PoC 001, SDK oficial de MCP para .NET), herramienta
sin parametros`, etiquetado como llamada a `poc-mcp-sdk` (no como conocimiento general de Claude).

`Observado:` ✓ poc_ping respondió correctamente:

pong - PocMcpSdk (PoC 001, SDK oficial de MCP para .NET), herramienta sin parametros

El servidor MCP poc-mcp-sdk está activo y la herramienta sin parámetros funciona.
**C2.2 — Parámetros válidos (`poc_echo_params`).** Pide:

```
Usa poc_echo_params del servidor poc-mcp-sdk con mensaje="hola" y repeticiones=3.
```

Resultado esperado: el texto de respuesta contiene **los valores exactos enviados**:
`mensaje='hola'` y `repeticiones=3`. Si aparece otro texto o números distintos, es un fallo: anota
literalmente qué devolvió.

`Observado:` ✓ poc_echo_params respondió correctamente:

eco - PocMcpSdk (PoC 001) recibio mensaje='hola' repeticiones=3

Los parámetros se transmitieron correctamente al servidor MCP poc-mcp-sdk y fueron recibidos sin problemas.

**C2.3 — Parámetros que violan el esquema.** Pide:

```
Usa poc_echo_params del servidor poc-mcp-sdk con repeticiones="tres" (una cadena, no un número).
```

**Qué se considera aprobado aquí (léelo antes de juzgar el resultado):** lo relevante NO es que el
servidor se caiga, ni que Claude "lo arregle" convirtiendo el valor por su cuenta sin decirlo. Se
aprueba si ocurre **cualquiera** de estas dos cosas, de forma legible para un humano:

- Claude (o el cliente MCP) rechaza la llamada por incumplir el esquema y te lo dice, o
- la llamada se hace y el servidor/protocolo devuelve un error diagnosticable que Claude te muestra
  o resume (no un cuelgue silencioso, no una respuesta vacía sin explicación).

Anota **cómo se manifestó exactamente** (mensaje de error, quién lo generó, si Claude reintentó por
su cuenta convirtiendo el tipo). No se aprueba si el proceso se queda colgado, si Claude Code
desconecta el servidor sin explicación, o si no hay ningún rastro de que algo falló.

`Observado:` ✗ Error esperado: El parámetro repeticiones espera un tipo integer, pero se envió una cadena "tres".

El servidor MCP rechazó la llamada porque el esquema de la herramienta requiere que repeticiones sea un número entero, no una cadena. Esto demuestra que el servidor valida los tipos de parámetros según su definición de esquema — no acepta cadenas donde espera números.

---

## 3. SC-003 — la traza de `poc_error` llega íntegra

**C3 — Pide la respuesta completa:**

```
Usa poc_error del servidor poc-mcp-sdk y muéstrame la respuesta entera, sin resumir.
```

**Antes de juzgar el resultado, lee esto (doble escapado):** la traza viaja como un string JSON
dentro del JSON-RPC del protocolo. Los marcadores son ASCII simple y **sí llegan literales**, pero
otros caracteres de la traza van escapados (`<` como `\u003C`, `'` como `\u0027`, saltos de línea
como `\n`). **No compares el texto entero carácter a carácter**: eso da diferencias que no son
truncado y te hace suspender el criterio por un motivo falso. La comprobación correcta es buscar
los dos marcadores.

Resultado esperado, comprobado como dos hechos independientes:

- Marcador de inicio presente en el texto devuelto: `===TRAZA-INICIO-POC-ERROR===`
  `Observado (sí/no, y si no, qué apareció en su lugar):` ______________________________________________

- Marcador de fin presente en el texto devuelto: `===TRAZA-FIN-POC-ERROR===`
  `Observado (sí/no, y si no, qué apareció en su lugar):` ______________________________________________

Criterio: **los dos marcadores presentes** = traza íntegra, SC-003 cumplido. Solo el de inicio y
falta el de fin = truncado (fallo real, anótalo con el punto exacto donde se corta). Faltan los dos
= fallo de transporte, no de recorte (distíntalo en la nota).

También debe verse `"ok": false` y `"fase": "runtime"` en el JSON de respuesta; anota si falta
alguno de los dos.

`Observado (ok/fase presentes):` Called poc-mcp-sdk

{
  "ok": false,
  "fase": "runtime",
  "resultado": null,
  "ids_creados": [],
  "error": "InvalidOperationException: No se puede completar la operacion. Revit devolvio el siguiente mensaje: 'No se pueden crear dos niveles con la misma cota.'",
  "traza": "===TRAZA-INICIO-POC-ERROR===\nAutodesk.Revit.Exceptions.InvalidOperationException: No se puede completar la operacion. Revit devolvio el siguiente mensaje: 'No se pueden crear dos niveles con la misma cota.'\n   at Autodesk.Revit.DB.Level.Create(Document document, Double elevation)\n   at Autodesk.Revit.DB.Level.Create(Document document, Double elevation)\n   at Script.Execute(UIApplication uiapp) en D:\\RevitBridge\\snippets\\generated_7f3a9c.cs:linea 27\n   at RevitBridge.Core.RoslynExecutor.<ExecuteSnippetAsync>d__12.MoveNext() en D:\\RevitBridge\\src\\Core\\RoslynExecutor.cs:linea 118\n   at RevitBridge.Core.ExternalEventHandler.Execute(UIApplication app) en D:\\RevitBridge\\src\\Core\\ExternalEventHandler.cs:linea 64\n   at Autodesk.Revit.UI.ExternalEvent.Raise()\n --->System.ArgumentException: El valor no se encuentra dentro del intervalo esperado.\n   Nombre del parametro: elevation\n   at Autodesk.Revit.DB.LevelUtils.ValidateElevation(Double elevacionEnPies)\n   at Autodesk.Revit.DB.Level.Create(Document document, Double elevation)\n   --- Fin del seguimiento de la pila de la excepcion interna ---\n   at Autodesk.Revit.DB.Level.Create(Document document, Double elevation)\n   at Script.Execute(UIApplication uiapp) en D:\\RevitBridge\\snippets\\generated_7f3a9c.cs:linea 27\nInnerException: System.ArgumentException: El valor no se encuentra dentro del intervalo esperado.\n   Nombre del parametro: elevation\n   at Autodesk.Revit.DB.LevelUtils.ValidateElevation(Double elevacionEnPies)\n   at Autodesk.Revit.DB.Level.Create(Document document, Double elevation)\n   at Script.Execute(UIApplication uiapp) en D:\\RevitBridge\\snippets\\generated_7f3a9c.cs:linea 27\n   at RevitBridge.Core.RoslynExecutor.<ExecuteSnippetAsync>d__12.MoveNext() en D:\\RevitBridge\\src\\Core\\RoslynExecutor.cs:linea 118\n===TRAZA-FIN-POC-ERROR===",
  "duracion_ms": 340
}
---

## 4. Recogida del fichero de instrumentación

Este fichero es un entregable del PoC: dice qué métodos del protocolo usa Claude Code de verdad
(dimensiona el plan B si el SDK oficial dejara de servir). Ábrelo tras completar las secciones 1-3:

```powershell
notepad "$env:LOCALAPPDATA\PocMcpSdk\rpc-methods.log"
```

(ruta completa en esta máquina: `C:\Users\Usuario\AppData\Local\PocMcpSdk\rpc-methods.log`)

Resultado esperado: el fichero existe y contiene líneas `<timestamp>\t<método>` (mínimo
`initialize`, `tools/list`, `tools/call` varias veces) más, al final, un bloque de recuento de la
sesión en curso del servidor.

**Pega aquí el contenido completo del fichero** (o al menos el bloque de recuento final):

```
Observado:
2026-08-17T16:17:10.8546107+02:00	tools/list
2026-08-17T16:17:10.8554246+02:00	tools/call
2026-08-17T16:17:10.8546133+02:00	tools/call
2026-08-17T16:17:10.8546075+02:00	initialize
2026-08-17T16:20:43.4981393+02:00	tools/list
2026-08-17T16:20:43.4986781+02:00	tools/call
2026-08-17T16:20:43.4981373+02:00	initialize
2026-08-17T16:20:43.4981359+02:00	tools/call
2026-08-17T16:20:43.5370416+02:00	--- recuento acumulado (hasta la última llamada registrada) ---
2026-08-17T16:20:43.5370416+02:00	tools/call	2
2026-08-17T16:20:43.5370416+02:00	tools/list	1
2026-08-17T16:20:43.5370416+02:00	initialize	1
2026-08-17T16:22:05.1433173+02:00	initialize
2026-08-17T16:22:05.1433189+02:00	tools/list
2026-08-17T16:22:05.1614875+02:00	--- recuento acumulado (hasta la última llamada registrada) ---
2026-08-17T16:22:05.1614875+02:00	tools/list	1
2026-08-17T16:22:05.1614875+02:00	initialize	1
2026-08-17T16:22:19.2048359+02:00	initialize
2026-08-17T16:22:19.2078992+02:00	--- recuento acumulado (hasta la última llamada registrada) ---
2026-08-17T16:22:19.2078992+02:00	initialize	1
2026-08-17T16:22:51.2288353+02:00	tools/call
2026-08-17T16:22:51.2288310+02:00	tools/list
2026-08-17T16:22:51.2288415+02:00	initialize
2026-08-17T16:22:51.2589964+02:00	--- recuento acumulado (hasta la última llamada registrada) ---
2026-08-17T16:22:51.2589964+02:00	tools/list	1
2026-08-17T16:22:51.2589964+02:00	initialize	1
2026-08-17T16:22:51.2589964+02:00	tools/call	1
2026-08-17T16:24:57.9791708+02:00	tools/list
2026-08-17T16:24:57.9791716+02:00	initialize
2026-08-17T16:24:57.9970153+02:00	--- recuento de ESTA sesión del servidor (no del fichero completo; las líneas de arriba pueden ser de sesiones anteriores) ---
2026-08-17T16:24:57.9970153+02:00	tools/list	1
2026-08-17T16:24:57.9970153+02:00	initialize	1
2026-08-17T16:34:28.9830978+02:00	server/discover
2026-08-17T16:34:28.9862657+02:00	--- recuento de ESTA sesión del servidor (no del fichero completo; las líneas de arriba pueden ser de sesiones anteriores) ---
2026-08-17T16:34:28.9862657+02:00	server/discover	1
2026-08-17T16:34:29.3223496+02:00	subscriptions/listen
2026-08-17T16:34:29.3424115+02:00	tools/list
2026-08-17T16:34:29.3427649+02:00	--- recuento de ESTA sesión del servidor (no del fichero completo; las líneas de arriba pueden ser de sesiones anteriores) ---
2026-08-17T16:34:29.3427649+02:00	tools/list	1
2026-08-17T16:34:29.3427649+02:00	subscriptions/listen	1
2026-08-17T16:35:11.5655847+02:00	server/discover
2026-08-17T16:35:11.5689822+02:00	--- recuento de ESTA sesión del servidor (no del fichero completo; las líneas de arriba pueden ser de sesiones anteriores) ---
2026-08-17T16:35:11.5689822+02:00	server/discover	1
2026-08-17T16:35:12.2086870+02:00	subscriptions/listen
2026-08-17T16:35:12.2302002+02:00	tools/list
2026-08-17T16:35:12.2306159+02:00	--- recuento de ESTA sesión del servidor (no del fichero completo; las líneas de arriba pueden ser de sesiones anteriores) ---
2026-08-17T16:35:12.2306159+02:00	tools/list	1
2026-08-17T16:35:12.2306159+02:00	subscriptions/listen	1
2026-08-17T16:36:30.7972898+02:00	server/discover
2026-08-17T16:36:30.8003614+02:00	--- recuento de ESTA sesión del servidor (no del fichero completo; las líneas de arriba pueden ser de sesiones anteriores) ---
2026-08-17T16:36:30.8003614+02:00	server/discover	1
2026-08-17T16:36:31.1325816+02:00	subscriptions/listen
2026-08-17T16:36:31.1512050+02:00	tools/list
2026-08-17T16:37:55.5560709+02:00	tools/call
2026-08-17T16:38:32.8950351+02:00	tools/call
2026-08-17T16:39:04.9755123+02:00	tools/call
2026-08-17T16:39:31.8814453+02:00	tools/call
2026-08-17T16:39:31.8817452+02:00	--- recuento de ESTA sesión del servidor (no del fichero completo; las líneas de arriba pueden ser de sesiones anteriores) ---
2026-08-17T16:39:31.8817452+02:00	tools/call	4
2026-08-17T16:39:31.8817452+02:00	tools/list	1
2026-08-17T16:39:31.8817452+02:00	subscriptions/listen	1


```

Si el fichero no existe o está vacío, no lo des como fallo del PoC sin más: revisa stderr del
proceso (prefijo `[PocMcpSdk] RpcMethodLoggerProvider`) vía `claude --debug=mcp` (§5, Paso 3).

---

## 5. Triaje de fallos (solo si algo de arriba no salió como se esperaba)

Un fallo del SDK, un fallo de registro y un fallo del propio PoC **se ven idénticos desde fuera**
(la herramienta simplemente "no está" o "no responde"). Sigue los tres pasos en este orden — cada
uno descarta una causa — y anota **por cuál se resolvió** cada fallo que hayas anotado arriba; esa
distinción se reutiliza en el Tier 0.

**Paso 1 — ¿arranca el ejecutable?** (descarta fallo del PoC)

```powershell
& "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net\pocs\001-poc-1-sdk-oficial-de-mcp-para-net\bin\Release\net8.0\win-x64\publish\PocMcpSdk.exe"
```

Escribe sus líneas `info:` (eso es stderr, es correcto) y se queda esperando entrada = el binario
funciona, el problema no es este. Da un error inmediato o una excepción = fallo del PoC, para aquí.

`Observado / conclusión:` ______________________________________________

**Paso 2 — ¿está bien registrado?** (descarta fallo de registro)

```powershell
claude mcp get poc-mcp-sdk
claude mcp list
```

Compara la ruta que muestra `get` con la ruta real del `.exe`. Si no existe, o la ruta no coincide,
o `list` no lo muestra: fallo de registro. Bórralo (`claude mcp remove poc-mcp-sdk`) y repite P2.

`Observado / conclusión:` ______________________________________________

**Paso 3 — ¿qué pasa en la conversación?** (aísla fallo del SDK/protocolo)

```powershell
claude --debug=mcp
```

El log de esa sesión, con el **stderr del servidor incluido** (todos los logs del PoC van a
stderr, nunca a stdout), queda en:

```
C:\Users\Usuario\.claude\debug\<session-id>.txt
```

Si el Paso 1 y el Paso 2 salieron bien y aun así algo falla en la conversación, la evidencia de por
qué está en ese fichero. Pega aquí lo relevante (excepciones, stack traces):

`Observado:` ______________________________________________

---

## 6. Limpieza — desregistrar el servidor

El PoC es desechable: no debe quedar colgado ocupando contexto en sesiones futuras.

```powershell
cd "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net"
claude mcp remove poc-mcp-sdk
claude mcp list
```

Resultado esperado: `Removed MCP server "poc-mcp-sdk" from local config` y, en el `list` posterior,
que `poc-mcp-sdk` ya no aparece.

`Observado:` PS C:\Users\Usuario> cd "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net"
>> claude mcp remove poc-mcp-sdk
>> claude mcp list
Removed MCP server "poc-mcp-sdk" from local config
File modified: C:\Users\Usuario\.claude.json [project:
D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net]
claude.ai Google Calendar: https://calendarmcp.googleapis.com/mcp/v1 - √ Connected
claude.ai Google Drive: https://drivemcp.googleapis.com/mcp/v1 - √ Connected
plugin:context7:context7: https://mcp.context7.com/mcp (HTTP) - √ Connected
plugin:github:github: https://api.githubcopilot.com/mcp/ (HTTP) - × Failed to connect — HTTP 400: Error POSTing to
endpoint: bad request: Authorization header is badly formatted


Si `remove` responde `exists in multiple scopes`, repite con `--scope local` y `--scope user` por
separado y confirma cada uno con `claude mcp list`.

---

## Resumen de qué cubre cada comprobación

| Comprobación | Criterio |
|---|---|
| C1.1, C1.2 | SC-001 — herramientas visibles con su esquema tipado |
| C2.1 | SC-002 — caso sin parámetros |
| C2.2 | SC-002 — caso con parámetros válidos |
| C2.3 | SC-002 — caso con parámetros que violan el esquema |
| C3 (dos marcadores) | SC-003 — traza íntegra |
| §4 | Entregable de instrumentación (no es un SC, pero es obligatorio) |
| §5 | Triaje — solo si algo de lo anterior falló |
| §6 | Cierre obligatorio del PoC |
