# Veredicto — PoC #2: Paquete NuGet de metadatos de la API de Revit

PoC: `002-poc-2-paquete-nuget-metadatos-api-revit` · Lote 5, tarea 1 (Escribir el veredicto)
Entradas: `RECONOCIMIENTO.md` §1–§8 (reconocimiento, Lote 1 tarea 1) y §9–§14 (decisión, Lote 1 tarea 2),
`GUION-VERIFICACION.md` (Lote 4, ejecutado y anotado por el usuario en Revit 2026 vivo, con
`Captura.PNG` como evidencia gráfica), run de CI
[32069521493](https://github.com/Ashromer/prompt-to-revit/actions/runs/32069521493) (Lote 3),
`plan.md` (resultados anotados por lote).
Fecha: 2026-08-18

---

## 1. Resultado — ADR-008 CONFIRMADO. No se activa FR-009.

Las referencias a la API de Revit se declaran por **paquete NuGet de metadatos**, no por ruta local a
`C:\Program Files\Autodesk\Revit 2026\`. Paquetes y versión, exactos y fijos:

```xml
<PackageReference Include="Nice3point.Revit.Api.RevitAPI"   Version="[2026.4.10]" />
<PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="[2026.4.10]" />
```

Nada falló: el addin compila en un runner sin Revit instalado, el DLL resultante carga en Revit 2026 y
su botón se comporta igual que el del DLL compilado contra las DLL locales. **No se cae a referencia por
ruta local, el CI de compilación se mantiene** y la fila del Tech Stack deja de estar en `TBD`.

La notación de corchetes `[2026.4.10]` es parte de la decisión, no estilo: fija el rango a esa única
versión, la que se verificó en Revit vivo. Se rechaza explícitamente el rango flotante
`Version="$(RevitVersion).*"` que recomienda el propio README del paquete (`RECONOCIMIENTO.md` §10.2).

## 2. Criterios de `requirements.md`, uno por uno

| Criterio | Resultado | Evidencia |
|---|---|---|
| **SC-001** — compila Debug y Release **sin Revit instalado** | **Cumplido** | Run de CI [32069521493](https://github.com/Ashromer/prompt-to-revit/actions/runs/32069521493), `conclusion: success`, 1m22s, runner `windows-latest` (sin Revit). Pasos `Build … (Debug)` y `Build … (Release)` en verde sobre `PocRevitAddin.Nuget.csproj`. El build de esta máquina (Lote 2, también limpio en ambas configuraciones) **no cuenta como prueba de este criterio**: aquí Revit 2026 sí está instalado — ver nota 2.1 |
| **SC-002** — el DLL carga en Revit 2026 y el botón funciona, indistinguible del build local | **Cumplido**, confirmado por el usuario | `GUION-VERIFICACION.md` §1 (A1.1–A1.4, build NuGet: copia, arranque sin diálogo de error, panel y botón en el ribbon, `TaskDialog` correcto — *"Hasta aquí desde A1.1 correcto todo"*) y §2 (A2.1–A2.6, build Local en solitario, con `Captura.PNG` como evidencia del ribbon y de la sesión). Tabla de equivalencia §3: **las siete filas coinciden**, veredicto anotado **"Equivalente"** |
| **SC-003** — el paquete expone como mínimo `RevitAPI` y `RevitAPIUI` | **Cumplido** | `dotnet list package` sobre `PocRevitAddin.Nuget.csproj` → `Nice3point.Revit.Api.RevitAPI [2026.4.10]` y `Nice3point.Revit.Api.RevitAPIUI [2026.4.10]` como referencias **directas** resueltas (Lote 3, tarea 3), no leído del README. Reforzado por el propio código: `Shared/PocRevitAddin.cs` usa tipos de los dos ensamblados (`Autodesk.Revit.UI.IExternalApplication`/`RibbonPanel`/`TaskDialog` de RevitAPIUI; `Autodesk.Revit.Attributes.Transaction` y `Autodesk.Revit.DB.ElementSet` de RevitAPI) y compila en el runner sin Revit |
| **SC-004** — un workflow de CI compila y pasa los tests, sin Revit en el runner | **Cumplido** | `.github/workflows/poc2-build.yml` (`push` + `workflow_dispatch`, `windows-latest`, `actions/setup-dotnet@v4` 8.0.x). Run 32069521493: los **tres** pasos en verde, incluido `dotnet test` sobre `PocRevitAddin.Tests.csproj` (3/3, suite mínima de FR-010 sin referencia alguna a `Autodesk.*`). El workflow no toca `PocRevitAddin.Local.csproj`, que exige Revit instalado |
| **SC-005** — paquete y versión anotados en Tech Stack y Dependencies de `tech-spec.md`, y Discovery cerrado | **Cumplido en el mismo commit que este documento** | Lote 5, tarea 2: fila "Referencias de la API" del Tech Stack, bloque Dependencies de runtime, ADR-008 y el ítem de Discovery *"¿Qué paquete de metadatos de la API de Revit se usa…?"* de `specs/tech-spec.md` (FR-007, FR-008) |

### 2.1 — Por qué el build de la máquina de desarrollo no prueba SC-001

Está dicho en `plan.md` (Contexto de ejecución) y se repite aquí porque es la trampa más fácil de este
PoC: en esta máquina existe `C:\Program Files\Autodesk\Revit 2026\RevitAPI.dll`, así que un
`dotnet build` limpio **no distingue** entre "resolvió por el paquete NuGet" y "resolvió por otra vía".
El build local del Lote 2/3 se reporta como lo que fue — filtro rápido antes de gastar un run de CI —
y la prueba de Historia 1 es exclusivamente el runner de GitHub Actions.

### 2.2 — Los cuatro success criteria de la sección "PoC #2" de `specs/roadmap.md`

Son los mismos cuatro primeros, en otra redacción. Correspondencia explícita, para que el roadmap no
se marque por inferencia:

| Success criterion del roadmap | Equivale a | Resultado |
|---|---|---|
| El addin compila en Debug y Release sin Revit instalado | SC-001 | ✅ |
| El DLL resultante carga en Revit 2026 y el botón funciona | SC-002 | ✅ |
| Están disponibles los ensamblados que el proyecto necesita, como mínimo `RevitAPI` y `RevitAPIUI` | SC-003 | ✅ |
| Un workflow de CI compila y pasa los tests sin Revit | SC-004 | ✅ |

## 3. El único hallazgo real de la verificación en Revit vivo, y por qué no activa FR-009

Al intentar cargar **los dos addins de prueba a la vez** (copiando el `.addin` del Local sin retirar el
del NuGet), el segundo `OnStartup` en ejecutarse falló con:

```
Autodesk.Revit.Exceptions.ArgumentException: The panel with name PoC #2 NuGet vs Local already exists!
```

Texto del diálogo "Fallo de herramienta externa" anotado por el usuario; nota de
`GUION-VERIFICACION.md` §2. (`Captura.PNG` no es de este momento: es de la sesión posterior, con el
build Local ya en solitario — evidencia de A2.2/A2.3, no de este fallo. No se conserva captura de
pantalla del diálogo de error.) La causa es conocida y trivial: los dos `.addin` registran un panel con el
**mismo nombre**, porque ambos builds compilan el **mismo fichero físico** `Shared/PocRevitAddin.cs`
(exigencia de la tarea 1 del Lote 2, para que la comparación compare de verdad el método de referencia y
no dos implementaciones distintas). Era, además, el punto que `@revit-developer` dejó anotado como
pendiente de verificar en Revit vivo al crear el build Local (Lote 2, tarea 3): la hipótesis de que
Revit daría dos paneles separados, uno por ensamblado, **es falsa** — el nombre de panel es único por
sesión de Revit, no por addin.

**Por qué esto no es FR-009**, dicho sin suavizarlo:

- FR-009 se activa si *"el paquete no compila sin Revit, o el DLL resultante no carga/funciona igual que
  el compilado localmente"*. Aquí el paquete compila (SC-001) y su DLL carga y funciona (SC-002).
- El fallo **no distingue entre builds** (inferencia del código, no observada directamente: solo se
  probó un orden de carga): por venir de un literal compartido en `Shared/PocRevitAddin.cs`, lo
  sufriría el segundo `.addin` en cargar sea cual sea. No hay nada del paquete NuGet ni de sus
  metadatos implicado.
- Historia 2 **no pide** cargar los dos a la vez: el propio Independent Test de `requirements.md`
  admite explícitamente cargar *"en una misma sesión de Revit 2026 o en dos sesiones sucesivas"*, y
  el Acceptance Scenario 3 pide *"repetir el mismo procedimiento de carga"* — que es lo que se hizo,
  cargando cada build por separado.
- El edge case de `requirements.md` que sí habría importado — *"compila sin errores contra el paquete
  pero falla al cargar en Revit 2026 por desajuste de versión entre los metadatos del paquete y el
  runtime real"* — **no se manifestó**: `GUION-VERIFICACION.md` §4 quedó sin activar, con esa
  constatación escrita.

Lección que sobrevive al PoC, aunque el código sea desechable: **el nombre del `RibbonPanel` es un
recurso global de la sesión de Revit**. Si Tier 0 llegara a convivir con otra build del mismo addin
(por ejemplo, una versión de desarrollo junto a una instalada), el nombre del panel debe incluir un
discriminante, o `CreateRibbonPanel` revienta el `OnStartup` del segundo.

## 4. Salvedad conocida y NO bloqueante: licencia y redistribución

Copiada de `RECONOCIMIENTO.md` §12, que la dejó redactada para este documento:

> El paquete elegido (`Nice3point.Revit.Api.RevitAPI` / `.RevitAPIUI` `2026.4.10`) es «solo
> metadatos» en el sentido de **empaquetado** — `ref/` sin `lib/`, no se copia a la salida — pero
> **no** en el sentido de contenido binario: el `.nupkg` incluye la `RevitAPI.dll` original de
> Autodesk (35,1 MB) bajo licencia MIT del empaquetador, **sin permiso de redistribución
> acreditado por Autodesk**. Interpretación de «solo metadatos» fijada por el usuario el
> 2026-08-17. No bloquea el PoC (ningún candidato viable lo evita y no existe paquete oficial de
> Autodesk), pero **debe reevaluarse antes de declarar «distribución a terceros» como objetivo con
> compromiso**. Mitigación identificada: archivar los `.nupkg` de la versión fijada; salida:
> FR-009 (referencia por ruta local), reversible con un cambio de `<Reference>` a cambio de perder
> el CI de compilación. Detalle completo y fuentes: `RECONOCIMIENTO.md` §12.

Dos consecuencias operativas que este veredicto añade a esa redacción:

- **ADR-010** ("diseñar para distribución, empaquetar después") es el punto donde esta salvedad vuelve a
  la mesa. Mientras la distribución a terceros siga siendo motivación y no objetivo con compromiso, la
  decisión se sostiene tal cual.
- La mitigación (archivar los dos `.nupkg` de `2026.4.10`) **no se ejecuta ahora**: es trabajo de Tier 0
  si se decide, no alcance de este PoC, y no se da por hecho aquí.

## 5. Riesgos abiertos de `RECONOCIMIENTO.md` §13, estado tras la verificación

| # | Riesgo | Estado |
|---|---|---|
| 1 | `ref/` era inferencia, no inspección directa del `.nupkg` | **Falsado en positivo por observación.** Tras `dotnet build`, `bin\Debug\net8.0-windows\` contiene solo el DLL propio del addin (5,6 KB): no aparece `RevitAPI.dll` de ~35 MB. La lectura (a), "solo compilación por empaquetado", se sostiene para este paquete concreto (Lote 2, tarea 2) |
| 2 | Nada de lo decidido en el Lote 1 demostraba que compilase | **Resuelto**: run de CI en verde (SC-001, SC-004) |
| 3 | Desajuste de *update* entre los metadatos `2026.4.10` y el runtime real de la máquina | **No se manifestó**, y además confirmado por observación: la barra de título de `Captura.PNG` muestra "Autodesk Revit 2026.4" — la máquina del usuario está en la update 4, la misma que los metadatos. **Sigue abierto para el proyecto final**: esta verificación no dice nada sobre APIs introducidas o cambiadas en updates concretas de Revit 2026, porque el addin trivial (`IExternalApplication`, `RibbonPanel`/`PushButtonData`, `IExternalCommand`, `TaskDialog`) no usa ninguna |
| 4 | Bus factor: `Nice3point` es una persona individual | **Abierto y asumido.** Mitigado, no eliminado: repo público con MIT sobre el empaquetado, `.nupkg` inmutables en nuget.org y versión fijada exacta |
| 5 | Licencia (§12 / §4 de este documento) | **Abierto y asumido conscientemente** |

## 6. FR-007, FR-008, FR-009

- **FR-007** (nombre y versión exactos en Tech Stack y Dependencies de `tech-spec.md`): cubierto por la
  tarea 2 del Lote 5, en el mismo commit que este documento.
- **FR-008** (cerrar el ítem de Discovery del paquete de metadatos): cubierto por la misma tarea.
- **FR-009** (caída a referencia por ruta local): **no se activa**. Ningún criterio falló. La salida
  sigue disponible y sigue siendo barata — cambiar los dos `<PackageReference>` por `<Reference>` con
  `HintPath`, exactamente lo que ya hace `PocRevitAddin.Local.csproj` — al coste explícito de perder el
  CI de compilación y de exigir Revit instalado a quien compile.

## 7. Qué queda desbloqueado

- **ADR-008 confirmado**: fin de la incertidumbre que bloqueaba el CI y, con él, **F0.1** (monorepo,
  solución y CI) y **F1.2** (`RoslynCompiler`), las dos features que `specs/roadmap.md` hace depender
  explícitamente del PoC #2.
- **Gate Fase 0 completo**: con el PoC #1 cerrado en positivo (`pocs/001-poc-1-sdk-oficial-de-mcp-para-net/VEREDICTO.md`,
  peldaño 1, ADR-001 confirmado) y este cerrado en positivo, **los dos PoCs bloqueantes están cerrados y
  Tier 0 puede arrancar**. Constancia formal en `specs/roadmap.md`, Gate Fase 0.
- **Reutilizable en Tier 0**: el workflow `.github/workflows/poc2-build.yml` es el primer CI del repo y
  sirve de plantilla para el de F0.1 (runner `windows-latest`, `setup-dotnet` 8.0.x, build Debug +
  Release + `dotnet test`). El resto del PoC es desechable por diseño: vive en
  `pocs/002-poc-2-paquete-nuget-metadatos-api-revit/`, fuera de `src/`, y no se arrastra. Lo que
  sobrevive es este veredicto, la versión fijada de los dos paquetes, la salvedad de licencia y la
  lección del nombre de panel (§3).
