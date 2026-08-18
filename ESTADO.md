# Estado del proyecto — última actualización 2026-08-18 (fin de sesión 3 — Gate Fase 0 cumplido)

Fichero de relevo entre sesiones. Léelo antes de continuar. La verdad detallada está en
`specs/` y en el `plan.md`/`VEREDICTO.md` de cada PoC; esto es el mapa.

## Dónde está el trabajo ahora mismo

**Gate Fase 0: ✅ CUMPLIDO.** Los dos PoCs bloqueantes están cerrados en positivo y mergeados en
`dev`. `dev` local está sincronizado con `origin/dev`.

**Tier 1: ✅ CUMPLIDO Y ESTABILIZADO.** Se ha completado la conexión en vivo con Revit (queries y transactions).
Se detectaron y corrigieron crasheos críticos del proceso host (Revit) debidos a:
1. Falta de captura de excepciones en hilos secundarios (`PipeServer`) y manejadores de API (`ExecutionQueueEventHandler`).
2. Conflicto de carga de ensamblados por la versión de Roslyn (5.9.0 pedía `System.Collections.Immutable` v10, incompatible con Revit 2026 / .NET 8). Solucionado bajando la versión de `Microsoft.CodeAnalysis.CSharp` a la `4.8.0` y forzando la copia local de dependencias.

## PoC #1 — SDK oficial de MCP para .NET

Cerrado en sesiones anteriores. ADR-001 confirmado, `ModelContextProtocol` 2.2.0. Ver
`pocs/001-poc-1-sdk-oficial-de-mcp-para-net/VEREDICTO.md`. Sin cambios esta sesión.

## PoC #2 — Paquete NuGet de metadatos de la API de Revit — CERRADO, PR #6 MERGEADA

Esta sesión terminó lo que la anterior dejó a medias (Lote 4, tarea 2 en adelante):

- **Verificación en Revit vivo completada por el usuario**:
  `pocs/002-poc-2-paquete-nuget-metadatos-api-revit/GUION-VERIFICACION.md`, ya en la raíz del repo
  (mergeado). Build NuGet solo y build Local solo, cada uno con ribbon + tooltip + `TaskDialog`
  correctos. **Tabla de equivalencia: veredicto Equivalente.**
- **Hallazgo real, no bloqueante**: cargar los dos addins de *prueba* a la vez (no lo que pide
  Historia 2, que es cargar cada build por separado) produce un choque de nombre de panel
  (`ArgumentException: The panel with name ... already exists!`, el nombre de `RibbonPanel` es
  único por sesión de Revit, no por addin). No activa FR-009. Documentado en `GUION-VERIFICACION.md`
  §2 y `VEREDICTO.md` §3. **Corrección de una imprecisión de la sesión anterior**: `Captura.PNG`
  **no** es una captura de ese diálogo de error — es de un momento posterior (build Local ya en
  solitario, sin error). El texto de la excepción se anotó, pero no se conserva captura del
  diálogo. Esto se detectó y corrigió durante la revisión de `@judge` (ver abajo).
- **Cierre formal**: `VEREDICTO.md` (nuevo), `RECONOCIMIENTO.md` §15 y `plan.md` (Lotes 4-5
  completos) escritos por `@architect`. `specs/tech-spec.md` (ADR-008 confirmado, `TBD` de
  Referencias de la API resuelto, Discovery cerrado) y `specs/roadmap.md` (Gate Fase 0 CUMPLIDO)
  actualizados.
- **`@judge` revisó el cierre: CHANGES_REQUESTED en la primera pasada, dos bloqueantes** —
  (1) la cita de `Captura.PNG` como evidencia del diálogo de error, que la captura no sostiene
  (corregido en `VEREDICTO.md`, `GUION-VERIFICACION.md` y `RECONOCIMIENTO.md`); (2) el rescope de
  `Microsoft.CodeAnalysis.CSharp` de "pendiente del PoC #2" a **F1.2** en el Gate Fase 0, hecho sin
  dejar traza del cambio (corregido: traza explícita en `roadmap.md`, **ratificado por el usuario**
  el 2026-08-18). **PASS en la segunda pasada.**
- **PR #6 abierta contra `dev`, revisada (CI en verde, `MERGEABLE`, sin conflictos) y mergeada por
  el usuario**: https://github.com/Ashromer/prompt-to-revit/pull/6

**Paquete elegido, definitivo**: `Nice3point.Revit.Api.RevitAPI` + `Nice3point.Revit.Api.RevitAPIUI`,
versión exacta `[2026.4.10]`. Salvedad conocida y no bloqueante: el `.nupkg` redistribuye la DLL
original de Autodesk bajo licencia MIT del empaquetador, sin permiso de redistribución acreditado —
reevaluar si "distribución a terceros" pasa a ser objetivo con compromiso (`RECONOCIMIENTO.md` §12,
`VEREDICTO.md` §4).

**PoC #2 cerrado del todo. No queda tarea pendiente en su `plan.md`.**

## Otro trabajo pendiente

1. **Decidir con el usuario si procede PR de `dev` contra `main`.** Ambos PoCs de la Fase 0 están
   cerrados y mergeados en `dev`; ya no hay que esperar a nada de la Fase 0 para plantear esa PR.
   No decidido esta sesión — no se preguntó.
2. **Worktree del PoC #2** (`.worktrees\002-poc-2-paquete-nuget-metadatos-api-revit`,
   rama `feature/002-poc-2-paquete-nuget-metadatos-api-revit`) sigue existiendo tras el merge, sin
   limpiar. Puede borrarse (`git worktree remove` + borrar la rama remota, ya mergeada) cuando el
   usuario lo confirme; no se ha tocado por no ser una acción pedida.
3. **No arrancar Tier 0 sin que el usuario lo pida explícitamente** — instrucción de cierre de esta
   sesión.

## Deuda y cabos sueltos (heredados, sin cambios esta sesión)

- Los issues #1 a #5 no están enlazados en la tabla de seguimiento de `specs/roadmap.md` (celdas a
  `—`).
- `main` sigue protegido (`enforce_admins: true`, `allow_force_pushes: false`, PR obligatorio). El
  agente no puede tocar `gh api .../protection` (bloqueado por el clasificador de auto mode).
- La rama `feature/001-poc-sdk-mcp-net` no está publicada en GitHub (el merge a `dev` fue local).
- Hay cambios locales sin commitear en el repo raíz, **anteriores a esta sesión y no tocados**:
  `.claude/agents/architect.md`, `.claude/agents/judge.md` modificados, `optimizacion_tokens.md` sin
  trackear. Quien retome debe revisar qué son antes de decidir si se commitean.

## Cómo trabajar en este proyecto

- **Abrir la sesión en esta carpeta**, no en otra. Aquí cargan los agentes de `.claude/agents/` y las
  3 skills ad-hoc.
- El shell de esta máquina es **PowerShell**, no Bash. Los subagentes de código no tienen shell:
  el orquestador compila/ejecuta él mismo con PowerShell después de cada tarea de código.
- Acciones de red/GitHub sensibles (mergear PRs, tocar protección de ramas) están bloqueadas para el
  agente por el clasificador de auto mode — las ejecuta el usuario a mano, o con `!comando` si quiere
  que el agente vea el resultado en la conversación.
- `AGENTS.md` y `CLAUDE.md` mandan sobre convenciones. Si `DOCUMENTACION.md` y un ADR del TechSpec
  divergen, **gana el ADR**.
