# Guion de verificación — PoC #2 Paquete NuGet de metadatos de la API de Revit

Checklist para ejecutar a mano, en una sesión real de Revit 2026. Es el único camino por el que
SC-002 puede darse por cumplido (FR-005): **ningún agente puede abrir Revit ni confirmar esto**,
ni parcial ni provisionalmente. Los campos `Observado:` se rellenan con lo que veas literalmente
(pega texto o capturas si hace falta) — no marques nada como OK de memoria ni "por adelantado".

Lo ya verificado sin Revit (Debug + Release limpios, suite de tests, contenido del paquete NuGet)
está en `RECONOCIMIENTO.md` y en los logs de compilación del Lote 2/3, y no se repite aquí. Este
guion cubre solo lo que falta: que **el DLL compilado contra el paquete NuGet se comporte en Revit
igual que el compilado contra las DLL locales** (Historia 2, requirements.md), cargando **cada
build por separado** — Historia 2 pide repetir el mismo procedimiento de carga, no cargar los dos
a la vez.

---

## 0. Preparación

**P0 — Rutas de este worktree** (ajusta si verificas desde otro). Define `$BASE` primero; los
bloques de las secciones siguientes lo usan tal cual:

```powershell
$BASE = "D:\Arquitectura\W_TRABAJOS\12_IA_OPT\2605_PROMPT_TO_REVIT\.worktrees\002-poc-2-paquete-nuget-metadatos-api-revit\pocs\002-poc-2-paquete-nuget-metadatos-api-revit"
```

