# Estado del proyecto — última actualización 2026-08-17 (sesión 2, en curso — PoC #2 a medias)

Fichero de relevo entre sesiones. Léelo antes de continuar. La verdad detallada está en
`specs/` y en el `plan.md` del PoC; esto es el mapa.

## Dónde está el trabajo ahora mismo

**Corrección sobre la versión anterior de este fichero**: el merge ya ocurrió. `feature/001-poc-sdk-mcp-net`
se fusionó en `dev` en el commit `088db80`, **en local**; el código del PoC ya vive en
`pocs/001-poc-1-sdk-oficial-de-mcp-para-net/` en la raíz del repo, no solo en el worktree. El worktree
(`.worktrees\001-poc-sdk-mcp-net`, rama `feature/001-poc-sdk-mcp-net`) sigue existiendo pero ya no es
la única copia. `dev` tiene commits que **no están en GitHub todavía** (`git status`: "ahead of
'origin/dev' by N commits").

**Hay un segundo worktree activo**, para el PoC #2, creado en esta misma sesión — ver la sección
"PoC #2 — EN EJECUCIÓN AHORA MISMO" más abajo, es lo más importante de este fichero ahora mismo.

## PoC #1 — SDK oficial de MCP para .NET

Plan y estado por tarea: `specs/001-poc-1-sdk-oficial-de-mcp-para-net/plan.md` (casillas marcadas).

| Lote | Estado |
|---|---|
| 1 — Reconocimiento | cerrado (2/2) |
| 2 — El experimento | cerrado (6/6) |
| 3 — Verificación | cerrado (2/2) |
| 4 — Veredicto y cierre | **cerrado (4/4)** |

**Corrección de un error de esta misma sección en la versión anterior**: la tabla de métodos JSON-RPC
más abajo decía que `initialize` se vio "0 veces" y lo dejaba como pregunta abierta. Es falso: el log
completo (ya en el commit `cf8cd25`, seis minutos antes de este fichero) muestra `initialize` **6
veces**, en las sesiones más tempranas. A partir de cierto punto el cliente cambia a `server/discover`
+ `subscriptions/listen`. No es un misterio: es el riesgo R4 que ya anticipaba `DECISION-PELDANO.md`
§6. Detalle completo en `pocs/001-poc-1-sdk-oficial-de-mcp-para-net/VEREDICTO.md` §3.

**Veredicto: PELDAÑO 1. ADR-001 confirmado.** `ModelContextProtocol` 2.2.0, `Microsoft.Extensions.Hosting`
8.0.0. Los cuatro criterios (SC-001, SC-002, SC-003, publicación/arranque) cumplidos con evidencia real
anotada por el usuario en `GUION-VERIFICACION.md`. `specs/tech-spec.md` (ADR-001, Tech Stack,
Dependencies, Discovery), `specs/roadmap.md` (Gate Fase 0) y el `requirements.md` del PoC (FR-011,
FR-012, Status) actualizados.

**`@judge` revisó `VEREDICTO.md`: CHANGES_REQUESTED en la primera pasada, ya corregido.** El veredicto
de fondo (peldaño 1) quedó confirmado como bien fundado; los dos bloqueantes eran un recuento erróneo
de `tools/call` en la instrumentación (7→9, mezclaba dos métodos de conteo) y que `requirements.md`
seguía pidiendo "comparación carácter por carácter" para SC-003 cuando lo verificado fue la presencia
de los dos marcadores (el string sufre doble escapado en el JSON-RPC). Ambos corregidos en
`VEREDICTO.md` y `requirements.md`; el "misterio" del `initialize` que corregía este mismo fichero en
la versión anterior fue confirmado exacto por `@judge`, contando a mano. Detalle completo del veredicto
y de la revisión en `pocs/001-poc-1-sdk-oficial-de-mcp-para-net/VEREDICTO.md`.

**PoC #1 cerrado del todo.** Siguiente: PoC #2.

### Lección para el Tier 0, a registrar en el TechSpec

De las seis tareas de código del Lote 2, **cuatro tenían un defecto que solo se veía ejecutando**, y
los cuatro eran el mismo patrón: *algo correcto que parecía roto*.

1. Marcador de traza con `<` y `>`, que `System.Text.Json` escapa a `\u003C`. La traza llegaba
   entera pero la comprobación por marcador literal fallaba.
2. Recuento de la instrumentación que dependía de `Dispose()`, que no se ejecuta ni cerrando stdin
   ordenadamente. No se escribía nunca.
3. Arranque en frío del ejecutable autocontenido (73,6 MB) que no responde en 4 s y se ve igual que
   un servidor muerto. Claude Code tiene `MCP_TIMEOUT`.
4. El propio guion de verificación afirmaba que el ejecutable arranca «sin escribir nada en
   pantalla». Escribe ocho líneas `info:` por stderr, que son la prueba de que arrancó.

Sin corregirlos, el veredicto habría sido *«el SDK oficial no sirve»* y se habría cambiado de
lenguaje sobre una premisa falsa. **Conclusión operativa: en el Tier 0, quien escribe el código no
puede ser quien lo verifica.**

## PoC #2 — Paquete NuGet de metadatos de la API de Revit — EN EJECUCIÓN AHORA MISMO

**Estado del pipeline de specs**: `requirements.md` generado y con sus 6 gaps ya cerrados vía
`/aisy.clarify-feature` (decisiones: reconocimiento previo del paquete como el Lote 1 del PoC #1;
CI en GitHub Actions; evidencia de equivalencia = confirmación manual anotada por el usuario en
Revit vivo; tests del addin trivial se crean en este PoC, no existen previos; falta de cobertura de
ensamblados extra no bloquea el veredicto). `plan.md` generado vía `/aisy.plan-feature` con **5
Lotes, 16 tareas** (ver tabla de agentes más abajo). Ninguna tarea del plan lleva tag `@human`, pero
las tareas del Lote 4 (verificación en Revit vivo) requieren que **el usuario** ejecute a mano el
`GUION-VERIFICACION.md` que preparará `@tester` — hasta que eso pase, esa tarea se bloqueará sola en
el loop de `/aisy.implement-feature` (comportamiento esperado, no es un fallo).

**Ejecución (`/aisy.implement-feature`) en marcha**:

- Worktree: `D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\002-poc-2-paquete-nuget-metadatos-api-revit`
- Rama: `feature/002-poc-2-paquete-nuget-metadatos-api-revit` (creada desde `dev`, commit `088db80`)
- Modo: secuencial, plan único (no hubo que preguntar paralelo/secuencial)
- **Última tarea disparada**: Lote 1, tarea 1 ("Identificar el paquete NuGet candidato"), agente
  `@architect` en background, **sin resultado recibido todavía en la sesión que escribió esto**. Si
  retomas y no ha llegado notificación, hay que asumir que el agente se perdió con el cierre de sesión
  y **relanzarlo desde cero** (no hay forma de reconectar a un agente en background de una sesión ya
  cerrada). El prompt exacto que se le dio está en el propio `plan.md`, tarea 1 del Lote 1 — basta con
  volver a montar el mismo prompt (task + batch + contexto del plan + working directory = la ruta del
  worktree de arriba) y volver a lanzarlo.
- **`plan.md` sigue con las 16 tareas en `- [ ]`**: no se ha marcado ninguna todavía porque la tarea 1
  no había terminado. No hay commits en la rama del worktree.
- Progreso real: **0/16 tareas confirmadas completas.**

**Cómo retomar**: entra en el repo (no hace falta `cd` al worktree para orquestar, el propio
`/aisy.implement-feature` ya sabe trabajar contra la ruta del worktree), invoca de nuevo
`/aisy.implement-feature`, selecciona el plan de PoC #2 (ya tiene el worktree y la rama creados —
la skill debería detectarlos y reusarlos; si `git worktree add` falla porque ya existen, es la razón,
no un error real) y sigue el loop de tareas desde la 1.

