# Estado del proyecto — última actualización 2026-08-17

Fichero de relevo entre sesiones. Léelo antes de continuar. La verdad detallada está en
`specs/` y en el `plan.md` del PoC; esto es el mapa.

## Dónde está el trabajo ahora mismo

El PoC #1 se desarrolla en un **worktree**, no en esta carpeta raíz:

```
D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\001-poc-sdk-mcp-net
rama: feature/001-poc-sdk-mcp-net
```

Esa rama tiene 4 commits que **no están en GitHub todavía**. La raíz (`dev` / `main`) no contiene
los ficheros del PoC: si buscas `pocs/` aquí y no aparece, es por eso.

## PoC #1 — SDK oficial de MCP para .NET

Plan y estado por tarea: `specs/001-poc-1-sdk-oficial-de-mcp-para-net/plan.md` (casillas marcadas).

| Lote | Estado |
|---|---|
| 1 — Reconocimiento | cerrado (2/2) |
| 2 — El experimento | cerrado (6/6) |
| 3 — Verificación | cerrado (2/2), ejecutado por el usuario |
| 4 — Veredicto y cierre | **pendiente (0/4)** ← siguiente trabajo |

### Veredicto empírico: PELDAÑO 1. ADR-001 confirmado.

`ModelContextProtocol` **2.2.0** estable. `Microsoft.Extensions.Hosting` **8.0.0**.
Los tres criterios verificados por el usuario en una sesión real de Claude Code, con evidencia
pegada en `pocs/001-poc-1-sdk-oficial-de-mcp-para-net/GUION-VERIFICACION.md`:

- **SC-001** cumplido. Claude describió `mensaje` string obligatorio y `repeticiones` integer
  obligatorio, con las descripciones de los atributos `[Description]`. El esquema tipado llega
  completo, incluido `required`.
- **SC-002** cumplido en los tres casos. Los valores vuelven literales.
- **SC-003** cumplido. Traza íntegra con los dos marcadores, cadena de `InnerException` completa.
  El doble escapado no es un problema en la práctica: Claude Code la entrega decodificada.

### Hallazgo que cambia una decisión

La instrumentación (`%LOCALAPPDATA%\PocMcpSdk\rpc-methods.log`) registró que Claude Code, en tres
sesiones, invocó:

| Método | Veces | ¿Estándar MCP? |
|---|---|---|
| `tools/call` | 4 | sí |
| `tools/list` | 3 | sí |
| `server/discover` | 3 | **no** |
| `subscriptions/listen` | 3 | **no** |
| `initialize` | **0** | sí, y obligatorio por especificación |

**Consecuencia:** el peldaño 2 (implementar MCP a mano) es más caro de lo que lo tasó el plan. No
bastaría con `initialize` + `tools/list` + `tools/call`: habría que reproducir métodos propios del
cliente que no están en ninguna especificación pública y que pueden cambiar sin aviso. Refuerza el
peldaño 1 más que cualquier criterio en verde.

**Cuestión abierta, no resuelta:** por qué no aparece `initialize`. Dos explicaciones posibles y no
se pueden distinguir con los datos actuales: o Claude Code usa un saludo propio, o la instrumentación
no captura ese camino. El instrumento *sí* captura `initialize` en pruebas manuales por stdin, lo que
apunta a la primera, pero apuntar no es saber. Resolver en el Tier 0.

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

## Lote 4 — lo que falta hacer

1. `@architect` — escribir el veredicto en la carpeta del PoC.
2. `@architect` — TechSpec: sustituir los `TBD` del SDK por `ModelContextProtocol` 2.2.0, cerrar el
   Discovery bloqueante, registrar la escalera de 3 peldaños en ADR-001.
3. `@architect` — roadmap: marcar los criterios del PoC #1 para que el Gate Fase 0 sea evaluable.
   Y enmendar FR-011 y FR-012 del `requirements.md` con la escalera (sustituyen al «suspenso = Node»).
4. `@judge` — revisar que ningún criterio se declare cumplido sin evidencia confirmada por el usuario.

Después: PR de `feature/001-poc-sdk-mcp-net` contra `dev`, y **PoC #2** (paquete NuGet de metadatos
de la API de Revit, issue #2) que sigue sin especificar. El Gate Fase 0 exige los dos PoCs cerrados.

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
