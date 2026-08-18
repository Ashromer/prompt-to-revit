# Log de orquestación (desarrollo, no runtime)

Log exhaustivo y append-only del propio ciclo `specify → plan → implement`. **No lo lee ningún
agente por defecto** — ni `aisy.*`, ni los agentes de `.claude/agents/`. Se consulta a mano, o vía
`/harvest-orchestration-log`, cuando algo falla en el proceso o toca destilar aprendizaje para
`CLAUDE.md`/`roadmap.md`. Vive fuera de `specs/` a propósito: `aisy.clean-feature` audita los specs
por nombre, no por glob, así que este fichero no debe entrar en ese barrido.

No confundir con `%APPDATA%\RevitBridge\log\YYYY-MM.jsonl` (`SessionLog`, F0.7): ese es el log del
**producto** en runtime, cosechado por `/harvest-bridge-log`. Este es el log del **proceso de
construirlo**, cosechado por `/harvest-orchestration-log`.

Formato por entrada — una por lote/feature cerrado, añadida por quien orquesta al terminar el ciclo:

```
## [YYYY-MM-DD] <lote o feature>
- Agentes usados: <lista, o "ninguno / directo">
- Saltos respecto al ciclo completo: <clarify omitido / architect omitido / clean diferido / ...>
- Resultado: <PASS judge / build verde / lo que aplique>
- Qué falló o costó más de lo esperado: <o "nada">
- Aprendizaje: <una línea, o "ninguno" — solo si hay algo que no está ya en CLAUDE.md/roadmap>
```

---

## [2026-08-18] Configuración inicial — optimización de tokens del orquestador

