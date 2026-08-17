# Estado del proyecto — última actualización 2026-08-17 (fin de sesión 2 — PoC #2 a medias, 12/16)

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

## PoC #2 — Paquete NuGet de metadatos de la API de Revit — PAUSADO A MEDIAS, Lote 4 en curso

Especificado (`requirements.md`, 6 gaps cerrados vía `/aisy.clarify-feature`) y planificado
(`plan.md`, 5 Lotes, 16 tareas) en esta sesión. Ejecutado vía `/aisy.implement-feature` en un
worktree dedicado:

- Worktree: `D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\002-poc-2-paquete-nuget-metadatos-api-revit`
- Rama: `feature/002-poc-2-paquete-nuget-metadatos-api-revit`, **publicada en GitHub** y al día
  (último commit `3a2dc8b`, sin nada pendiente de push en el worktree).
- **Progreso: 12/16 tareas** (Lotes 1, 2 y 3 cerrados y commiteados; Lote 4 con su tarea 1 hecha,
  tarea 2 a medias). Detalle y evidencia de cada tarea, con su **Resultado** anotado, en
  `specs/002-poc-2-paquete-nuget-metadatos-api-revit/plan.md` dentro del worktree (el `plan.md` de
  este PoC vive solo ahí, no en la raíz del repo — se copió al worktree al arrancar la ejecución).

**Lo que ya quedó demostrado con evidencia real** (no solo en esta máquina):
- Paquete elegido: `Nice3point.Revit.Api.RevitAPI` + `.RevitAPIUI`, versión exacta `[2026.4.10]`
  (razón completa en `RECONOCIMIENTO.md` del PoC, dentro del worktree).
- **CI en verde en GitHub Actions**, runner `windows-latest` sin Revit instalado:
  https://github.com/Ashromer/prompt-to-revit/actions/runs/32069521493 — compila Debug y Release,
  tests pasan. Esto prueba Historia 1 e Historia 3 con evidencia real, no solo "compiló en mi
  máquina" (esta máquina tiene Revit instalado y por sí sola no puede probarlo).
- `dotnet list package` confirma `RevitAPI`/`RevitAPIUI` resueltos — SC-003 cumplido.

**Dónde se quedó exactamente (Lote 4, tarea 2 — verificación en Revit vivo, la ejecuta el
usuario)**: el usuario siguió el `GUION-VERIFICACION.md` hasta A2.2 inclusive.

- A1.1-A1.4 (build NuGet solo): **todo correcto**, botón visible, diálogo esperado, sin error.
- A2.1 (copiar también el build Local, sin quitar el NuGet): **correcto**.
- A2.2 (reabrir Revit con los dos addins a la vez): **falló** —
  `Autodesk.Revit.Exceptions.ArgumentException: The panel with name PoC #2 NuGet vs Local already
  exists!`, con traza en `PocRevitAddin.App.OnStartup`. Captura del usuario guardada en
  `pocs/002-poc-2-paquete-nuget-metadatos-api-revit/Captura.PNG` (worktree).
- **Diagnóstico (ya documentado en el propio guion, bloque HALLAZGO tras A2.1)**: no es un fallo
  del PoC ni activa FR-009. Los dos addins registran un panel con el mismo nombre
  (`"PoC #2 NuGet vs Local"`) desde ensamblados distintos, y Revit no lo permite — el nombre de
  panel es único por pestaña, no por addin (el supuesto de `revit-developer` de que convivirían
  como paneles separados era incorrecto). Es un defecto del propio diseño del guion de
  verificación (probar ambos addins cargados simultáneamente), no de la hipótesis del PoC
  (ADR-008: referenciar la API por paquete NuGet de metadatos).
