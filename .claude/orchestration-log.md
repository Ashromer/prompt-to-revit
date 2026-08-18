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