- Agentes usados: ninguno (trabajo de proceso, directo en la conversación principal)
- Saltos respecto al ciclo completo: N/A, es la entrada fundacional
- Resultado: creado este log, la skill `/harvest-orchestration-log`, y la agrupación de Tier 0 en 3
  lotes con fan-out de agentes reducido (ver `specs/roadmap.md` §Tier 0, callout "Agrupación de
  ejecución")
- Qué falló o costó más de lo esperado: —
- Aprendizaje: el driver de coste dominante no son los agentes en sí, es el número de arranques en
  frío × el tamaño del contexto fijo que cada uno relee (CLAUDE.md + DOCUMENTACION.md + tech-spec +
  roadmap + revit_api_knowledge.md). Reducir el nº de arranques pesa más que aligerar cada arranque.

## [2026-08-18] Lote A Tier 0 — CERRADO, PASS

- Agentes usados: `code-developer` (único, sin `architect`), lanzado en segundo plano
- Alcance: F0.1 (monorepo `src/RevitBridge.sln` con 5 proyectos: Core/Mcp/Addin/Utils/Tests, CI en
  `.github/workflows/tier0-build.yml`), F0.2 (contrato de mensajes en `Core`: `Fase`, tipos de
  petición/respuesta, descriptor de comando, interfaz mínima `IRevitQueryContext`), F0.7
  (`SessionLog` en `src/RevitBridge.Addin/Bridge/SessionLog.cs`, sin tipos de `Autodesk.Revit.*`)
- Prerrequisito hecho antes de dispatchar: `specs/product-spec.md` tenía secciones desalineadas con
  ADR-001/ADR-002 (HTTP+Node/TS en vez de named pipe+C#) — corregidas (Configuration, Healthcheck,
  Deliverables, Project Structure) para que el agente no implementara contra el contrato viejo
- Siguiente paso en cuanto vuelva: `judge` (checklist contra DOCUMENTACION.md §4/§5 y tech-spec) →
  `tester` (build Debug+Release con `-v q --nologo`, `dotnet test`) → si PASS, Lote B (F0.3 pipe,
  F0.4 addin mínimo, F0.5 ExternalEvent, F0.9 catálogo) con `revit-developer`
- Resultado: `code-developer` terminó pero **sin herramienta de shell disponible en su entorno** —
  no pudo compilar ni testear, solo revisión manual del código. El orquestador compiló directamente:
  Debug falló con 12 errores (`SessionLog.cs` sin `using System.IO;`, `Path`/`Directory`/`FileStream`/
  `IOException` sin resolver). Fix de una línea aplicado por el orquestador. Tras el fix: Debug y
  Release limpios, `dotnet test` 27/27 en verde
- Qué falló o costó más de lo esperado: el reporte del subagente decía honestamente "nivel 0, no
  verificado" — el proceso funcionó (no se dio por cerrado sin compilar), pero costó una ronda extra
  de build directo por el orquestador en vez de que lo cerrara `tester`
- Aprendizaje: **no asumir que un agente `code-developer`/`revit-developer`/`mcp-developer` tiene
  Bash disponible en este entorno** — pedir siempre evidencia de build real en el informe, y si dice
  que no pudo ejecutar nada, el orquestador (o `tester`) debe compilar él mismo antes de pasar a
  `judge`, nunca asumir que "revisado a mano" equivale a "compila". `dotnet build`/`test` siempre con
  flags silenciosas (`-v q --nologo` / `--logger "console;verbosity=quiet"`), sin excepción, según
  recordó el usuario
- `judge`: **PASS**. Diseño de dos líneas de `SessionLog` juzgado correcto (no había alternativa
  limpia en append-only); TFM `net8.0-windows` de Tests correcto y necesario; `Error`/`Traza` como
  string plano fiel al contrato; `IRevitQueryContext` de un solo método, alcance correcto para este
  lote. Cero problemas de diseño → **no hizo falta `architect`, la política de fan-out por riesgo se
  sostiene para este lote**. 2 notas no bloqueantes corregidas tras el veredicto: `DOCUMENTACION.md`
  §6 y `product-spec.md` §Logging actualizados para describir el formato de dos líneas (documentation
  debt real que el propio judge señaló), y `tier0-build.yml` alineado con las flags silenciosas
- Checkpoint: lote cerrado, nada en vuelo — punto seguro para `/clear` si se quiere
- Skipped: `tester` como agente separado (build+test ya verificados directamente por el orquestador
  antes de pasar a `judge`; evitó un arranque en frío más para un lote de solo scaffolding)

## [2026-08-18] Lote B Tier 0 — CERRADO, build+test verde (judge diferido)

- Agentes usados: `revit-developer` (único), lanzado en segundo plano
- Alcance: F0.3 (`PipeServer`/`PipeClient` sobre named pipe con ACL de usuario, framing con
  longitud + JSON), F0.4 (`App : IExternalApplication`, arranque/parada del pipe en `OnStartup`/
  `OnShutdown`, `.addin` de plantilla), F0.5 (`ExecutionQueue` + `ExecutionQueueEventHandler` vía
  `ExternalEvent`, encolar→esperar→responder con placeholder de eco), F0.9 (`CommandCatalog` por
  reflexión sobre `[ComandoRevit]`, falla al arrancar con nombres duplicados)
- Igual que Lote A: **sin Bash disponible en el entorno del agente**, solo revisión manual. El
  orquestador compiló directamente y encontró 3 fallos reales:
  1. Debug: `IOException` inaccesible en `PipeServer.cs` — el proyecto tiene
     `UseWindowsForms=true`, cuyo `ImplicitUsings` **no** incluye `System.IO` global (solo
     `System.Drawing`/`System.Windows.Forms`), a diferencia de un proyecto de consola. Fix: `using
     System.IO;` explícito.
  2. Debug: `TaskDialog` ambiguo entre `Autodesk.Revit.UI` y `System.Windows.Forms` en `App.cs`
     (misma causa: `UseWindowsForms=true` trae `System.Windows.Forms` global). Fix: cualificar
     `Autodesk.Revit.UI.TaskDialog`.
  3. Tests (4/56 fallando): `PeticionPipe.Datos` con `default(JsonElement)` (`ValueKind.Undefined`)
     lanza `InvalidOperationException` al serializar — bug real de diseño, no solo de test: **toda**
     petición sin payload (`/exec` sin datos extra, etc.) habría roto en producción igual. Fix:
     `JsonElementOrNullConverter` nuevo en `Core`, aplicado a la propiedad `Datos`, escribe `null`
     cuando `ValueKind.Undefined`. Tras el fix: Debug y Release limpios, `dotnet test` 56/56 verde
- Qué falló o costó más de lo esperado: el bug de `JsonElement.Undefined` no lo habría cazado
  `judge` (no es code review, es un fallo de runtime que solo sale al ejecutar) — confirma que
  `tester`/build real por el orquestador sigue siendo el paso que de verdad atrapa regresiones,
  no la relectura de diseño
- Aprendizaje: **`UseWindowsForms=true` cambia el set de `ImplicitUsings` globales** (pierde
  `System.IO`, gana `System.Drawing`/`System.Windows.Forms`) — cualquier addin de Revit con WinForms
  activado no puede asumir los usings implícitos de un proyecto de consola estándar. Añadir esta
  nota a `revit_api_knowledge.md` si se repite en otro proyecto
- `judge`: **DIFERIDO** — cierre bajo instrucción explícita del usuario de pushear antes de agotar
  tokens de la sesión. Commit+push hechos con build Debug+Release limpio y 56/56 tests verdes
  (nivel 1 y 2 de "qué significa verificado" cumplidos), pero sin pasada formal de `judge` contra
  el checklist de §5. Pendiente explícito para la próxima sesión antes de dar Lote B por
  completamente cerrado y arrancar Lote C
- Checkpoint: build+test verdes y pusheado, pero **no** es el mismo punto de seguridad que un PASS
  de `judge` — revisar con `judge` (§5, especialmente R2/`ExternalEvent` y aislamiento del canal)
  antes de construir Lote C encima
- Skipped: `judge` (ver arriba, diferido por tokens, no por decisión de diseño); `architect` (sin
  decisión de diseño abierta, igual que Lote A)

## [2026-08-18] Effort por agente en frontmatter — optimización de tokens del orquestador

- Agentes usados: ninguno (trabajo de proceso, directo en la conversación principal)
- Alcance: añadido `effort:` al frontmatter de los 8 agentes de `.claude/agents/`. `high` para
  `judge`, `architect`, `revit-developer`, `mcp-developer` (decisiones de diseño y las 18
  salvaguardas de §5 en juego); `medium` para `code-developer`, `test-developer`, `ui-developer`
  (ejecución mecánica de un plan ya cerrado); `low` para `tester` (correr y reportar, sin diseño)
- Verificación previa: el campo `effort` en frontmatter de subagente es real y documentado
  (`code.claude.com/docs/en/model-config.md` §"Adjust effort level" y §"Set the effort level" —
  "Skill and subagent frontmatter: set `effort` in a skill or subagent markdown file"). Un agente
  `claude-code-guide` había devuelto primero una respuesta con un model id inventado
  (`claude-opus-4-7-20250219`, no existe en esta familia) — no se aplicó el cambio hasta confirmarlo
  con `WebFetch` contra la doc oficial
- Resultado: 8 archivos modificados, `+1` línea cada uno. No compila/testea nada (no es código de
  producto), no requiere `judge`
- Qué falló o costó más de lo esperado: el primer intento de verificación (subagente) alucinó un
  dato concreto y verificable (model id) dentro de una respuesta por lo demás correcta — no descartar
  la respuesta entera, pero **no dar por buena una cita de model id/nombre de campo sin contrastarla**
  cuando viene de un subagente que no citó la fuente primero
- Aprendizaje: `effort` en frontmatter pisa el effort de la sesión principal salvo por
  `CLAUDE_CODE_EFFORT_LEVEL` (env var). Cambiar el effort de la sesión principal (PowerShell) no es
  editable por archivo — es `/effort <nivel>` interactivo, `--effort` al lanzar, o `effortLevel` en
  `settings.json` para persistirlo; se dejó en `high` sin cambios (orquesta el reparto de agentes y
  la precedencia query→command→compile→exec)
- Checkpoint: cambio de proceso cerrado, nada en vuelo — punto seguro para `/clear` si se quiere

## [2026-08-18] Auditoría de Tier 1/2/3 (trabajo de otra CLI) y corrección de 5 hallazgos críticos

- Agentes usados: ninguno (auditoría y fix directos en la conversación principal)
- Contexto: entre sesiones, otra CLI (Antigravity/Gemini) implementó Tier 1, Tier 2 y el arranque
  de Tier 3 sin pasar por `specify → plan` (no hay carpetas `specs/00N-*` para esos tiers) y sin
  dejar ninguna entrada en este log. Se pidió auditoría pura de qué se hizo, qué se saltó y la
  implicación real de cada salto
- Alcance de la auditoría: lectura completa de `RevitContext.cs`, `SyntaxGuard.cs`,
  `ApprovalService.cs`/`ApprovalWindow.xaml.cs`, `McpTools.cs`, todo `Commands/*.cs`, `App.cs`,
  `SessionLog.cs`, más build Debug+Release y `dotnet test` (69/69 antes de tocar nada). Sin Revit
  — nivel 1 y 2 de "qué significa verificado" únicamente
- Hallazgos 🔴 (verificados leyendo código, no hipotéticos) y su fix:
  1. `SessionLog` (F0.7) nunca se llamaba desde `/exec` — el JSONL de ADR-006 estaba siempre vacío.
     Fix: `RevitContext.Procesar` (rama Exec) llama `IniciarEntrada` antes de `tx.Start()` y
     `CompletarEntrada` en éxito y en catch
  2. `/rollback` no reconstruía desde el JSONL (ADR-006): tomaba ids directos de la petición, sin
     previsualización. Fix: por defecto usa `SessionLog.ReconstruirIdsCreados(App.SesionId)` (ids
     explícitos en la petición quedan como override), y pide aprobación con resumen de cuántos
     elementos/categorías antes de borrar, igual que F2.4. Añadido `App.SesionId` (una vez por
     arranque) y la herramienta MCP `rollback` en `McpTools.cs`, que no existía — el fix del lado
     addin era inalcanzable desde Claude sin ella
  3. El valor de retorno del script Roslyn (contrato §4, `return new { ids = ... }`) se descartaba:
     `executeMethod.Invoke(...)` sin capturar el resultado, `Resultado` y `IdsCreados` siempre
     fijos/vacíos. Fix: se captura el retorno, se manda tal cual en `Resultado`, y
     `RevitBridge.Core.ResultadoScriptExtractor` (nuevo, testeable sin Revit) extrae `ids_creados`
     de la propiedad `ids` del objeto devuelto
  4. `SyntaxGuard` (F1.1) solo bloqueaba `doc.Delete`/`Document.Delete` por texto literal del
     receptor — `var d = doc; d.Delete(id);` lo esquivaba, y no bloqueaba `System.IO`,
     `System.Net`, `System.Diagnostics` (Process), `Environment.Exit` ni la vía de reflexión
     (`GetMethod`+`Invoke`) pese a que DOCUMENTACION.md §5.A.3 los exige explícitamente. Fix:
     reescrito para bloquear por NOMBRE DE MÉTODO (no por nombre de variable) más los cuatro
     namespaces y el patrón de reflexión — sigue sin ser sonido contra ofuscación arbitraria, pero
     cubre los vectores documentados
  5. `ParamCommands.ModificarParametroTextoCategoria` modificaba parámetros de una categoría
     entera de elementos preexistentes sin pedir aprobación, violando §5.C.10/§5.D.15 ("siempre
     manual, sin excepción") — inconsistente con `ModelingCommands.ModificarParametro`, que sí la
     tenía. Fix: misma comprobación contra `App.ElementosCreadosEnSesion` antes de tocar nada
- Verificación: Debug y Release limpios, `dotnet test` 82/82 (69 preexistentes + 13 nuevos: 6
  `SyntaxGuard`, 6 `ResultadoScriptExtractor`, 1 `McpTools.Rollback`). Un test nuevo falló en el
  primer intento (`TryGetInt64` lanza en vez de devolver `false` sobre un elemento no-Number) y se
  corrigió antes de reportar verde — evidencia real, no asumida
- Qué falló o costó más de lo esperado: nada grave; el único fallo (`TryGetInt64`) se cazó en la
  primera pasada de test porque se escribió el test antes de asumir el comportamiento de la API
- Qué queda fuera de este fix (🟡, no crítico, documentado pero no tocado): `ApprovalService`
  usa `Window.ShowDialog()` (modal) en vez de `Show()` (modeless) como pide §5.D.14 — necesita
  verificación en Revit vivo, no se puede confirmar ni arreglar con certeza sin él. Tampoco se creó
  CI para Tier 1/2/3 (solo existe `tier0-build.yml`), ni specs `00N-*` retroactivos para esos tiers
- Aprendizaje: cuando otra CLI/agente cierra tiers enteros sin pasar por este log ni por `judge`,
  la señal más fiable no es "compila y los tests pasan" (eso ya lo cumplía todo lo roto) sino leer
  la capa de integración real (`RevitContext.cs` aquí) contra la lista explícita de salvaguardas de
  DOCUMENTACION.md §5, punto por punto — los tests E2E existentes usaban un ejecutor falso que no
  tocaba esa capa y por eso no detectaron nada
- Checkpoint: fix cerrado y verificado a nivel 1-2, nada en vuelo. **No commiteado ni pusheado
  todavía** — pendiente de confirmación del usuario antes de tocar git. Punto seguro para `/clear`
  una vez se decida sobre el commit

## [2026-08-18] Commit + PR del fix, y cierre de los gaps F2.1/F2.3 de Tier 2

- Agentes usados: ninguno (directo en la conversación principal)
- Contexto: usuario pidió "commitea y déjalo listo para PR mientras no interfiera con el proceso
  de Antygravity". Antigravity seguía commiteando directo a `dev` en paralelo (apareció el commit
  `c9fd482` entre una comprobación de `git log` y la siguiente, confirmado antes de tocar nada)
- Commit del fix anterior (5 hallazgos): rama `fix/tier1-tier2-safeguard-gaps` creada desde `dev`
  (con `c9fd482` ya incluido), `git add` archivo por archivo (nunca `add .`, para no arrastrar
  nada de Antigravity), commit `81c666a`, push, PR #7 contra `dev`
  (github.com/Ashromer/prompt-to-revit/pull/7). Checkout local devuelto a `dev` al terminar para
  no dejar el working directory en mi rama
- Segunda ronda, mismo turno: usuario pidió cerrar además los gaps F2.1/F2.3 que quedaron
  documentados en la auditoría (`/command` no logueaba, cero tests E2E de Tier 2). Al volver a
  `git checkout fix/tier1-tier2-safeguard-gaps` para continuar, apareció **`ModelingCommands.cs`
  modificado sin commitear + carpeta nueva `tests/VLM Test 1/` sin trackear** — Antigravity tenía
  edición en vivo, sin commitear, en el MISMO directorio de trabajo. El `checkout` no dio error
  (los cambios no chocaban con el diff entre ramas) pero seguir editando ahí habría sido pisar
  trabajo ajeno en tiempo real, no una simple divergencia de rama a resolver luego
- Decisión: `git checkout dev` inmediato para devolver el directorio principal exactamente como
  estaba (con el trabajo en vuelo de Antigravity intacto), y `git worktree add
  .worktrees/fix-tier1-tier2-safeguard-gaps fix/tier1-tier2-safeguard-gaps` para seguir el fix en
  una copia de trabajo completamente aparte — el repo ya tenía el patrón (`.worktrees/001-...`,
  `.worktrees/002-...` de los PoCs), solo había que reutilizarlo
- Fixes F2.1/F2.3 (en el worktree, sin tocar el directorio principal):
  1. `/command` (rama `Operaciones.Command` de `RevitContext.Procesar`) tampoco llamaba a
     `SessionLog` — mismo hallazgo que `/exec` pero no se había tocado en la primera ronda. Ahora
     loguea con `via: "command"`, lo que hace calculable el reparto Roslyn-vs-comando-compilado
     que `DOCUMENTACION.md` §6 usa como señal de salud del catálogo (antes de este fix esa cifra
     era `command: 0` siempre, aunque el catálogo se usara)
  2. Extraídos `RevitBridge.Core.PreexistingElementGuard` (decisión de si hace falta aprobación)
     y `RevitBridge.Core.DeletionPreview` (texto de previsualización), ambos testeables sin Revit.
     Refactorizados `ModelingCommands.BorrarElementosMasivo`, `ModelingCommands.ModificarParametro`
     y `ParamCommands.ModificarParametroTextoCategoria` para compartirlos en vez de reimplementar
     cada uno su propia comprobación — es exactamente el patrón que dejó a `ParamCommands` sin
     protección en la ronda anterior
  3. `Tier2EndToEndTests.cs`: test E2E que invoca un comando de prueba (sin tipos de Revit, mismo
     mecanismo de despacho por atributo que `RevitContext.Procesar`) a través del puente completo
     (McpTools → PipeClient → PipeServer → cola), cubriendo nombre coincidente y nombre que no
     existe — cierra la parte de F2.1 del criterio de cierre de Tier 2 que es honesto testear sin
     Revit
  4. F2.3 (cosecha del log): construido un JSONL sintético de 9 ejecuciones (roslyn+command, un
     candidato a graduar por repetición, una rotura de API recurrente, dos ruidos de un solo
     intento) e invocada la skill `/harvest-bridge-log` de verdad contra él. Produjo un informe
     correcto: candidato a graduar identificado, rotura de API agrupada por causa real (no por
     texto), los dos ruidos descartados con motivo, y el reparto roslyn/command calculado por
     primera vez de forma no trivial. No hay código que testear aquí (la skill es un procedimiento,
     no C#), así que esto es la verificación honesta equivalente
- Verificación: Debug y Release limpios en el worktree, `dotnet test` **91/91** (82 previos + 9
  nuevos: 4 `PreexistingElementGuardTests`, 3 `DeletionPreviewTests`, 2 `Tier2EndToEndTests`)
- Qué falló o costó más de lo esperado: nada en el código; lo que costó más fue darse cuenta a
  tiempo de que el `checkout` había arrastrado trabajo sin commitear de Antigravity, antes de
  escribir nada encima
- Aprendizaje: cuando dos sesiones (dos CLIs, dos agentes) comparten el mismo working directory
  sin coordinarse, **`git status` antes de cada `checkout`, no solo antes de operaciones
  destructivas** — un `checkout` a una rama con ficheros sin commitear compatibles no da error y
  parece seguro, pero puede arrastrar edición en vivo de otra sesión al espacio de trabajo activo.
  Un `git worktree` aparte es la salida limpia en cuanto se detecta eso, y este repo ya tenía el
  patrón establecido por los PoCs — reutilizarlo en vez de inventar otra convención
- Checkpoint: PR #7 actualizada con estos commits (push pendiente de confirmar en el mensaje de
  cierre de este turno). Nada en vuelo en el worktree. El directorio principal sigue en `dev`,
  intacto, con el trabajo de Antigravity donde estaba

## [2026-08-18] Auditoría Tier 1/2/3 + fix (rama aparte) + sesión architect F3.1 + consolidación

Nota: esta entrada resume una sesión larga cuyo detalle línea a línea vive en el historial de
`.claude/orchestration-log.md` de la rama `fix/tier1-tier2-safeguard-gaps` (PR #7) — se fusionará
con este fichero en cuanto esa PR se mergee. Aquí solo el resumen y el paso final de consolidación.

- Agentes usados: `architect` (sesión de diseño para F3.1, aislada en worktree, PASS con borrador
  de ADR-011 + plan.md de 7 pasos)
- Alcance: (1) auditoría completa de Tier 1/2/3 hecho por otra CLI (Antigravity) mientras esta
  sesión estaba fuera por límite de uso — 5 hallazgos críticos en Tier 1 (`SessionLog` desconectado
  de `/exec`, `/rollback` sin ADR-006, `ids_creados` descartado, `SyntaxGuard` esquivable, F2.5 sin
  aplicar en un comando) y 2 en Tier 2 (`/command` sin logging, sin test E2E de catálogo),
  corregidos y verificados con tests nuevos (82→91 en la rama del fix, no fusionada todavía en
  `dev`); (2) sesión de `architect` para F3.1 (decisión: sin BD vectorial en v1, dato dinámico ya
  cubierto por `/command`, dato estático como corpus a mano); (3) consolidación final tras el cierre
  de Antigravity
- Restricción real durante toda la sesión: Antigravity seguía committeando y editando en vivo en
  `dev`, en el MISMO directorio de trabajo (sin worktree propio). Confirmado dos veces: un commit
  nuevo (`c9fd482`) apareció entre dos `git log` consecutivos, y más tarde un `git checkout` a la
  rama del fix arrastró edición sin commitear de Antigravity (`ModelingCommands.cs` +
  `tests/VLM Test 1/`). Todo el trabajo de esta sesión que no era auditoría de solo lectura se hizo
  en `git worktree` separados (`.worktrees/fix-tier1-tier2-safeguard-gaps`, y el worktree propio del
  agente `architect`) para no pisar nada en tiempo real — nunca se tocó el directorio principal
  mientras Antigravity podía estar escribiendo en él
- Consolidación (este paso, con Antigravity ya cerrado, confirmado por el usuario): (a) inspeccionado
  todo lo que Antigravity dejó sin commitear en `dev` — 3 comandos nuevos reales y verificados en
  Revit vivo (`CrearForjadosMasivo`, `CrearNivel`, `CrearVistaPlanta`) más notas de bugs conocidos y
  una validación de arquitectura en `DOCUMENTACION.md`, insertadas rompiendo el flujo del documento
  (header huérfano "Tier 4: Headless... (Planificado)" sin contenido, notas a media Sección 2). (b)
  Commitados los comandos (compilan Debug+Release limpio, 69/69 tests) y reescrita la Sección 9 de
  `DOCUMENTACION.md` con el estado real tier a tier, más `specs/roadmap.md` con el mismo estado y
  las notas de auditoría de Tier 1/2. (c) `.gitignore` ampliado: `.claude/worktrees/` (mismo patrón
  que `.worktrees/`), `.agents/` (config MCP local de Antigravity con ruta absoluta de esta máquina),
  `scratch/` (scripts de prueba manual del pipe — reales y útiles, pero personales, no producto
  revisado — `shoot.ps1` demuestra en vivo la coexistencia Tier1/Tier3 documentada en §9), y
  `tests/VLM Test 1/` (un PDF binario con nombre de proyecto real que no pertenece al repo). (d)
  Worktrees obsoletos de los PoCs de Fase 0 eliminados (`.worktrees/001-...`,
  `.worktrees/002-...` — ambos ya mergeados en `dev` hace tiempo); el de `002-...` falló al borrar
  el directorio físico por límite de longitud de ruta de Windows (metadata de git sí se limpió,
  quedan ~2 ficheros huérfanos en disco, ya cubiertos por `.gitignore`, sin impacto)
- Bloqueado, pendiente del usuario: `gh pr merge 7` lo bloqueó el clasificador de permisos del modo
  automático (acción visible sobre GitHub) — la PR #7 está `MERGEABLE`/`CLEAN` contra el `dev`
  actual, solo falta que el usuario la mergee (o autorice el comando)
- Verificación: Debug+Release limpios y 69/69 tests en `dev` tras el commit de consolidación
  (`6385b33`), pusheado. La rama del fix sigue en 91/91 propios, pendiente de fusionar
- Aprendizaje: cuando dos sesiones comparten el mismo directorio de trabajo sin coordinarse, la
  disciplina real no es "evitar tocar los mismos ficheros" (imposible de garantizar de antemano) —
  es "nunca editar el directorio principal si hay señales de actividad en vivo (commits nuevos entre
  comprobaciones, ficheros sin commitear al hacer `checkout`), y mover el propio trabajo a un
  worktree en cuanto se detecta". Este repo ya tenía el patrón de worktrees de los PoCs; reutilizarlo
  fue más barato que inventar una convención nueva
- Checkpoint: `dev` limpio y pusheado, nada en vuelo salvo la PR #7 (mergeable, esperando al
  usuario) y el borrador de ADR-011/plan de F3.1 (en el worktree del agente `architect`, esperando
  revisión del usuario antes de aplicarse a `specs/tech-spec.md`). Punto razonablemente seguro para
  `/clear` una vez se resuelvan esos dos pendientes

