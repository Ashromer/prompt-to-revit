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
