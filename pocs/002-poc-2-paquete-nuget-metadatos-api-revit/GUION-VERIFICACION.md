# Guion de verificación — PoC #2 Paquete NuGet de metadatos de la API de Revit

Checklist para ejecutar a mano, en una sesión real de Revit 2026. Es el único camino por el que
SC-002 puede darse por cumplido (FR-005): **ningún agente puede abrir Revit ni confirmar esto**,
ni parcial ni provisionalmente. Este documento solo prepara los pasos y deja hueco para lo que
observes; los campos `Observado:` se rellenan a mano, con lo que veas literalmente (pega texto o
capturas si hace falta) — no marques nada como OK de memoria, y no los rellenes "por adelantado"
asumiendo el resultado esperado.

Lo que ya está verificado sin Revit (Debug + Release limpios de los dos builds, la suite de tests
del PoC, el contenido del paquete NuGet) está en `RECONOCIMIENTO.md` y en los logs de compilación
del Lote 2/3, y no se repite aquí. Este guion cubre solo lo que falta: que **el DLL compilado
contra el paquete NuGet se comporte en Revit igual que el compilado contra las DLL locales**
(Historia 2, requirements.md), con el resultado real de FR-009 si alguno de los dos no carga.

---

## 0. Preparación

**P0 — Rutas de este worktree** (ajusta si verificas desde otro). Ejecuta esto primero en tu
PowerShell — define `$BASE` como variable real; los bloques de código de las secciones
siguientes lo usan tal cual:

```powershell
$BASE = "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\002-poc-2-paquete-nuget-metadatos-api-revit\pocs\002-poc-2-paquete-nuget-metadatos-api-revit"
```

