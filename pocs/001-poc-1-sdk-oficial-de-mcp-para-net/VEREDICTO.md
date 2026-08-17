# Veredicto — PoC #1: SDK oficial de MCP para .NET

PoC: `001-poc-1-sdk-oficial-de-mcp-para-net` · Lote 4, tarea 1 (Escribir el veredicto)
Entradas: `DECISION-PELDANO.md` (Lote 1), `RECONOCIMIENTO.md` (Lote 1), `GUION-VERIFICACION.md` (Lote 3,
verificación ejecutada y anotada por el usuario), `%LOCALAPPDATA%\PocMcpSdk\rpc-methods.log`
(instrumentación, volcada en `GUION-VERIFICACION.md` §4).
Fecha: 2026-08-17

---

## 1. Resultado — PELDAÑO 1. ADR-001 confirmado.

`ModelContextProtocol` **2.2.0** (estable) sobre `Microsoft.Extensions.Hosting` **8.0.0**. El proceso
puente se escribe en C# con el SDK oficial. No se activa el peldaño 2 (implementación propia) ni el
peldaño 3 (Node/TypeScript). **ADR-004 no queda afectado**: el contrato compartido en `RevitBridge.Core`
sigue siendo código compilado, no un esquema duplicado.

## 2. Criterios, uno por uno

| Criterio | Resultado | Evidencia |
|---|---|---|
| SC-001 — herramientas visibles con esquema tipado | **Cumplido** | `GUION-VERIFICACION.md` C1.1 (las 3 herramientas: `poc_ping`, `poc_echo_params`, `poc_error` — el PoC declaró una tercera además de las dos mínimas de FR-001) y C1.2 (Claude describe `mensaje: string` y `repeticiones: integer`, ambos `required`, leídos de la definición del servidor, no inventados) |
| SC-002 — los tres casos sin error de protocolo | **Cumplido** | C2.1 sin parámetros (`poc_ping`, respuesta fija correcta); C2.2 con parámetros válidos (eco exacto de `mensaje='hola'` `repeticiones=3`); C2.3 con parámetro que viola el esquema (rechazado con error diagnosticable — ver 2.3 más abajo) |
| SC-003 — traza íntegra por presencia de los dos marcadores | **Cumplido, con el criterio revisado** — ver nota abajo | C3: `===TRAZA-INICIO-POC-ERROR===` y `===TRAZA-FIN-POC-ERROR===` presentes los dos en el JSON pegado por el usuario en `GUION-VERIFICACION.md:201-208`, con `"ok": false` y `"fase": "runtime"` también presentes |
| Publicación autocontenida y arranque en máquina de dev (criterio 4 del roadmap / FR-005 / SC-004) | **Cumplido** | Lote 2 (`dotnet publish -c Release -r win-x64 --self-contained`, sin error) + `GUION-VERIFICACION.md` P1 (el arranque en frío del `.exe` se comportó como documenta el guion, confirmado en bloque por el usuario: "Funciona exactamente como se describe") |

**Nota sobre SC-003, añadida en la revisión de `@judge`:** el criterio tal y como sigue escrito en
`requirements.md` (SC-003 y el Acceptance Scenario 2 de la Historia 2) pide **comparación exacta,
carácter por carácter**, del campo `traza`. Esa comparación **no se ejecutó** y no podía ejecutarse con
la evidencia disponible: la traza viaja como string JSON dentro del JSON-RPC y sufre doble escapado
(`<` → `<`, saltos de línea → `\n`), que `GUION-VERIFICACION.md` §3 y `README.md` ya advertían que
invalida la comparación literal. El criterio realmente aplicado — y el único que la evidencia soporta —
es la presencia de los dos marcadores de inicio y fin, que prueba ausencia de truncado en los extremos
pero **no** integridad byte a byte del interior. De hecho, comparando el JSON pegado
(`GUION-VERIFICACION.md:207`) contra el string que emite `PocTools.cs`, hay una diferencia de un
carácter (un espacio de más tras `--->`) casi con toda seguridad un artefacto de copiado del terminal, no
un truncado real — el resto de las 21 líneas de la traza, ambos marcadores, `ok`, `fase` y `duracion_ms`
coinciden literalmente —, pero es la prueba de que la captura no es fiel byte a byte y de que "cumplido
por comparación exacta" habría sido una afirmación no respaldada. `requirements.md` se enmienda en el
mismo commit que este documento para que el spec deje de exigir un criterio que no se verificó.

### 2.3 — Detalle de C2.3, porque el criterio de aceptación era doble

`requirements.md` acepta C2.3 por dos vías: rechazo por el cliente, o error diagnosticable que Claude
muestre. Lo observado fue la segunda: *"El parámetro repeticiones espera un tipo integer, pero se envió
una cadena 'tres'... El servidor MCP rechazó la llamada porque el esquema de la herramienta requiere que
repeticiones sea un número entero"*. Diagnosticable, legible, sin cuelgue ni respuesta muda. Cumple.
El guion pedía además anotar si Claude reintentó por su cuenta convirtiendo el tipo; el usuario no dejó
esa anotación explícita, pero el log de instrumentación corrobora que la llamada inválida llegó
efectivamente al servidor: la sesión 16:37–16:39 registra **4** `tools/call` (`GUION-VERIFICACION.md:284-287`),
uno por cada comprobación C2.1/C2.2/C2.3/C3 de esa tanda — no hubo rechazo silencioso antes de protocolo.