## Otro trabajo pendiente

1. Decidir con el usuario si procede PR de `dev` contra `main` para el PoC #1 (ya cerrado), o si se
   espera a tener también el PoC #2 cerrado antes de tocar `main`.

## Deuda y cabos sueltos

- **Los issues #1 a #5 no están enlazados** en la tabla de seguimiento de `specs/roadmap.md` (celdas
  a `—`). Commit en `dev`, nunca en `main`.
- **Dos commits con el email personal del usuario** en el historial público: `e255255` y `53728a9`.
  Los posteriores usan `Ashromer@users.noreply.github.com`. Reescribirlos exige levantar la
  protección de `main`, `filter-branch`, force-push y restaurarla.
- `main` está protegido con `enforce_admins: true` y `allow_force_pushes: false`: bloquea también al
  propietario. Cualquier reescritura necesita levantar la protección temporalmente.
- La rama `feature/001-poc-sdk-mcp-net` no está publicada en GitHub.

## Cómo trabajar en este proyecto

- **Abrir la sesión en esta carpeta**, no en otra. Aquí cargan los 8 agentes de `.claude/agents/` —
  incluidos `revit-developer` y `mcp-developer`, propios del proyecto — y las 3 skills ad-hoc. En
  sesiones abiertas desde otra carpeta esos agentes no existen y hay que suplirlos a mano.
- El shell de esta máquina es **PowerShell**, no Bash. Las definiciones de agente que declaran `Bash`
  se quedan sin ejecutor: los subagentes escriben, y la compilación y las pruebas las corre el
  orquestador.
- `AGENTS.md` y `CLAUDE.md` mandan sobre convenciones. Si `DOCUMENTACION.md` y un ADR del TechSpec
  divergen, **gana el ADR**.