- Build NuGet: `$BASE\PocRevitAddin.Nuget\bin\Release\net8.0-windows\PocRevitAddin.Nuget.dll`
  (+ `PocRevitAddin.Nuget.addin` en `$BASE\PocRevitAddin.Nuget\`)
- Build Local: `$BASE\PocRevitAddin.Local\bin\Release\net8.0-windows\PocRevitAddin.Local.dll`
  (+ `PocRevitAddin.Local.addin` en `$BASE\PocRevitAddin.Local\`)
- Carpeta de registro: `%APPDATA%\Autodesk\Revit\Addins\2026\`
  (ruta completa en esta máquina: `C:\Users\Usuario\AppData\Roaming\Autodesk\Revit\Addins\2026\`)

Los dos builds ya están compilados en Debug y Release (verificado por el agente `revit-developer` /
`tester` antes de este lote, nivel "Compila" de `CLAUDE.md`). Este guion usa el build **Release**
de cada uno; no hace falta recompilar.

**P1 — Revit CERRADO antes de tocar la carpeta de Addins.** Por la regla de `CLAUDE.md`
("el DLL del addin queda bloqueado con Revit abierto"): si Revit está abierto, ciérralo por
completo (comprueba que no queda un proceso `Revit.exe` colgado en el Administrador de tareas)
antes de copiar o sustituir nada en `%APPDATA%\Autodesk\Revit\Addins\2026\`. Si copias con Revit
abierto, la copia puede fallar en silencio o el `.dll` viejo seguir cargado en la sesión ya
abierta — cualquiera de los dos te haría desconfiar de un resultado que en realidad es un
artefacto del propio procedimiento, no del PoC.

**P2 — Carpeta de Addins limpia de intentos anteriores de este PoC.** Antes de empezar, comprueba
si ya existen `PocRevitAddin.Nuget.*` o `PocRevitAddin.Local.*` en
`%APPDATA%\Autodesk\Revit\Addins\2026\` de una verificación previa a medias, y bórralos primero.
Así el paso 1 arranca desde un estado conocido (solo el build NuGet presente).

`Observado:` ______________________________________________

---

## 1. Build NuGet — Historia 2, Acceptance Scenarios 1 y 2

**A1.1 — Copiar el `.dll` y el `.addin` del build NuGet** (con Revit cerrado, P1):

```powershell
Copy-Item "$BASE\PocRevitAddin.Nuget\bin\Release\net8.0-windows\PocRevitAddin.Nuget.dll" "$env:APPDATA\Autodesk\Revit\Addins\2026\" -Force
Copy-Item "$BASE\PocRevitAddin.Nuget\bin\Release\net8.0-windows\PocRevitAddin.Nuget.pdb" "$env:APPDATA\Autodesk\Revit\Addins\2026\" -Force
Copy-Item "$BASE\PocRevitAddin.Nuget\PocRevitAddin.Nuget.addin" "$env:APPDATA\Autodesk\Revit\Addins\2026\" -Force
```

Resultado esperado: los tres ficheros aparecen en `%APPDATA%\Autodesk\Revit\Addins\2026\` sin
error de copia (no debe haber ningún otro `.addin` de este PoC todavía — eso es el Local, va en
la sección 2).

`Observado:` ______________________________________________

**A1.2 — Abrir Revit 2026** desde el icono/acceso normal (no hace falta abrir ningún modelo,
basta con la pantalla de inicio o un proyecto en blanco).

Resultado esperado: Revit arranca sin ningún diálogo de error de carga de addin ("A problem
occurred while loading a plugin" / similar). Si aparece un diálogo así con el nombre
`PocRevitAddin.Nuget`, no sigas con esta sección: ve directo a la sección 4 (edge case de carga).

`Observado:` ______________________________________________

**A1.3 — Confirmar que el botón aparece en el ribbon** (Historia 2, Acceptance Scenario 1).

Busca la pestaña/ribbon donde Revit coloca los paneles de add-ins (normalmente la pestaña
"Add-Ins" en la UI en inglés de esta instalación) y localiza el panel `PoC #2 NuGet vs Local` con
un botón de dos líneas: `PoC #2` / `PocRevitAddin.Nuget`.

Resultado esperado: el panel `PoC #2 NuGet vs Local` existe y contiene exactamente un botón
etiquetado `PoC #2` / `PocRevitAddin.Nuget`. Pasa el ratón por encima: el tooltip debe mencionar
"paquete NuGet de metadatos" y terminar en `Assembly: PocRevitAddin.Nuget`.

`Observado:` ______________________________________________

**A1.4 — Pulsar el botón y confirmar que ejecuta la acción esperada sin error** (Historia 2,
Acceptance Scenario 2).

Resultado esperado: aparece un `TaskDialog` con título **"PoC #2 - Paquete NuGet de metadatos"**
y cuerpo:

```
El botón del addin PoC #2 se ha ejecutado correctamente.
Build: PocRevitAddin.Nuget
```

Ningún error, ninguna excepción no controlada de Revit, ningún cuelgue. Cierra el diálogo con
Aceptar.

`Observado:` ______________________________________________

Si A1.2, A1.3 o A1.4 no salen como se espera, anota el fallo tal cual (texto exacto del error,
captura si hace falta) y ve a la sección 4 o 5 según corresponda antes de continuar con el build
Local — no tiene sentido comparar contra un Local "bueno" si el NuGet ya falló de forma clara.

---

## 2. Build Local — Acceptance Scenario 3, y observación del panel duplicado

**No cierres Revit todavía.** Este bloque añade el segundo addin a la misma sesión para poder
comparar ambos botones en el mismo arranque y, de paso, observar cómo se comporta el ribbon con
dos addins que registran un panel con el mismo nombre desde dos ensamblados distintos (pregunta
abierta señalada por `revit-developer`, sin verificar hasta ahora).

**A2.1 — Cerrar Revit** (P1: hay que copiar un `.dll` nuevo, no se puede con Revit abierto) y
copiar el `.dll` y el `.addin` del build Local, **sin borrar los del build NuGet** copiados en la
sección 1:

```powershell
Copy-Item "$BASE\PocRevitAddin.Local\bin\Release\net8.0-windows\PocRevitAddin.Local.dll" "$env:APPDATA\Autodesk\Revit\Addins\2026\" -Force
Copy-Item "$BASE\PocRevitAddin.Local\bin\Release\net8.0-windows\PocRevitAddin.Local.pdb" "$env:APPDATA\Autodesk\Revit\Addins\2026\" -Force
Copy-Item "$BASE\PocRevitAddin.Local\PocRevitAddin.Local.addin" "$env:APPDATA\Autodesk\Revit\Addins\2026\" -Force
```

Resultado esperado: ahora `%APPDATA%\Autodesk\Revit\Addins\2026\` contiene **los dos** juegos de
ficheros (`PocRevitAddin.Nuget.*` y `PocRevitAddin.Local.*`) a la vez.

`Observado:` ______________________________________________

**A2.2 — Reabrir Revit 2026.**

Resultado esperado: arranca sin diálogo de error de carga para ninguno de los dos addins. Si
aparece un diálogo de error mencionando `PocRevitAddin.Local` (y no el NuGet, que ya pasó la
sección 1), ve a la sección 4 — ese es exactamente el caso interesante para el diagnóstico, aunque
aquí sería al revés de lo esperado (el Local es la referencia de comparación y en teoría no
debería fallar nunca por versión de metadatos, solo por otras causas del entorno).

`Observado:` ______________________________________________

**A2.3 — Observación dedicada: ¿un panel o dos?** Antes de mirar el botón Local en concreto,
fíjate en la zona del ribbon donde apareció el panel en la sección 1.

Anota exactamente uno de estos tres casos (no hay resultado "esperado" predefinido aquí —
es una pregunta abierta, cualquiera de los tres es información válida):

- (a) Aparecen **dos paneles separados**, ambos titulados `PoC #2 NuGet vs Local`, cada uno con
  un único botón (uno con `PocRevitAddin.Nuget`, el otro con `PocRevitAddin.Local`).
- (b) Aparece **un solo panel** `PoC #2 NuGet vs Local` con **los dos botones dentro**.
- (c) Cualquier otro comportamiento (uno de los dos botones no aparece, Revit fusiona algo de
  forma inesperada, aparece un error nuevo que no salió en la sección 1, etc.) — descríbelo tal
  cual, con captura si es posible.

`Observado:` ______________________________________________

**A2.4 — Confirmar el botón Local: ribbon** (mismo criterio que A1.3, ahora sobre el botón
`PoC #2` / `PocRevitAddin.Local`, dondequiera que haya aparecido según A2.3).

Resultado esperado: existe un botón etiquetado `PoC #2` / `PocRevitAddin.Local`, con tooltip que
menciona "DLL locales de Revit 2026" y termina en `Assembly: PocRevitAddin.Local`.

`Observado:` ______________________________________________

**A2.5 — Pulsar el botón Local y confirmar que ejecuta la acción esperada sin error**
(Acceptance Scenario 3: repetir exactamente el mismo procedimiento de carga y pulsación que en
la sección 1).

Resultado esperado: `TaskDialog` con el mismo título **"PoC #2 - Paquete NuGet de metadatos"** y
cuerpo:

```
El botón del addin PoC #2 se ha ejecutado correctamente.
Build: PocRevitAddin.Local
```

`Observado:` ______________________________________________

---

## 3. Criterio de equivalencia (compara lo anotado en 1 y 2)

SC-002 exige que el comportamiento sea "indistinguible" entre los dos builds. Criterio explícito,
verifícalo campo por campo con lo que anotaste arriba — no de memoria:

| Campo | NuGet (sección 1) | Local (sección 2) | ¿Coincide? |
|---|---|---|---|
| Revit arranca sin diálogo de error | A1.2 | A2.2 | |
| Panel/botón visible en el ribbon | A1.3 | A2.4 | |
| Título del `TaskDialog` | A1.4 | A2.5 | debe ser **idéntico**: `PoC #2 - Paquete NuGet de metadatos` |
| Primera línea del cuerpo del `TaskDialog` | A1.4 | A2.5 | debe ser **idéntica**: `El botón del addin PoC #2 se ha ejecutado correctamente.` |
| Segunda línea del cuerpo | A1.4 | A2.5 | debe diferir **solo** en el identificador de build (`Build: PocRevitAddin.Nuget` vs `Build: PocRevitAddin.Local`) |
| Excepción o error en cualquiera de los dos | A1.2-A1.4 | A2.2-A2.5 | debe ser **no** en ambos |

**Veredicto de equivalencia (SC-002 / FR-005):** ______________________________________________

Se considera **equivalente** si las seis filas coinciden en el sentido descrito. Cualquier
desviación (texto distinto más allá del identificador de build, un botón que no aparece, un
diálogo de error en uno mientras el otro va bien) es un fallo de equivalencia: anótalo tal cual,
no lo suavices ni lo redondees a "básicamente funciona igual".

---

## 4. Si alguno de los dos falla al cargar (edge case de requirements.md)

Este es el edge case explícito de `requirements.md`: *"¿Qué ocurre si el addin compila sin errores
contra el paquete pero falla al cargar en Revit 2026 (por ejemplo, por un desajuste de versión
entre los metadatos del paquete y el runtime real de Revit 2026)?"*. Si llegaste aquí desde A1.2,
A1.4, A2.2 o A2.5 por un fallo real, sigue este bloque en vez de improvisar el diagnóstico.

**Qué anotar, exactamente (no resumas, copia literal):**

1. **Cuál de los dos builds falló** — NuGet, Local, o ambos. Esto es el dato más importante:
   - Si falla **solo el NuGet** y el Local funciona: es la señal directa de un desajuste entre los
     metadatos del paquete (`Nice3point.Revit.Api.RevitAPI`/`RevitAPIUI` versión `2026.4.10`,
     ver `RECONOCIMIENTO.md`) y el runtime real de Revit 2026 instalado en esta máquina.
   - Si falla **solo el Local**: no es el edge case de este PoC (el Local es la referencia,
     compilada contra las DLL de la propia instalación) — es un problema del entorno de
     verificación en sí (permisos, `.addin` mal copiado, antivirus bloqueando el DLL, etc.);
     revisa la sección 5 antes de sacar ninguna conclusión sobre el paquete NuGet.
   - Si fallan **los dos**: tampoco es evidencia contra el paquete NuGet — sospecha primero de la
     carpeta de Addins, del `AddInId`/`FullClassName` o de la propia instalación de Revit;
     sección 5.
2. **El texto exacto del diálogo de error de Revit** al arrancar (Revit suele mostrar un diálogo
   tipo "A problem occurred while loading a plug-in" con el nombre del `.addin`/ensamblado y a
   veces un botón para ver el detalle — cópialo entero, incluida cualquier excepción anidada).
3. **El journal de Revit de esa sesión**, si el diálogo no da suficiente detalle: carpeta
   `%LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit 2026\Journals\` (o `%APPDATA%\...`, según
   versión), el `.txt` más reciente por fecha de modificación. Busca `PocRevitAddin` dentro.
4. **Versión de Revit instalada exacta** (Help → About, o `Autodesk Revit 2026 (build ...)` en la
   pantalla de inicio) frente a la versión del paquete fijada en el `.csproj`
   (`Nice3point.Revit.Api.RevitAPI` `2026.4.10` — ver `PocRevitAddin.Nuget.csproj`).

`Observado (build que falló, diálogo completo, journal si aplica, versión de Revit):`
______________________________________________

**Qué significa este resultado para el veredicto del PoC — léelo antes de concluir nada:**

- Este edge case **no descarta el PoC de raíz por sí solo**. Un fallo de carga aislado no invalida
  automáticamente la hipótesis de ADR-008; primero hay que distinguir (punto 1 de arriba) si es un
  problema del paquete o del propio procedimiento de verificación.
- Si tras esa distinción el fallo es real y achacable al paquete (el NuGet falla, el Local no, y
  la sección 5 descarta causas de entorno), entonces **sí aplica FR-009**: *"Si el PoC falla (el
  paquete no compila sin Revit, o el DLL resultante no carga/funciona igual que el compilado
  localmente), el proyecto MUST caer a referencia por ruta local, documentando que esto implica la
  desaparición del CI de compilación y que la distribución a terceros exigirá tener Revit instalado
  para compilar"*. Esa caída a referencia local, y su documentación, no la decide este guion ni
  quien lo rellena: es una consecuencia que se tramita después, con lo anotado aquí como evidencia.
- No es tarea de quien verifica "arreglar" el fallo cambiando la versión del paquete sobre la
  marcha ni reinterpretar el resultado para que encaje con lo esperado. Anota lo que pasó.

---

## 5. Triaje (solo si algo de arriba no salió como se esperaba y no encaja en la sección 4)

**Paso 1 — ¿está el `.addin` bien formado y en la carpeta correcta?**

```powershell
Get-ChildItem "$env:APPDATA\Autodesk\Revit\Addins\2026\" | Where-Object { $_.Name -like "PocRevitAddin*" }
```

Debe listar, tras la sección 2, cuatro o seis ficheros (`.dll` + `.addin`, y opcionalmente `.pdb`)
por cada build. Si falta alguno, la copia de la sección 1 o 2 no se completó: repítela con Revit
cerrado.

`Observado / conclusión:` ______________________________________________

**Paso 2 — ¿el `FullClassName` del `.addin` coincide con la clase real?** Los dos `.addin` de
este PoC declaran `FullClassName=PocRevitAddin.App` (mismo namespace/clase en los dos ensamblados,
es intencional: son builds distintos del mismo código en `Shared\PocRevitAddin.cs`). Confirma que
no se ha copiado por error el `.addin` de un build junto al `.dll` del otro (p. ej.
`PocRevitAddin.Nuget.addin` apuntando a `PocRevitAddin.Local.dll`): el elemento `<Assembly>` de
cada `.addin` debe coincidir en nombre con el `.dll` que está al lado.

`Observado / conclusión:` ______________________________________________

**Paso 3 — ¿hay rastro en el journal de Revit?** Mismo journal que en la sección 4, punto 3.
Busca también mensajes de bloqueo de antivirus/SmartScreen sobre un `.dll` sin firmar descargado
de una carpeta de compilación (no es un caso exótico con ensamblados nuevos sin firmar).

`Observado:` ______________________________________________

---

## 6. Limpieza — quitar los dos addins de `%APPDATA%\Autodesk\Revit\Addins\2026\`

Este PoC es desechable, igual que el PoC #1: no debe quedar registrado ocupando el ribbon de
sesiones futuras de Revit que no tengan nada que ver con este PoC.

```powershell
Remove-Item "$env:APPDATA\Autodesk\Revit\Addins\2026\PocRevitAddin.Nuget.*" -Force
Remove-Item "$env:APPDATA\Autodesk\Revit\Addins\2026\PocRevitAddin.Local.*" -Force
Get-ChildItem "$env:APPDATA\Autodesk\Revit\Addins\2026\" | Where-Object { $_.Name -like "PocRevitAddin*" }
```

Resultado esperado: los seis (o cuatro) ficheros desaparecen y el último `Get-ChildItem` no
devuelve nada. Haz esto con Revit cerrado, igual que en la copia (P1).

`Observado:` ______________________________________________

---

## Resumen de qué cubre cada comprobación

| Comprobación | Criterio |
|---|---|
| A1.2 - A1.4 | Historia 2, Acceptance Scenarios 1 y 2 (build NuGet: ribbon + ejecución sin error) |
| A2.2, A2.4, A2.5 | Historia 2, Acceptance Scenario 3 (build Local, mismo procedimiento) |
| A2.3 | Observación abierta: panel de ribbon duplicado vs. fusionado con dos addins simultáneos |
| §3 | SC-002 / FR-005 — criterio explícito de equivalencia entre ambos builds |
| §4 | Edge case de `requirements.md` — fallo de carga por desajuste de versión, y su relación con FR-009 |
| §5 | Triaje — solo si algo de lo anterior falló y no encaja en el diagnóstico de §4 |
| §6 | Cierre obligatorio del PoC (desregistro de ambos addins) |
