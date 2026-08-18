# Instrucciones del proyecto — PROMPT_TO_REVIT

`DOCUMENTACION.md` es la **fuente de verdad del diseño**: arquitectura (§2), las dos vías (§3),
contrato de la pasarela (§4), salvaguardas (§5), registro y aprendizaje (§6). Ningún plan, tarea
ni commit puede contradecirlo. Si una tarea lo contradice, no se implementa: se reporta.

`DOCUMENTACION.md` y los specs están **alineados** desde 2026-08-17; §10 de ese fichero resume qué
cambió y por qué. Si en el futuro discrepan, **manda el ADR** de `specs/tech-spec.md` y se actualiza
`DOCUMENTACION.md`, nunca al revés.

Los nombres de las operaciones de §4 (`/query`, `/commands`, `/compile`, `/exec`, `/command`,
`/rollback`) son nombres de **operación**, no rutas HTTP: el transporte es un named pipe.

## Regla de precedencia: commandset → Roslyn

**Antes de usar `exec_csharp`, consultar la lista de comandos disponibles** (`GET /commands`).
Roslyn es la escotilla de emergencia, no la vía por defecto.

Orden obligatorio para cualquier operación:

1. **`/query`** — resolver contra el documento abierto. Prohibido escribir nombres de tipo,
   familia o nivel a mano; primero se obtienen los `ElementId` reales. El código generado usa ids.
2. **`/command`** — si existe un comando compilado que cubra la operación, se usa ese.
3. **`/compile`** — dry-run obligatorio antes de ejecutar C# nuevo. Un fallo de compilación
   cuesta ~1 s; un fallo en runtime dentro de una transacción cuesta mucho más.
4. **`/exec`** — solo si ninguna herramienta anterior cubre la operación, y solo tras aprobación.

Lo que se demuestra estable en Roslyn **gradúa** a comando compilado (§6). El catálogo se puebla
con lo que realmente se usa.

## Salvaguardas no negociables

Las 18 salvaguardas de §5 son el producto, no burocracia. Esto ejecuta código arbitrario dentro
del modelo vivo del usuario. Código que debilita una salvaguarda es un defecto aunque habilite
algo útil. Las cuatro que más se rompen por descuido:

- **`ExternalEvent` siempre.** Jamás tocar la API de Revit desde el hilo del listener del pipe.
- **El bridge nunca escribe en disco.** Ni `Save`, ni `SaveAs`, ni exportaciones.
- **Canal local exclusivamente**: named pipe con ACL del usuario actual. Sin puerto, sin token, sin superficie de red (ADR-002 sustituye el HTTP + token de §E.18).
- **Borrar o modificar preexistentes → aprobación manual siempre**, sin excepción.

**Limitación conocida y aceptada**: Revit es monohilo y no hay timeout real. Un bucle infinito
congela Revit sin posibilidad de matarlo desde fuera. Todo bucle en código generado lleva cota
explícita de iteraciones.

## Entorno