## 3. Instrumentación — hallazgo que no cambia el veredicto pero sí una expectativa

El log de métodos JSON-RPC (`GUION-VERIFICACION.md` §4) registra, a lo largo de varias sesiones de
Claude Code contra el mismo servidor:

| Método | Veces observado | ¿Estándar MCP? |
|---|---|---|
| `initialize` | 6 | sí, obligatorio por especificación |
| `tools/list` | 8 | sí |
| `tools/call` | 9 | sí |
| `server/discover` | 3 | no — extensión propia del cliente |
| `subscriptions/listen` | 3 | no — extensión propia del cliente |

Recuento hecho contando cada línea de invocación (`<timestamp>\t<método>`) del fichero completo
pegado en `GUION-VERIFICACION.md` §4, sin usar los bloques `--- recuento ---` intermedios: esos bloques
resumen solo la sesión de servidor en curso en cada momento y se solapan entre sí, así que sumarlos
cuenta de más o de menos según el método. La primera versión de esta tabla usó ese atajo para
`tools/call` (7, por suma de bloques) mezclado con el conteo de líneas para los demás métodos — error
detectado y corregido en la revisión de `@judge`.

**Corrige una nota del relevo anterior (`ESTADO.md`, escrito antes de que se pegara este log
completo):** ese fichero afirmaba que `initialize` se había visto **0 veces** y lo dejaba como pregunta
abierta. El log completo, ya disponible en el commit `cf8cd25` seis minutos antes de escribirse
`ESTADO.md`, muestra `initialize` **6 veces**, siempre en las primeras sesiones cronológicamente
(16:17–16:24). A partir de las 16:34, las sesiones nuevas empiezan por `server/discover` +
`subscriptions/listen` en vez de `initialize`. **No es un misterio nuevo: es exactamente el riesgo R4
que ya anticipó `DECISION-PELDANO.md` §6** — el flujo *discovery-first* de la especificación
`2026-07-28` no está documentado por transporte en la doc `/v2/`, pero el servidor implementa ambos
caminos y los dos funcionan (las herramientas se listan y se invocan igual en ambos casos, según
C1.1–C2.3). La pregunta abierta de `ESTADO.md` queda cerrada: no hay ausencia de `initialize`, hay dos
caminos de negociación que el cliente alterna, y ninguno de los dos rompe nada verificado.

Consecuencia real, la misma que ya preveía `DECISION-PELDANO.md`: si algún día se necesitara implementar
MCP a mano (peldaño 2), habría que soportar `server/discover` y `subscriptions/listen` además de
`initialize` + `tools/list` + `tools/call`, porque Claude Code los usa de verdad y no están en ninguna
especificación pública. Razón más para preferir el SDK, que ya los resuelve sin que el autor tenga que
perseguirlos.

## 4. Riesgos de `DECISION-PELDANO.md` §6, resueltos por la verificación

| # | Riesgo | Estado tras el Lote 3 |
|---|---|---|
| R1 | Contaminación de stdout | No se manifestó — el servidor respondió correctamente en todas las sesiones |
| R2 | Registro mal hecho en Claude Code | No se manifestó — `claude mcp add` / `claude mcp list` funcionaron a la primera (P2, P3) |
| R3 | Rechazo del handshake por versión de protocolo fijada | No se manifestó — `ProtocolVersion` se dejó en `null` como prescribía la mitigación |
| R4 | `server/discover` sobre stdio, sin documentar por transporte | Se manifestó (§3 de este documento) y no causó fallo: ambos caminos funcionan |
| R5 | Truncado del campo `traza` | No se manifestó — los dos marcadores llegaron íntegros |
| R6 | Versión de `Microsoft.Extensions.Hosting` sin fijar | Resuelto en Lote 2: `8.0.0`, anotado en `PocMcpSdk.csproj` |
| R7 | Avisos de deprecación `MCP9005`/`MCP9007` | No aplica — el PoC no usa Roots/Sampling/Logging ni OAuth |

## 5. FR-008 / FR-009

- **FR-008** (dejar el veredicto por escrito y marcar el Discovery bloqueante como resuelto): este
  documento, más el cierre del Discovery en `specs/tech-spec.md` (Lote 4, tarea 2).
- **FR-009** (si el veredicto es negativo, dejar constancia del criterio y del efecto en cascada sobre
  ADR-004): no aplica. El veredicto es positivo, peldaño 1, ADR-004 no se toca.

## 6. Qué queda desbloqueado

- **ADR-001 confirmado**: C# con `ModelContextProtocol` 2.2.0 para el proceso puente. Fin de la
  incertidumbre que bloqueaba Tier 0.
- El Gate Fase 0 para el PoC #1 puede marcarse cumplido por la vía "Peldaño 1 (SDK oficial)" de
  `specs/roadmap.md` (Lote 4, tarea 3), pendiente solo del cierre del PoC #2.
- El PoC es desechable por diseño (FR-010): el código vive en `pocs/001-poc-1-sdk-oficial-de-mcp-para-net/`
  y no se arrastra a `src/`. Lo que sobrevive es este veredicto, la versión fijada del paquete y la
  lección de instrumentación, no el proyecto en sí.