- **Procedimiento de aislamiento ya escrito en el guion** (bloque HALLAZGO, tras A2.1): mover
  temporalmente `PocRevitAddin.Nuget.addin` fuera de la carpeta de Addins (con
  `Move-Item ... .addin .bak`), reabrir Revit con solo el build Local cargado, completar A2.4/A2.5
  sobre el botón Local en solitario, y luego restaurar el `.addin` del NuGet antes de la sección 3
  (comandos `Move-Item` exactos ya están en el guion, copiables tal cual).

**Siguiente paso exacto al retomar**: pedirle al usuario que ejecute el procedimiento de
aislamiento del bloque HALLAZGO (mover el `.addin` del NuGet, reabrir Revit, completar A2.4/A2.5
del build Local, restaurar el `.addin`), luego seguir con la sección 3 (criterio de equivalencia)
y el resto del guion (§4 no aplica salvo que aparezca un fallo nuevo, §5 solo si algo más falla, §6
limpieza). Con eso cerrado, Lote 4 tarea 2 (`@tester` recoge y diagnostica lo anotado) y todo el
Lote 5 (veredicto, TechSpec, roadmap, `@judge`) — pendientes, plan.md los tiene detallados.

**Aviso de proceso para quien retome**: los agentes de código (`revit-developer`,
`test-developer`, `code-developer`) **no tienen shell en esta sesión** — solo escriben ficheros.
El orquestador debe compilar/ejecutar él mismo con PowerShell después de cada tarea de código, no
asumir que el agente lo hizo. Ya pasó una vez en el Lote 2 (quedó `BLOCKED` y hubo que compilar a
mano) — evitarlo dando esta instrucción por adelantado en el prompt de la tarea, como se hizo desde
la tarea 3 en adelante.

## Otro trabajo pendiente

1. Decidir con el usuario si procede PR de `dev` contra `main` para el PoC #1 (ya cerrado, ya
   publicado en `origin/dev`), o si se espera a tener también el PoC #2 cerrado antes de abrirlo.

## Deuda y cabos sueltos

- **Los issues #1 a #5 no están enlazados** en la tabla de seguimiento de `specs/roadmap.md` (celdas
  a `—`). Commit en `dev`, nunca en `main`.
- ~~Dos commits con el email personal del usuario en el historial público~~ **RESUELTO en esta
  sesión (2026-08-17)**: `origin/dev` y `origin/main` reescritos. `origin/dev` ahora en `b7bf4fb`
  (historia completa, con `0761a97`/`b94093a` de email corregido en la base). `origin/main` solo en
  `b94093a` (los dos commits base corregidos, sin arrastrar el trabajo del PoC #1 — eso entra por
  PR). Se necesitaron dos reglas de protección de `main` levantadas temporalmente
  (`allow_force_pushes` y `required_pull_request_reviews`, ambas restauradas exactamente al estado
  original tras el push). El agente no puede tocar `gh api .../protection` (bloqueado por el
  clasificador de auto mode): lo ejecutó el usuario a mano cada vez.
- `main` sigue protegido igual que antes (`enforce_admins: true`, `allow_force_pushes: false`, PR
  obligatorio). Cualquier reescritura futura necesita repetir el mismo procedimiento manual.
- La rama `feature/001-poc-sdk-mcp-net` no está publicada en GitHub (el merge a `dev` fue local).

## Cómo trabajar en este proyecto

- **Abrir la sesión en esta carpeta**, no en otra. Aquí cargan los 8 agentes de `.claude/agents/` —
  incluidos `revit-developer` y `mcp-developer`, propios del proyecto — y las 3 skills ad-hoc. En
  sesiones abiertas desde otra carpeta esos agentes no existen y hay que suplirlos a mano.
- El shell de esta máquina es **PowerShell**, no Bash. Las definiciones de agente que declaran `Bash`
  se quedan sin ejecutor: los subagentes escriben, y la compilación y las pruebas las corre el
  orquestador.
- `AGENTS.md` y `CLAUDE.md` mandan sobre convenciones. Si `DOCUMENTACION.md` y un ADR del TechSpec
  divergen, **gana el ADR**.