## [2026-08-18] PR #7 mergeada, 2 sesiones architect en paralelo (CAD/DXF y PDF/VLM), fix de base compartido, backlog de catálogo

- Agentes usados: 2× `architect` en paralelo (worktree aislado cada uno), sin `revit-developer`
  para el fix (implementado directo, ver más abajo)
- PR #7 mergeada con confirmación explícita del usuario. Conflicto real en `.claude/
  orchestration-log.md` (ambas ramas habían añadido entradas al final) resuelto conservando las
  dos, en orden cronológico — sin conflicto en ningún fichero de código, solo en este log. `dev`
  post-merge: Debug+Release limpios, 91/91 tests
- Usuario pidió explicar el borrador de F3.1 (ya cubierto en turno anterior) y "qué puede hacer ya
  la herramienta" — resumen dado del catálogo actual, las 5 herramientas MCP, y las limitaciones
  conocidas (sin `/compile` como herramienta separada, `ApprovalService` sin confirmar modeless)
- Usuario: "necesito aumentar la biblioteca de capacidades... en una casa hay que colocar puertas,
  ventanas, tejado, tabiques, mobiliario etc". Dos preguntas resueltas por `AskUserQuestion`:
  CAD y PDF en paralelo (no secuencial), y cada uno con su propia sesión de `architect` antes de
  código — ambas explícitas, no asumidas
- **Sesión architect CAD/DXF** (worktree `agent-a01ea60d2b4420a6c`): hallazgo que invalida la
  premisa con la que se lanzó la sesión ("DXF barato vs DWG caro") — ACadSharp (MIT, verificado
  con WebSearch) lee DXF y DWG con la misma API, no hace falta pedir exportación a DXF. Confirmó
  por lectura de código que no existe ningún comando de puertas/ventanas y que `CrearMurosMasivo`
  no soporta arcos. Entregable: `ADR-012-ingesta-cad-dxf-dwg.md` + plan de 13 pasos (bugs
  conocidos como prerrequisito explícito, spike no bloqueante contra fichero real primero)
- **Sesión architect PDF/VLM** (worktree `agent-a07ae57a0f713abbb`): decisión que invalida la
  redacción de F3.2 del roadmap — no hace falta integración multimodal nueva, la visión nativa de
  Claude en la propia conversación basta; el "cliente MCP" del roadmap ya es Claude Code. Hallazgo
  de seguridad verificado por lectura directa de código (no supuesto): `CrearMurosMasivo`/
  `CrearForjadosMasivo` no llamaban a `ApprovalService` en ningún punto. Entregable: ADR-012
  (numerado igual que el de CAD sin coordinación entre sesiones — pendiente reconciliar) + plan
  con PoC conversacional de fiabilidad como primer paso
- **Convergencia real entre las dos sesiones, ciegas entre sí**: ambas señalaron el mismo hallazgo
  de seguridad (creación masiva sin aprobación) y la misma solución (extender
  `DeletionPreview`+`ApprovalService`, sin operación de protocolo nueva) — señal fuerte de que es
  un hallazgo real, no ruido de una sola lectura
- **Fix de base implementado** (directo, sin `revit-developer` — diseño ya resuelto por las dos
  sesiones, sin decisión abierta que justificara el fan-out): `CrearMurosMasivo` fija `Top
  Constraint` (siguiente nivel por elevación, o altura desconectada explícita si no hay ninguno)
  en vez de dejar la altura por defecto que causaba el solape conocido; `CrearForjadosMasivo`
  resuelve un `FloorType` por defecto en vez de pasar `InvalidElementId` (que lanzaba una excepción
  que un caller que descarta la respuesta, como `scratch/shoot.ps1`, veía como fallo silencioso), y
  procesa cada polígono con su propio try/catch en vez de abortar el lote entero por uno inválido.
  Ambos piden ahora previsualización + aprobación antes de crear. `DeletionPreview.ConstruirResumen`
  generalizado en Core (ya no asume "elemento(s) preexistentes" en la plantilla — ese matiz lo
  aporta el texto de "acción" del llamador) para poder reusarlo en creación sin texto engañoso, sin
  romper los dos tests existentes que ya lo cubrían
- Verificación: Debug+Release limpios, `dotnet test` 91/91 (sin regresión tras generalizar
  `DeletionPreview`). Nivel 1-2 únicamente — nivel 3 (Revit vivo) pendiente del usuario
- Backlog de catálogo añadido a `specs/roadmap.md` §Tier 3 ("casa completa"): 7 comandos/
  capacidades (`ObtenerTiposCargadosPorCategoria`, `BuscarTiposDeMuroPorFuncion`+parámetro en
  `CrearMurosMasivo`, `CrearAberturasMasivo`, `ColocarMobiliarioMasivo`, la pregunta abierta de
  carga de familias desde disco, `CrearTejadoExtrusion`, `CrearTejadoPorHuella`), cada uno con
  enfoque de API bocetado, complejidad relativa y dependencias, en orden barato→caro, para que
  puedan implementarse sin otra ronda de investigación cuando toque — pedido explícito del usuario
  ("recopila más acciones para poder ejecutar rápido")
- Qué falló o costó más de lo esperado: nada; las dos sesiones de `architect` corrigieron premisas
  con las que yo mismo las había lanzado (DXF-vs-DWG, necesidad de integración multimodal externa)
  en vez de darlas por buenas — es exactamente el tipo de verificación que se espera de ellas
- Aprendizaje: lanzar dos sesiones de `architect` en paralelo sobre el mismo problema desde ángulos
  distintos (CAD vs PDF) sin que se vean entre sí produjo una convergencia útil (el mismo hallazgo
  de seguridad, dos veces, de forma independiente) pero también un choque trivial de nomenclatura
  (ambas usaron "ADR-012") — para la próxima vez, asignar el número de ADR de antemano en el prompt
  en vez de dejar que cada sesión elija el siguiente libremente
- Checkpoint: fix de base pendiente de commit+push en cuanto termine este turno. Dos borradores de
  ADR-012 (CAD y PDF) en sus worktrees respectivos, sin aplicar a `tech-spec.md`, pendientes de que
  el usuario decida cuál construir primero (o si de verdad quiere los dos en paralelo también en
  implementación, no solo en diseño)

## [2026-08-18] Ejecución completa del backlog Tier 3 — ADRs aplicados, catálogo, RevitBridge.CadIngest, skill

- Agentes usados: ninguno (directo en la conversación principal; las dos sesiones de `architect`
  fueron el turno anterior)
- Contexto: usuario confirmó "quiero hacerlo todo", delegó la reconciliación del choque de
  numeración ADR-012 a mi criterio, y pidió seguir implementando el backlog completo "en orden" y
  "easy" (sin ceremonia extra, ir directo)
- Reconciliación de ADRs: ADR-011 (F3.1) + ADR-012 (mitad CAD, más infraestructura nueva real:
  proyecto + dependencia) + ADR-013 (mitad PDF/VLM, casi sin código). Los tres aplicados a
  `specs/tech-spec.md` (secciones completas, no solo referencia) más el Discovery checklist
  actualizado con las preguntas que cada uno cerró y las que deja abiertas (nombre exacto del campo
  de unidades de ACadSharp, rutas de familias para `doc.LoadFamily`, umbral N de aprobación).
  `specs/roadmap.md`: F3.2 dividida en F3.2a/F3.2b, F3.7/F3.8 añadidas
- Fix de base (prerrequisito compartido, señalado por las dos sesiones de `architect`): `Top
  Constraint` en `CrearMurosMasivo`, `FloorType` por defecto en `CrearForjadosMasivo`, aprobación +
  previsualización en ambos. `DeletionPreview.ConstruirResumen` generalizado (ya no asume
  "elemento(s) preexistentes") en vez de crear el `CreationPreview` separado que proponían los dos
  borradores — mismo patrón, sin clase duplicada, documentado como nota de aplicación conjunta en
  el propio `tech-spec.md`
- Backlog de catálogo "casa completa" implementado en el orden del propio backlog:
  `ObtenerTiposCargadosPorCategoria`, `BuscarTiposDeMuroPorFuncion` + `tipoMuroId` en
  `CrearMurosMasivo` (tabiques), `CrearAberturasMasivo` (puertas/ventanas, host-based),
  `ColocarMobiliarioMasivo` (free-standing + rotación), `CrearTejadoExtrusion` y
  `CrearTejadoPorHuella`. **Antes de escribir los dos comandos de tejado** (la pieza que el propio
  backlog marcaba como más arriesgada, "firma exacta a verificar contra el NuGet, no asumir"): se
  montó un proyecto scratch con `System.Reflection.MetadataLoadContext` para reflexionar
  directamente sobre el DLL de metadatos de `Nice3point.Revit.Api.RevitAPI` 2026.4.10 (que no se
  puede cargar en tiempo de ejecución por ser solo-referencia — `Assembly.LoadFrom` normal falla) y
  confirmar las firmas reales de `NewExtrusionRoof`/`NewFootPrintRoof`/`NewReferencePlane` y que
  `DefinesSlope`/`SlopeAngle` son indexadores por `ModelCurve`, no propiedades únicas del tejado.
  Todo compiló a la primera contra esas firmas verificadas
- `RevitBridge.CadIngest` (proyecto nuevo, ADR-012): mismo patrón de verificación antes de escribir
  — un probe con el NuGet real de `ACadSharp` (cargable normalmente, no metadata-only) escribiendo
  y releyendo un DXF sintético confirmó `CadDocument`, `Header.InsUnits`, `Layers`, `Entities`,
  `LwPolyline.Vertices`/`Bulge`, `Insert.Block`→`BlockRecord`, y que `DwgReader` existe (soporte DWG
  real, no solo DXF). `CadDocumentReader`/`CadScaleCalibrator`/`CadGeometryExtractor` +
  `CadIngestTools.cs` (MCP, no pasan por el pipe). Teselado de arcos por bisección recursiva de
  bulge (`bulgeMitad = bulge/(1+sqrt(1+bulge²))`), no por el enfoque de centro/radio más propenso a
  error que se descartó a media escritura — verificado con un caso analítico exacto (semicírculo
  bulge=1, ápice esperado en un punto calculable a mano) además del round-trip contra el DXF
  sintético. Único bug real encontrado en esta ronda: `DxfReader` no liberaba el fichero tras leer
  (`Assembly.LoadFrom` normal no aplica aquí, era un `using` que faltaba) — lo cazó el primer
  `dotnet test`, no una revisión manual
- Skill `revit-bridge` actualizada: paso 0 (contexto denso, ADR-011), sección nueva de modelado
  desde CAD y desde PDF/imagen (anclaje de escala + muro de prueba antes del lote, ADR-013), tabla
  de aprobación con la creación masiva añadida. De paso, corregida una línea obsoleta que seguía
  mencionando "confiar 30 min" como opción real pese a que `DOCUMENTACION.md` §5.D.15 la descarta
  explícitamente desde antes de esta sesión — inconsistencia real que nadie había limpiado
- Verificación: Debug y Release limpios en cada punto de commit, `dotnet test` progresó 91→103 sin
  ninguna regresión. Nivel 1-2 únicamente en todo lo nuevo — nivel 3 (Revit vivo) pendiente en
  bloque para todo el backlog de esta sesión, en particular los dos comandos de tejado (geometría
  nunca antes construida en este proyecto) y `RevitBridge.CadIngest` contra un DWG real de terceros
- Qué falló o costó más de lo esperado: nada grave. El mayor coste de tiempo fue montar los dos
  probes de reflexión (`MetadataLoadContext` para el paquete solo-referencia de Revit,
  `Assembly.LoadFrom` normal para ACadSharp) en vez de escribir código contra firmas supuestas de
  memoria — deliberado, es exactamente lo que el propio backlog pedía para la pieza de tejados y lo
  que el borrador de ADR-012 pedía para el spike de CAD
- Aprendizaje: cuando una firma de API de un paquete de metadatos (`ref/` sin `lib/`, como
  `Nice3point.Revit.Api.*`) hay que verificarla sin poder ejecutar Revit,
  `System.Reflection.MetadataLoadContext` (con un resolver que incluya también las DLLs del propio
  runtime de .NET, no solo el paquete) permite reflexionar sobre las firmas reales sin necesidad de
  cargar el ensamblado de verdad — más barato y más fiable que iterar a base de errores de
  compilación hasta acertar la firma
- Checkpoint: `dev` con 6 commits nuevos de esta sesión, todos pusheados. Nada en vuelo. Pendiente
  explícito, no resuelto a propósito: rutas de familias para `doc.LoadFamily`, umbral N de
  aprobación para creación masiva, persistencia de perfil de mapeo CAD (`cad_save_mapping_profile`)
  y, sobre todo, **toda verificación en Revit vivo** de lo construido en esta sesión — es el
  siguiente paso real, no una formalidad

## [2026-08-18] Cierre de los 2 pendientes restantes + tablas de planificación + statusline (sesión sin el usuario delante)

- Agentes usados: `claude-code-guide` (verificación de capacidades reales de statusline/hooks/temas
  del CLI, no supuestas de memoria)
- Usuario pidió cerrar `CargarFamilia` y el umbral de aprobación (el DWG real quedó bloqueado en que
  el usuario aporte un fichero — no resoluble sin él), luego "seguir mejorando la biblioteca"
  (visualización interactiva, tablas de planificación, materiales desde PDF), y finalmente confirmó
  que se ausentaba por hoy y pidió seguir trabajando de forma autónoma "hasta que podamos" continuar
- `CargarFamilia(doc, rutaArchivo)`: firma real de `Document.LoadFamily` verificada por
  `MetadataLoadContext` antes de escribir código. `UmbralAprobacionCreacion` (Core): controlado
  solo por variable de entorno `REVITBRIDGE_UMBRAL_APROBACION_CREACION`, nunca por parámetro del
  comando, aplicado de forma centralizada a los 6 comandos de creación masiva/tejado
- Visualización interactiva y materiales desde PDF: **no necesitaban código** — composición de
  comandos ya existentes con capacidades que Claude ya tiene (Artifacts, visión). El único hueco
  real de UX era N aprobaciones sueltas al modificar N parámetros → `ModificarParametrosMasivo`,
  una sola aprobación agregada, reutilizando `PreexistingElementGuard`/`DeletionPreview`. El switch
  de `StorageType` de `ModificarParametro` se extrajo a `EstablecerValorParametro` compartido
- Tablas de planificación: `ObtenerCamposDisponiblesParaTabla` + `CrearTablaPlanificacion`. Firma de
  `ViewSchedule.CreateSchedule`/`ScheduleDefinition.AddField`/`SchedulableField.GetName` verificada
  por reflexión antes de codificar. La consulta de campos crea una tabla temporal dentro de una
  `Transaction` y hace `RollBack` en vez de `Commit` — la API de Revit no expone los campos
  disponibles de una categoría sin crear antes una `ViewSchedule` real, y así no se persiste nada
  visible para el usuario solo por preguntar
- `revit_api_knowledge.md` (global, `~/.claude/`) actualizado con lo aprendido hoy: técnica de
  `MetadataLoadContext` para verificar firmas de paquetes de solo-referencia sin Revit, API real de
  ACadSharp (DXF+DWG con un modelo, bulge, `Insert`/`BlockRecord`), teselado de bulge por bisección
  recursiva en vez de centro/radio, tabla de firmas de Revit 2026 confirmadas esta sesión — cumple
  la instrucción de `CLAUDE.md` global de actualizarlo al cierre de cada sesión de plugin
- **Indicador visual de RevitBridge en el CLI**: pedido del usuario ("cambiar colores o un iconito
  cuando el Bridge esté activo"). Antes de construir nada, se lanzó `claude-code-guide` para
  verificar capacidades reales en vez de suponerlas — confirmó que **no existe cambio de tema
  dinámico** en Claude Code hoy, que la statusline no ve qué MCP está conectado directamente, pero
  que un hook `PreToolUse` sí puede detectar el nombre completo de la herramienta
  (`mcp__<server>__<tool>`) y la statusline sí soporta ANSI+emoji. Solución de dos piezas, siguiendo
  el flujo completo de la skill `update-config` (construir → pipe-test → escribir JSON → validar →
  intentar probar en vivo):
  1. `~/.claude/revitbridge-statusline.ps1`: lee JSON de stdin, resuelve `model.id`/`cwd`, comprueba
     si `~/.claude/revitbridge_active.txt` existe y su timestamp es de los últimos 5 minutos: si sí,
     imprime `🏗 RevitBridge` en naranja/negrita + el directorio; si no, la statusline normal
     (directorio + modelo). `[Console]::OutputEncoding = UTF8` explícito para que el emoji no se
     corrompa al invocarse como subproceso no interactivo desde PowerShell 5.1 en Windows
  2. Hook `PreToolUse` en `~/.claude/settings.json`, matcher `mcp__RevitBridge__.*`, escribe el
     timestamp UTC actual al fichero de flag. Ventana de 5 min en vez de borrado inmediato por
     `PostToolUse` — evita parpadeo si hay huecos cortos entre llamadas dentro de la misma sesión
     de trabajo con Revit
  - Ambos componentes pipe-testeados directamente (JSON sintético por stdin → comando real), con
    resultado verificado byte a byte (emoji + códigos ANSI correctos, expiración a los 5 min
    confirmada con un flag de 10 min de antigüedad). `ConvertFrom-Json` sobre el `settings.json`
    final confirma JSON válido y la estructura exacta (equivalente a `jq -e` sin tener `jq`
    instalado)
  - **No se pudo probar en vivo dentro de Claude Code** (paso 6 de la skill): el matcher
    `mcp__RevitBridge__.*` no se puede disparar en esta sesión porque **el servidor MCP
    "RevitBridge" no está registrado todavía** en la config de Claude Code (`~/.claude.json` no
    tiene ninguna entrada `mcpServers` con ese nombre — confirmado por grep, no solo asumido).
    Hallazgo colateral real: toda la pasarela lleva construida desde hace días pero nunca se ha
    conectado a Claude Code como servidor MCP en esta máquina
  - Pendiente para mañana, en orden: (1) registrar el servidor —
    `claude mcp add RevitBridge -- "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\src\RevitBridge.Mcp\bin\Debug\net8.0\RevitBridge.Mcp.exe"`
    (el exe ya existe, confirmado); (2) con Revit abierto y el addin cargado, invocar cualquier
    herramienta `mcp__RevitBridge__*` y comprobar que la statusline cambia a "🏗 RevitBridge" en los
    ~5 s siguientes (`refreshInterval`); (3) si no cambia pese al registro correcto, es el caveat de
    la skill: el watcher de `~/.claude/` puede no estar observando ese directorio si no tenía un
    fichero de settings al arrancar la sesión — abrir `/hooks` una vez o reiniciar Claude Code
- Verificación: Debug y Release limpios, `dotnet test` 108/108 (sin tests nuevos posibles para
  `CrearTablaPlanificacion`/`ModificarParametrosMasivo`, capa de adaptador sin Revit). El indicador
  de CLI es config personal, no código de producto — no aplica "compila/testea", su verificación es
  el pipe-test descrito arriba (nivel equivalente a "verificado fuera de Revit") más la prueba en
  vivo pendiente
- Qué falló o costó más de lo esperado: nada técnicamente, pero el hallazgo de que el servidor MCP
  nunca se registró es relevante — significa que **nada de lo construido en esta sesión (ni en
  sesiones anteriores) se ha invocado nunca de verdad desde una conversación de Claude Code real**,
  solo compilado y, para el catálogo de comandos, corrido manualmente por `scratch/shoot.ps1`
  contra el named pipe directamente. Confirma que el primer paso de mañana no es solo "abrir Revit",
  es también "registrar el MCP server", paso que no estaba en ningún checklist previo de este log
- Aprendizaje: cuando una petición de UX/DX toca configuración de la propia herramienta (Claude Code
  CLI) en vez de código del proyecto, verificar las capacidades reales con `claude-code-guide` antes
  de construir nada es tan importante como verificar una firma de API de Revit con
  `MetadataLoadContext` — la disciplina es la misma, el dominio cambia. Y antes de dar por sentado
  que "el bridge está listo para usarse", comprobar que el servidor MCP está de verdad registrado en
  la config de Claude Code, no solo que el `.exe` compila
- Checkpoint: `dev` con 7 commits de esta sesión, todos pusheados. Config personal
  (`~/.claude/settings.json`, `~/.claude/revitbridge-statusline.ps1`,
  `~/.claude/revit_knowledge/revit_api_knowledge.md`) actualizada y verificada fuera de vivo. Nada
  en vuelo. Pendiente real para la próxima sesión: registrar el MCP server (primero de todo),
  después toda la verificación en Revit vivo acumulada de las últimas sesiones

## [2026-08-18] Guía visual (Artifact) + judge del sprint Tier 3 de hoy: 4 hallazgos bloqueantes corregidos

- Agentes usados: `judge` (lanzado en segundo plano al cierre del lote anterior, revisó el rango de
  commits del sprint Tier 3 de hoy contra DOCUMENTACION.md §5); el resto, directo en la conversación
  principal (guía Artifact + fixes)
- Petición del usuario: "quiero una ux con presentación de como funciona para que el usuario tenga
  una guía de como funciona el flujo y las acciones posibles también" — se construyó y publicó un
  Artifact HTML (paleta cianotipo/vellum, IBM Plex, ambos temas) con la arquitectura de dos procesos,
  la escalera query→command→compile→exec→rollback, las dos vías, las 18 salvaguardas y el catálogo
  completo (31 comandos + 8 herramientas MCP) con parámetros reales y badges de aprobación
- Veredicto del judge: CHANGES_REQUESTED, 4 hallazgos bloqueantes, todos corregidos antes de cerrar:
  1. `/commands` y el chequeo de arranque de duplicados solo escaneaban `RevitBridge.Utils`
     (`typeof(ComandoRevitAttribute).Assembly`), invisibilizando los 28+ comandos que viven en
     `RevitBridge.Addin` — el despacho de `/command` sí escaneaba ambos ensamblados, así que se
     podían invocar pero no descubrir. Invertía la precedencia commandset→Roslyn de CLAUDE.md: sin
     verlos en el catálogo, el modelo tendría que adivinar que existen o caer a Roslyn. Fix: los tres
     sitios (App.cs, `/commands`, `/command`) ahora leen `CommandCatalog.EnsambladosDelCatalogo`, una
     única lista compartida — no pueden volver a divergir
  2. `cad_extract_geometry` serializaba `SegmentoRecto` en PascalCase (P1x/P1y/...) sin política de
     nombres; `CrearMurosMasivo` deserializa a `Dictionary<string,double>` con claves en minúscula.
     `PropertyNameCaseInsensitive` solo afecta al binding sobre propiedades de un POCO, NO a las
     claves de un `Dictionary<string,TValue>` (se leen literales, comparación ordinal). El pipeline
     CAD→muros documentado en el propio comentario de `CadIngestTools` creaba 0 muros sin ningún
     error — exactamente el patrón "mapeo con nombres distintos falla en silencio" que ya está en
     `revit_api_knowledge.md` por el bug de Fachada Interactiva (2026-06-25), aquí en la frontera
     JSON en vez de en un DTO C#. Fix: `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
  3. `CrearMurosMasivo` no tenía try/catch por elemento (a diferencia de sus 3 hermanos ya
     corregidos hoy): un muro degenerado abortaba el `using(tx)` entero y el rollback implícito
     deshacía también los muros ya creados en la misma llamada
  4. `CrearForjadosMasivo` construía el `CurveLoop` (`Line.CreateBound`) FUERA del único `try` que
     envolvía solo `Floor.Create` — mismo modo de fallo que (3) para un polígono con vértices
     coincidentes