- Revit 2026 (**interfaz en inglés**), .NET 8 (`net8.0-windows`, `win-x64`)
- `RevitAPI.dll` / `RevitAPIUI.dll` referenciadas por paquete NuGet de metadatos: `Nice3point.Revit.Api.RevitAPI` + `Nice3point.Revit.Api.RevitAPIUI` `[2026.4.10]` (ADR-008, confirmado por PoC #2)
- El DLL del addin queda bloqueado con Revit abierto → compilar con Revit cerrado
- Registro en `%APPDATA%\Autodesk\Revit\Addins\2026\`; el `FullClassName` del `.addin` debe
  coincidir exactamente con `namespace.ClassName`
- Conocimiento acumulado de la API: `C:\Users\Usuario\.claude\revit_knowledge\revit_api_knowledge.md`.
  Leerlo antes de escribir código de Revit; actualizarlo al final de cada sesión con lo nuevo.
- `dotnet build`/`dotnet test` siempre con `-v q --nologo` (o `--logger "console;verbosity=quiet"`
  en test). Suprime ruido de MSBuild sin ocultar errores ni warnings; ahorra tokens tanto al
  orquestador como a cualquier subagente que lo ejecute.

## Reparto de agentes

`/aisy.plan-feature` lee el frontmatter de `.claude/agents/*.md` y atribuye cada tarea. Reparto
esperado en este proyecto:

| Agente | Se lleva |
|---|---|
| `architect` | Diseño del esqueleto del addin, decisiones abiertas de §9, evaluar Westwind.Scripting vs Roslyn directo |
| `revit-developer` | Todo lo que toque `RevitAPI.dll` — `RevitBridge.Addin` y `RevitBridge.Utils`: listener del pipe, Roslyn, `ExternalEvent`, transacciones, comandos del catálogo, ventana WPF de aprobación |
| `mcp-developer` | `RevitBridge.Mcp` y `RevitBridge.Core` — servidor MCP en C#, declaración de herramientas y esquemas, cliente del named pipe, contrato de mensajes, timeouts |
| `code-developer` | Código que no es ni Revit ni MCP: tooling, CI, parsing del log JSONL |
| `test-developer` / `tester` | Tests de las partes verificables sin Revit (ver abajo) |
| `judge` | Revisión con las salvaguardas de §5 como checklist explícito |

No inventar nombres de agente: solo los que existen en `.claude/agents/`.

**Fan-out por riesgo, no por defecto** (piloto en Tier 0, ver `specs/roadmap.md`): `architect` solo
cuando queda una decisión de diseño abierta (Tier 1+, §9); en features ya cerradas fila por fila en
el roadmap, ir directo al dev de dominio + `judge`. Agrupar features de bajo riesgo del mismo tier
en un solo ciclo `specify → plan → implement` en vez de uno por feature, y saltar `clarify-feature`
cuando la fila del roadmap no deja gap. `clean-feature` se difiere a un pase por tier, no por
feature. Cada lote cerrado deja una línea en `.claude/orchestration-log.md`; `/harvest-orchestration-
log` decide si la agrupación pasa al siguiente tier o se ajusta.

**Checkpoint de `/clear` al cierre de cada lote.** No hay herramienta para que Claude limpie el
contexto por sí mismo — es un comando de CLI, solo lo lanza el usuario. En cuanto un lote cierra
(`judge` en PASS, tests en verde, entrada escrita en `.claude/orchestration-log.md`), Claude debe
avisar explícitamente "lote cerrado, seguro hacer `/clear`" en vez de asumir que el usuario se
acuerda de preguntar. Es el único momento seguro: nada queda en vuelo que se pueda perder, y el
estado para retomar ya vive en `orchestration-log.md` + `roadmap.md` + este fichero, no en el
historial de la conversación.

## Skills propias de este proyecto

Además del catálogo `aisy.*` (flujo Spec-Driven), hay cuatro skills ad-hoc que **no** vienen del
catálogo y que el instalador no sobrescribe:

| Skill | Cuándo se activa |
|---|---|
| `/revit-bridge` | Cualquier petición que implique leer, crear o modificar algo en el modelo abierto. Impone la escalera `query → command → compile → exec`, el contrato del snippet, los niveles de aprobación y el triaje de errores por `fase` |
| `/revit-api-2026` | Cualquier tarea que toque la API de Revit. Carga el conocimiento acumulado antes de escribir código, y las roturas de API por versión y colisiones de tipos |
| `/harvest-bridge-log` | Cosechar el log JSONL del **producto**: qué snippets graduan a comando compilado y qué errores recurrentes van a `revit_api_knowledge.md` (§6) |
| `/harvest-orchestration-log` | Cosechar `.claude/orchestration-log.md`, el log del **proceso** de desarrollo (specify/plan/implement): qué saltos de ceremonia funcionan y cuáles hay que revertir |

Ninguna duplica reglas: `DOCUMENTACION.md` y `revit_api_knowledge.md` siguen siendo las fuentes
únicas, las skills son el procedimiento.

## Skill externa vendorizada: `/diagram-design`

`.claude/skills/diagram-design/` es una copia íntegra (no de autoría propia) de
[cathrynlavery/diagram-design](https://github.com/cathrynlavery/diagram-design) (MIT; ver
`LICENSE-UPSTREAM` y `THIRD_PARTY_LICENSES.md` dentro de la carpeta de la skill — bundlea iconos
MIT/CC0 de Tabler Icons y Simple Icons). Genera diagramas editoriales autocontenidos en HTML+SVG
(arquitectura, secuencia, máquinas de estado, ER, swimlane, timeline, Gantt, árboles, org charts,
cuadrantes, radar...) sin dependencias externas ni JS — encaja con las restricciones del `Artifact`
del propio Claude Code (self-contained, sin CDN).

Es una capa de **presentación**, generada a partir de `DOCUMENTACION.md`/`specs/`/
`revit_api_knowledge.md`, no una fuente de verdad nueva ni un sustituto de ellos. Útil para
diagramas de secuencia del contrato del named pipe, máquinas de estado del flujo de aprobación
(§5), o visualizar la salida de `ExportarGrafoTopologico` antes de escribir el C# que la procesa —
no para documentación normativa, que sigue siendo texto versionable.

## Qué significa "verificado" aquí

Ninguna tarea puede reportarse como funcionando por haber compilado. Tres niveles, y siempre hay
que decir en cuál se está:

1. **Compila** — Debug **y** Release limpios. Mínimo para cerrar una tarea.
2. **Verificado fuera de Revit** — el filtro sintáctico de Roslyn, el parseo del JSONL, la lógica
   pura y el servidor MCP contra un addin simulado se prueban en tests o en una consola. Esto es
   donde debe vivir la mayor parte de la cobertura, porque es lo único automatizable.
3. **Verificado en Revit vivo** — solo el usuario puede confirmarlo. Nunca lo afirma un agente.

`tester` no puede ejecutar Revit. Su trabajo real aquí es el nivel 2 y confirmar que la build
está limpia. Un "verde" falso es peor que un rojo.

## Antes de cualquier sesión con el bridge

Guardar el modelo, sobre archivo local, nunca directamente sobre un modelo central compartido.

## Convenciones de repositorio

- Nunca push directo a `main`. Todo cambio entra vía pull request.
- Prefijos obligatorios en el título de la PR: `release:` (major), `feature:` (minor),
  `fix:` (patch), `chore:` (sin tag).