- Build NuGet: `$BASE\PocRevitAddin.Nuget\bin\Release\net8.0-windows\PocRevitAddin.Nuget.dll`
  (+ `.addin` en `$BASE\PocRevitAddin.Nuget\`)
- Build Local: `$BASE\PocRevitAddin.Local\bin\Release\net8.0-windows\PocRevitAddin.Local.dll`
  (+ `.addin` en `$BASE\PocRevitAddin.Local\`)
- Carpeta de registro: `C:\Users\Usuario\AppData\Roaming\Autodesk\Revit\Addins\2026\`

Los dos builds ya están compilados en Debug y Release (nivel "Compila" de `CLAUDE.md`, confirmado
por `revit-developer`/`tester`). Este guion usa el build **Release**; no hace falta recompilar.

**P1 — Revit CERRADO antes de tocar la carpeta de Addins.** El DLL queda bloqueado con Revit
abierto (`CLAUDE.md`). Comprueba que no queda un `Revit.exe` colgado en el Administrador de tareas
antes de copiar o mover nada.

**P2 — Carpeta de Addins limpia de intentos anteriores.** Borra cualquier `PocRevitAddin.Nuget.*`
o `PocRevitAddin.Local.*` de una verificación previa a medias.

`Observado:` TODO OK

---

## 1. Build NuGet — Historia 2, Acceptance Scenarios 1 y 2

**A1.1 — Copiar `.dll`/`.pdb`/`.addin` del build NuGet** (Revit cerrado):

```powershell
Copy-Item "$BASE\PocRevitAddin.Nuget\bin\Release\net8.0-windows\PocRevitAddin.Nuget.dll" "$env:APPDATA\Autodesk\Revit\Addins\2026\" -Force
Copy-Item "$BASE\PocRevitAddin.Nuget\bin\Release\net8.0-windows\PocRevitAddin.Nuget.pdb" "$env:APPDATA\Autodesk\Revit\Addins\2026\" -Force
Copy-Item "$BASE\PocRevitAddin.Nuget\PocRevitAddin.Nuget.addin" "$env:APPDATA\Autodesk\Revit\Addins\2026\" -Force
```

Resultado esperado: los tres ficheros aparecen sin error de copia.

`Observado:` TODO OK

**A1.2 — Abrir Revit 2026.** Resultado esperado: arranca sin diálogo de error de carga de addin.
Si aparece uno mencionando `PocRevitAddin.Nuget`, salta a la sección 4.

`Observado:` TODO OK

**A1.3 — Confirmar el botón en el ribbon** (Historia 2, Scenario 1). Panel `PoC #2 NuGet vs
Local`, botón de dos líneas `PoC #2` / `PocRevitAddin.Nuget`; el tooltip debe mencionar "paquete
NuGet de metadatos" y terminar en `Assembly: PocRevitAddin.Nuget`.

`Observado:` TODO OK

**A1.4 — Pulsar el botón** (Scenario 2). Resultado esperado: `TaskDialog` con título
**"PoC #2 - Paquete NuGet de metadatos"** y cuerpo:

```
El botón del addin PoC #2 se ha ejecutado correctamente.
Build: PocRevitAddin.Nuget
```

Sin error, sin excepción no controlada, sin cuelgue.

`Observado:` Hasta aquí desde A1.1 correcto todo.

Si A1.2-A1.4 no salen como se espera, anota el fallo tal cual y ve a la sección 4 o 5 — no tiene
sentido comparar contra un Local "bueno" si el NuGet ya falló.

---

## 2. Build Local — Acceptance Scenario 3

> **Nota — por qué se prueba en solitario y no junto al NuGet.** Se intentó cargar los dos
> ensamblados a la vez (copiando el Local sin retirar el NuGet) para observar el ribbon con ambos
> presentes. Revit no lo permite: los dos `.addin` registran un panel con el **mismo nombre**
> (`"PoC #2 NuGet vs Local"`), y el segundo `OnStartup` en ejecutarse revienta con
> `Autodesk.Revit.Exceptions.ArgumentException: The panel with name PoC #2 NuGet vs Local already
> exists!` sin llegar a poner su botón — texto del diálogo "Fallo de herramienta externa" anotado
> por el usuario (no hay captura de pantalla de ese diálogo entre los artefactos del PoC;
> `Captura.PNG` es de un momento posterior, la sesión con el build Local ya en solitario — ver
> A2.2/A2.3). **Esto no es FR-009** (no es el paquete NuGet fallando contra el runtime de Revit; es
> un choque de nombre de panel entre dos addins de prueba cargados juntos, algo que Historia 2 no
> pide — el propio `requirements.md`, Historia 2, admite explícitamente cargar "en dos sesiones
> sucesivas"). El procedimiento correcto — y el que sigue este guion — es retirar el `.addin` del
> NuGet antes de copiar el Local, para que solo haya un addin registrando el panel.

**A2.1 — Cerrar Revit** y dejar cargado solo el build Local:

```powershell
Move-Item "$env:APPDATA\Autodesk\Revit\Addins\2026\PocRevitAddin.Nuget.addin" "$BASE\PocRevitAddin.Nuget.addin.bak" -Force
Copy-Item "$BASE\PocRevitAddin.Local\bin\Release\net8.0-windows\PocRevitAddin.Local.dll" "$env:APPDATA\Autodesk\Revit\Addins\2026\" -Force
Copy-Item "$BASE\PocRevitAddin.Local\bin\Release\net8.0-windows\PocRevitAddin.Local.pdb" "$env:APPDATA\Autodesk\Revit\Addins\2026\" -Force
Copy-Item "$BASE\PocRevitAddin.Local\PocRevitAddin.Local.addin" "$env:APPDATA\Autodesk\Revit\Addins\2026\" -Force
```

(El `.dll`/`.pdb` del NuGet quedan en la carpeta pero inertes: sin `.addin`, Revit no los registra.)

`Observado:` Correcto — hecho.

**A2.2 — Reabrir Revit 2026.** Resultado esperado: arranca sin diálogo de error para
`PocRevitAddin.Local`.

`Observado:` Sin diálogo de error — confirmado por `Captura.PNG` (sesión abierta con normalidad,
ribbon cargado).

**A2.3 — Confirmar el botón en el ribbon** (mismo criterio que A1.3, ahora sobre
`PocRevitAddin.Local`).

`Observado:` Confirmado por `Captura.PNG` — panel `PoC #2 NuGet vs Local` con un único botón
`PoC #2` / `PocRevitAddin.Local`.

**A2.4 — Tooltip del botón Local.** Pasa el ratón por encima y confirma que menciona "DLL locales
de Revit 2026" y termina en `Assembly: PocRevitAddin.Local`.

`Observado:` TODO OK

**A2.5 — Pulsar el botón Local** (Scenario 3: mismo procedimiento que en la sección 1). Resultado
esperado: `TaskDialog` con el mismo título **"PoC #2 - Paquete NuGet de metadatos"** y cuerpo:

```
El botón del addin PoC #2 se ha ejecutado correctamente.
Build: PocRevitAddin.Local
```

`Observado:` Build: PocRevitAddin.Local

**A2.6 — Cierra Revit y limpia el fichero temporal** dejado en A2.1 (no hace falta restaurarlo a
Addins; la sección 6 desregistra ambos builds igualmente):

```powershell
Remove-Item "$BASE\PocRevitAddin.Nuget.addin.bak" -Force
```

`Observado:` Hecho

---

## 3. Criterio de equivalencia (compara lo anotado en 1 y 2)

SC-002 exige comportamiento "indistinguible" entre los dos builds **cargados por separado**
(no simultáneamente — ver nota de la sección 2). Verifícalo campo por campo con lo anotado arriba:

| Campo | NuGet (sección 1) | Local (sección 2) | ¿Coincide? |
|---|---|---|---|
| Revit arranca sin diálogo de error | A1.2 | A2.2 | Sí (ambos OK) |
| Panel/botón visible en el ribbon | A1.3 | A2.3 | Sí (ambos OK) |
| Tooltip correcto | A1.3 | A2.4 | SI|
| Título del `TaskDialog` | A1.4 | A2.5 | Si|
| Primera línea del cuerpo del `TaskDialog` | A1.4 | A2.5 | Si|
| Segunda línea del cuerpo (solo difiere el build) | A1.4 | A2.5 | Si|
| Excepción o error en cualquiera de los dos | A1.2-A1.4 | A2.2-A2.5 | No|

**Veredicto de equivalencia (SC-002 / FR-005):** Equivalente. Las siete filas coinciden en el
sentido descrito — ambos builds se comportan de forma indistinguible cargados por separado.

Se considera **equivalente** si todas las filas coinciden en el sentido descrito. Cualquier
desviación (texto distinto más allá del identificador de build, diálogo de error en uno y no en
el otro) es un fallo de equivalencia: anótalo tal cual, sin suavizarlo.

---

## 4. Si alguno de los dos falla al cargar (edge case de requirements.md)

Edge case explícito de `requirements.md`: *"¿Qué ocurre si el addin compila sin errores contra el
paquete pero falla al cargar en Revit 2026 (desajuste de versión entre metadatos y runtime real)?"*
No se ha activado en esta verificación — el único error visto (choque de nombre de panel al cargar
los dos addins a la vez, sección 2) no es este edge case. Si A1.2, A1.4, A2.2 o A2.5 fallan de
verdad, sigue este bloque:

1. **Cuál build falló** — NuGet, Local o ambos:
   - Solo **NuGet**: señal directa de desajuste entre los metadatos del paquete
     (`Nice3point.Revit.Api.RevitAPI`/`RevitAPIUI` `2026.4.10`, ver `RECONOCIMIENTO.md`) y el
     runtime real de Revit 2026 instalado.
   - Solo **Local**: no es el edge case de este PoC (el Local es la referencia) — problema del
     entorno de verificación; ve a la sección 5.
   - **Ambos**: tampoco es evidencia contra el paquete — sospecha de la carpeta de Addins, del
     `AddInId`/`FullClassName` o de la instalación de Revit; sección 5.
2. **Texto exacto del diálogo de error** (nombre del `.addin`/ensamblado, excepción anidada si la
   muestra).
3. **Journal de Revit** si el diálogo no basta: `%LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit
   2026\Journals\`, el `.txt` más reciente. Busca `PocRevitAddin`.
4. **Versión de Revit instalada** (Help → About) frente a `Nice3point.Revit.Api.RevitAPI 2026.4.10`
   fijada en `PocRevitAddin.Nuget.csproj`.

`Observado:` ______________________________________________

**Qué significa esto para el veredicto:** un fallo aislado no descarta el PoC por sí solo — primero
distingue (punto 1) si es del paquete o del procedimiento. Si el fallo es real y achacable al
paquete, **aplica FR-009**: caer a referencia local, documentando la pérdida de CI de compilación y
que la distribución a terceros exigirá tener Revit instalado. Esa caída no la decide este guion:
se tramita después con lo anotado aquí como evidencia. No es tarea de quien verifica "arreglar" el
fallo cambiando la versión del paquete sobre la marcha.

---

## 5. Triaje (solo si algo no salió como se esperaba y no encaja en la sección 4)

**Paso 1 — ¿`.addin` bien formado y en la carpeta correcta?**

```powershell
Get-ChildItem "$env:APPDATA\Autodesk\Revit\Addins\2026\" | Where-Object { $_.Name -like "PocRevitAddin*" }
```

Tras la sección 2 debe listar solo los tres ficheros del Local (el `.dll`/`.pdb` del NuGet siguen
ahí pero sin `.addin`). Si falta alguno, repite la copia con Revit cerrado.

`Observado / conclusión:` ______________________________________________

**Paso 2 — ¿el `FullClassName` coincide?** Los dos `.addin` declaran `FullClassName=PocRevitAddin.App`
(mismo namespace/clase en los dos ensamblados, intencional: builds distintos del mismo código en
`Shared\PocRevitAddin.cs`). Confirma que no se ha copiado el `.addin` de un build junto al `.dll`
del otro: el `<Assembly>` de cada `.addin` debe coincidir con el `.dll` que tiene al lado.

`Observado / conclusión:` ______________________________________________

**Paso 3 — ¿rastro en el journal?** Mismo journal de la sección 4, punto 3. Busca también bloqueos
de antivirus/SmartScreen sobre un `.dll` sin firmar.

`Observado:` ______________________________________________

---

## 6. Limpieza — quitar ambos addins de la carpeta de Addins

PoC desechable, igual que el PoC #1: no debe quedar registrado en sesiones futuras.

```powershell
Remove-Item "$env:APPDATA\Autodesk\Revit\Addins\2026\PocRevitAddin.Nuget.*" -Force
Remove-Item "$env:APPDATA\Autodesk\Revit\Addins\2026\PocRevitAddin.Local.*" -Force
Get-ChildItem "$env:APPDATA\Autodesk\Revit\Addins\2026\" | Where-Object { $_.Name -like "PocRevitAddin*" }
```

Resultado esperado: el último `Get-ChildItem` no devuelve nada. Con Revit cerrado (P1).

`Observado:` Vacío — el `Get-ChildItem` no devolvió ningún fichero `PocRevitAddin*`. Ambos addins
desregistrados correctamente.

---

## Resumen de qué cubre cada comprobación

| Comprobación | Criterio |
|---|---|
| A1.2 - A1.4 | Historia 2, Scenarios 1 y 2 (build NuGet: ribbon + ejecución sin error) |
| A2.2 - A2.5 | Historia 2, Scenario 3 (build Local, mismo procedimiento, en solitario) |
| Nota §2 | Hallazgo real pero fuera de FR-009: choque de nombre de panel al cargar los dos addins a la vez |
| §3 | SC-002 / FR-005 — equivalencia entre ambos builds cargados por separado |
| §4 | Edge case de `requirements.md` — fallo de carga por desajuste de versión, y su relación con FR-009 |
| §5 | Triaje — solo si algo falló y no encaja en §4 |
| §6 | Cierre obligatorio del PoC (desregistro de ambos addins) |