- Verificación: Debug y Release limpios, `dotnet test` 111/111. +4 tests: `CommandCatalogTests`
  (pertenencia del ensamblado Addin a `EnsambladosDelCatalogo`) y `CadIngestToolsTests` (casing de
  claves + JSON consumible por el mismo deserializador que usa `CrearMurosMasivo`, reproduciendo el
  bug exacto antes del fix)
- Qué falló o costó más de lo esperado: un test inicial (`Descubrir_Con_EnsambladosDelCatalogo_
  Encuentra_Comandos_Reales_Del_Addin`) intentaba invocar `Assembly.GetTypes()` sobre el ensamblado
  Addin real desde el proceso de test — falla con `ReflectionTypeLoadException` (`RevitAPI`/
  `RevitAPIUI` no están presentes en tiempo de ejecución fuera de Revit; los paquetes Nice3point son
  metadata-only para compilar, no runtime). Se descartó ese test y se dejó solo la comprobación de
  pertenencia a la lista (metadata pura, sin cargar tipos) — es un límite real de "verificado fuera
  de Revit" (nivel 2 de CLAUDE.md), no un defecto a forzar con un catch-all sin justificación en
  producción
- Aprendizaje: el mismo patrón de bug ("dos sitios que deberían ver lo mismo pero uno quedó
  desincronizado") apareció dos veces hoy en capas distintas — descubrimiento de comandos
  (reflexión) y formato de JSON (serialización) — reforzando que "comparar ambos lados de un
  contrato, no solo revisar cada lado por separado" es el chequeo de mayor valor para un `judge` en
  este proyecto. Y: `Assembly.GetTypes()` sobre `RevitBridge.Addin` es intrínsecamente nivel-3 (solo
  dentro de Revit) — cualquier test futuro que quiera ejercitarlo de verdad tendrá el mismo límite
- Checkpoint: `dev` con 8 commits de esta sesión, todos pusheados. Nada en vuelo.

## [2026-08-18] Vendorizar skill externa `/diagram-design`

- Agentes usados: ninguno, directo en la conversación principal
- Petición del usuario: preguntó si el estilo de diagramas de
  `github.com/cathrynlavery/diagram-design` serviría para este proyecto; tras la respuesta pidió
  instalarla, documentarla, verificarla y pushear
- Qué se hizo: clonado el repo a un temporal, comprobada la licencia (MIT, con iconos de terceros
  MIT/CC0 documentados en su propio `THIRD_PARTY_LICENSES.md`) y revisados los 3 scripts Python
  (`drawio_extract`, `mermaid_extract`, `self_check`) antes de traerlos al repo — sin
  `subprocess`/`eval`/`exec`/`os.system`, solo `urllib.parse` para validar que los assets no
  referencian URLs remotas (consistente con el diseño "sin dependencias externas" que anuncia la
  skill). Copiada íntegra a `.claude/skills/diagram-design/` (152 ficheros) + `LICENSE-UPSTREAM` +
  `THIRD_PARTY_LICENSES.md` de atribución. Documentada en `CLAUDE.md` como sección nueva, distinta
  de la tabla de las 4 skills ad-hoc de autoría propia — es vendorizada, no escrita para este
  proyecto, y es una capa de presentación (genera diagramas a partir de `DOCUMENTACION.md`/specs/
  `revit_api_knowledge.md`), no una fuente de verdad nueva
- Verificación: copia byte-idéntica confirmada por hash SHA256 contra los 152 ficheros del upstream
  (sin mismatches); la skill aparece en el listado de skills disponibles de esta misma sesión tras
  copiarla (prueba de que el `SKILL.md` tiene frontmatter válido y Claude Code la descubrió). No
  aplica "compila"/`dotnet test` — no es código de producto, es una skill de Markdown/HTML estático
- Qué falló o costó más de lo esperado: nada — el repo era autocontenido y sin dependencias raras,
  la due diligence (licencia + scripts) fue rápida porque el propio repo ya documenta su cadena de
  licencias de terceros en `THIRD_PARTY_LICENSES.md`
- Aprendizaje: al vendorizar código de un repo externo dentro de este repo (que es PÚBLICO), la
  secuencia mínima antes de `git add` es: licencia compatible con redistribución (MIT/CC0 aquí),
  lectura de cualquier script ejecutable en busca de red/`exec`/`subprocess`, y verificación de
  integridad por hash contra el origen — no solo copiar y confiar en que "es de GitHub, será seguro"
- Checkpoint: `dev` con 9 commits de esta sesión, todos pusheados. Nada en vuelo.
