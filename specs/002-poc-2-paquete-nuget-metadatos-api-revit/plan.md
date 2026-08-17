# Plan — PoC #2: Paquete NuGet de metadatos de la API de Revit

Requirements: `specs/002-poc-2-paquete-nuget-metadatos-api-revit/requirements.md`

## Contexto de ejecución

Este PoC vale por el **veredicto que produce**, no por el código. Vive en
`pocs/002-poc-2-paquete-nuget-metadatos-api-revit/`, fuera de `src/`, y se descarta al cerrarlo
(mismo patrón que FR-010 del PoC #1). La calidad del código no es criterio de aceptación: no
refactorizar, no abstraer, no generalizar. El addin es deliberadamente trivial (FR-002): un
`IExternalApplication` con un botón de ribbon, nada más.

**No hay escalera de peldaños aquí, hay una hipótesis binaria con una salida documentada
(FR-009).** Si el paquete no compila sin Revit, o si el DLL resultante no carga/no funciona igual
que el compilado contra las DLL locales, el proyecto **cae a referencia por ruta local**: ADR-008
se revierte, desaparece el CI de compilación, y la distribución a terceros exige que quien compile
tenga Revit instalado. Esa consecuencia se documenta en el veredicto, no se decide a mitad del PoC.

**Máquina de desarrollo != máquina sin Revit.** `C:\Program Files\Autodesk\Revit 2026\RevitAPI.dll`
existe en esta máquina, así que compilar aquí **no puede** demostrar que la compilación funciona sin
Revit instalado (Historia 1). Esa demostración solo la da un runner de GitHub Actions, que no tiene
Revit — por eso el workflow de CI (Historia 3) no es un extra tardío, es el instrumento con el que
se verifica Historia 1. Sí sirve esta máquina, en cambio, para producir el DLL de comparación
compilado contra las DLL locales (Historia 2, FR-004), precisamente porque aquí Revit está instalado.

**Qué no puede verificar ningún agente.** Los agentes compilan, publican y disparan workflows, pero
**no pueden abrir Revit 2026, cargar un `.addin` ni pulsar un botón del ribbon**. SC-002 (carga y
funciona en Revit vivo) lo confirma el usuario siguiendo un guion escrito, igual que
`GUION-VERIFICACION.md` del PoC #1. Un agente que reporte ese criterio como cumplido sin esa
confirmación está mintiendo.

---

## Lote 1 — Reconocimiento (bloquea todo lo demás)

- [x] @architect · Identificar el paquete NuGet candidato: buscar, con fuentes primarias (nuget.org,
  la API de NuGet, GitHub), qué paquete o paquetes de solo metadatos cubren la API de Revit 2026
  completa, exponiendo como mínimo `RevitAPI` y `RevitAPIUI` (FR-001). No asumir de antemano cuál es
  "el" paquete: rastrear candidatos reales (por ejemplo, familias de paquetes versionadas por año de
  Revit publicadas por terceros activos en el ecosistema de plugins de Revit, y cualquier paquete
  oficial de Autodesk si existiera) y descartar los que no llegan a 2026, están abandonados, o no
  declaran explícitamente ser "solo metadatos" (referencias que no redistribuyen la DLL real). Para
  cada candidato serio, anotar: identificador exacto en NuGet, publicador, versión más reciente que
  cubre 2026, fecha de último release, actividad del repositorio si es público, y si expone
  `RevitAPI`/`RevitAPIUI` como mínimo. Dejar constancia explícita de si el publicador es un tercero no
  oficial (edge case de `requirements.md`: riesgo de que dejen de mantenerlo) sin que eso descarte el
  candidato. Escribir el resultado en `pocs/002-poc-2-paquete-nuget-metadatos-api-revit/RECONOCIMIENTO.md`,
  con el mismo rigor de evidencia primaria que `pocs/001-poc-1-sdk-oficial-de-mcp-para-net/RECONOCIMIENTO.md`.
  **Resultado**: `RECONOCIMIENTO.md` escrito. No existe paquete oficial de Autodesk (verificado, no
  supuesto: `owner:autodesk revit` en NuGet solo devuelve un paquete irrelevante de 20 KB). 5
  candidatos de terceros documentados con evidencia primaria; 1 descartado por no llegar a 2026.
  Hallazgo que condiciona la tarea 2: "solo metadatos" tiene dos lecturas que ningún candidato cumple
  a la vez — (a) por empaquetado NuGet (`ref/`, solo compila, no se copia a la salida) o (b) por
  contenido binario (ensamblado realmente desnudado de implementación). Todos los candidatos viables
  cumplen (a) con la DLL original de Autodesk dentro del `.nupkg`; el único que intentaba (b) tiene el
  repo en 404. Además detecta un riesgo de licencia no resuelto: el candidato mejor situado (Nice3point)
  redistribuye la DLL original de Autodesk bajo licencia MIT propia, sin que conste permiso de
  Autodesk — no bloquea el PoC pero condiciona el alcance de "distribución a terceros" si se declara
  objetivo. Recomendación no vinculante del agente: `Nice3point.Revit.Api.RevitAPI`/`.RevitAPIUI`
  2026.4.10.
- [x] @architect · Elegir el paquete y fijar la versión exacta: a partir del reconocimiento, elegir un
  único paquete (o par de paquetes, si `RevitAPI`/`RevitAPIUI` vienen en paquetes separados) y anotar
  su versión exacta, sin rango flotante, con la razón de la elección frente a las alternativas
  descartadas. Si ningún candidato cubre 2026 completo con `RevitAPI` + `RevitAPIUI`, cerrar el PoC en
  negativo aquí mismo (FR-009) y saltar directo al Lote 5. Dejar la decisión por escrito en
  `pocs/002-poc-2-paquete-nuget-metadatos-api-revit/RECONOCIMIENTO.md` antes de escribir una línea de código.
  **Resultado**: FR-009 no se activa. Elegidos `Nice3point.Revit.Api.RevitAPI` +
  `Nice3point.Revit.Api.RevitAPIUI`, versión exacta fijada (no rango flotante, pese a que el propio
  README del paquete recomienda uno): `Version="[2026.4.10]"`. Razones frente a los otros dos
  candidatos elegibles bajo la lectura "por empaquetado": única línea 2026 con updates reales
  (2026.0.4→2026.4.10, no una sola versión congelada de abril 2025), único TFM exacto
  `net8.0-windows7.0`, único "solo compilación" verificable en el propio empaquetado
  (`PackagePath="ref\$(TargetFramework)\"`), único con repo público + licencia declarada. Riesgo de
  licencia documentado en `RECONOCIMIENTO.md` §12 (la DLL original de Autodesk viaja dentro del
  `.nupkg` bajo MIT del empaquetador, sin permiso de redistribución acreditado — no bloquea, pero
  condiciona "distribución a terceros" como objetivo futuro). Riesgo pendiente de falsar en el Lote 2:
  que el ensamblado esté realmente en `ref/` es inferencia, no inspección directa del `.nupkg` — se
  confirma o se refuta con el primer build (§13.1 de `RECONOCIMIENTO.md`).

---

## Lote 2 — El experimento (depende del Lote 1)

- [ ] @revit-developer · Crear el addin trivial compartido: en
  `pocs/002-poc-2-paquete-nuget-metadatos-api-revit/`, fuera de la solución de `src/`, un
  `IExternalApplication` mínimo (`OnStartup`/`OnShutdown`) que añade un panel y un botón al ribbon, y
  un `IExternalCommand` de acción trivial que el botón ejecuta al pulsarlo (por ejemplo, un
  `TaskDialog.Show` con un mensaje reconocible) (FR-002). El código fuente (`.cs`) debe ser el
  **mismo fichero físico** para las dos compilaciones de más abajo (vía `<Compile Include>` con ruta
  relativa compartida, no copia duplicada), para que la comparación de Historia 2 compare de verdad
  el método de referencia y no dos implementaciones distintas.
- [ ] @revit-developer · Compilar contra el paquete NuGet: crear
  `PocRevitAddin.Nuget.csproj` (`net8.0-windows`) que referencia únicamente el paquete y la versión
  exacta fijados en el Lote 1 (sin ruta local, sin `HintPath`), más su `.addin` de registro
  correspondiente. Debe compilar en Debug y en Release en esta misma máquina como primera comprobación
  rápida (sabiendo que aquí no demuestra "sin Revit", eso lo hace el Lote 3), y debe producir un DLL
  que se pueda copiar junto a su `.addin` a `%APPDATA%\Autodesk\Revit\Addins\2026\` (FR-003).
- [ ] @revit-developer · Compilar contra las DLL locales, como término de comparación: crear
  `PocRevitAddin.Local.csproj` que referencia `RevitAPI.dll`/`RevitAPIUI.dll` por `HintPath` a
  `C:\Program Files\Autodesk\Revit 2026\`, con `Private=False`/`CopyLocal=False` como hace el resto de
  plugins del autor, más su propio `.addin` (FR-004). Compila en esta máquina, que sí tiene Revit
  instalado. Nombrar de forma que ambos DLL y `.addin` puedan convivir registrados a la vez sin
  colisionar (GUID de `.addin` distinto, `AssemblyName` distinto, texto del botón que identifique de
  cuál build viene, para poder distinguirlos a simple vista en el ribbon durante la verificación).
- [ ] @test-developer · Crear la suite de tests mínima (FR-010): como no hay ninguna suite aplicable
  todavía y el addin trivial no tiene lógica de negocio real, extraer a una clase de C# puro, **sin
  ninguna referencia a `RevitAPI`/`RevitAPIUI`** (ni siquiera el paquete de metadatos: sus tipos son
  solo firmas, no ejecutables fuera de un proceso Revit real), un fragmento pequeño y honesto de lógica
  del addin — por ejemplo, el texto y tooltip del botón, o el nombre del panel — y escribir 2-3 tests
  xUnit sobre esa clase en un proyecto de test aparte (`PocRevitAddin.Tests.csproj`) que no referencia
  ni `PocRevitAddin.Nuget.csproj` ni `PocRevitAddin.Local.csproj` por su parte de API. Sin objetivo de
  cobertura: el único propósito es que el workflow de CI tenga algo real que ejecutar en un runner sin
  Revit (FR-010, Historia 3).
- [ ] @code-developer · Crear el workflow de GitHub Actions: `.github/workflows/` (no existe ninguno
  todavía en el repo, se crea desde cero), disparable por `push` y por `workflow_dispatch`, en un
  runner `windows-latest` (no tiene Revit instalado, y el addin necesita `net8.0-windows`), que
  ejecute `dotnet build` en Debug y en Release sobre `PocRevitAddin.Nuget.csproj` (nunca sobre
  `PocRevitAddin.Local.csproj`, que exige Revit instalado y no tiene sentido en CI) y luego
  `dotnet test` sobre `PocRevitAddin.Tests.csproj` (FR-006). El job debe fallar si cualquiera de las
  dos configuraciones no compila limpio o si algún test falla.

---

## Lote 3 — Verificación sin Revit (depende del Lote 2, la ejecuta @tester)

- [ ] @tester · Confirmar el build local del proyecto NuGet como humo rápido: ejecutar
  `dotnet build -c Debug` y `dotnet build -c Release` sobre `PocRevitAddin.Nuget.csproj` en esta
  máquina y reportar limpio/no limpio. Dejar explícito en el reporte que **esto no demuestra por sí
  solo Historia 1** (esta máquina tiene Revit instalado), es solo el primer filtro antes de gastar un
  run de CI.
- [ ] @tester · Disparar el workflow y confirmar en verde: lanzar el workflow de GitHub Actions (push
  o `workflow_dispatch`, según quede disponible) y confirmar que el job compila
  `PocRevitAddin.Nuget.csproj` en Debug y en Release sin errores, y que `dotnet test` sobre
  `PocRevitAddin.Tests.csproj` pasa, todo en un runner `windows-latest` sin Revit instalado (SC-001,
  SC-004; Historia 1 y Historia 3). Adjuntar el enlace o el log del run como evidencia. Si el job
  falla, diagnosticar si el fallo es del paquete NuGet elegido (ensamblados que faltan, tipos que no
  resuelven) o del propio workflow, y reportarlo con esa distinción antes de tocar nada del Lote 1.
- [ ] @tester · Inspeccionar los ensamblados expuestos por el paquete: a partir del build en verde,
  confirmar explícitamente que el paquete NuGet elegido expone, como mínimo, `RevitAPI` y `RevitAPIUI`
  (SC-003) — listando las referencias resueltas del proyecto (`dotnet list package` o inspección del
  `.deps.json`/`obj` tras el build) y no solo asumiéndolo del README del paquete.

---

## Lote 4 — Verificación en Revit vivo (depende del Lote 2, la ejecuta el usuario)

- [ ] @tester · Preparar el guion de verificación: escribir en
  `pocs/002-poc-2-paquete-nuget-metadatos-api-revit/GUION-VERIFICACION.md` la secuencia exacta que
  el usuario debe seguir, con el resultado esperado de cada paso y un hueco para anotar lo observado,
  mismo patrón que `pocs/001-poc-1-sdk-oficial-de-mcp-para-net/GUION-VERIFICACION.md`. Debe cubrir, en
  orden: (1) copiar el DLL + `.addin` del build NuGet a
  `%APPDATA%\Autodesk\Revit\Addins\2026\`, abrir Revit 2026 (cerrado previamente, por el bloqueo de
  DLL de `CLAUDE.md`), confirmar que el botón aparece en el ribbon y que al pulsarlo se ejecuta la
  acción esperada sin error (Acceptance Scenarios 1-2 de Historia 2); (2) repetir exactamente lo mismo
  con el DLL + `.addin` del build local (Acceptance Scenario 3); (3) un criterio explícito de
  equivalencia entre ambos (mismo texto de diálogo salvo el identificador de build, mismo
  comportamiento, sin excepción en ninguno de los dos); (4) qué anotar si alguno de los dos falla al
  cargar (edge case de `requirements.md`: desajuste de versión entre los metadatos del paquete y el
  runtime real de Revit 2026) — ese resultado no descarta el PoC de raíz, pero condiciona el veredicto
  vía FR-009. Dejar explícito que ningún agente puede rellenar los `Observado:`, solo prepararlos.
- [ ] @tester · Recoger y diagnosticar lo anotado por el usuario: una vez el usuario haya ejecutado el
  guion, reportar qué quedó cumplido y qué no, citando la evidencia real anotada (no una inferencia).
  Si algo falló, distinguir si el problema es del paquete NuGet (compila pero el runtime no coincide),
  del `.addin` (registro/manifiesto), o de la propia acción del comando trivial, con la misma
  disciplina de triaje que usó el PoC #1 para separar fallo del SDK / fallo de registro / fallo del
  PoC.

---

## Lote 5 — Veredicto y cierre (depende de los Lotes 3 y 4)

- [ ] @architect · Escribir el veredicto: documento
  `pocs/002-poc-2-paquete-nuget-metadatos-api-revit/VEREDICTO.md` con el resultado de cada criterio de
  éxito (SC-001 a SC-004 de `requirements.md`, y los cuatro Success criteria de la sección PoC #2 de
  `specs/roadmap.md`), la decisión sobre ADR-008 (confirmado, o revertido a referencia por ruta local
  por FR-009 con la consecuencia explícita de perder el CI de compilación), y si el veredicto es
  negativo, qué criterio concreto falló y con qué evidencia.
- [ ] @architect · Actualizar el TechSpec: en `specs/tech-spec.md`, sustituir el `TBD` de la fila
  "Referencias de la API" del Tech Stack por el nombre e identificador exactos del paquete
  y su versión, añadirlo a la lista de dependencias directas de runtime, actualizar
  ADR-008 con el veredicto (confirmado tal cual, o revertido, con la consecuencia sobre el
  CI) y marcar como resuelto el ítem de Discovery ("¿Qué paquete de metadatos de la
  API de Revit se usa...?") con la decisión tomada (FR-007, FR-008).
- [ ] @architect · Actualizar el roadmap: en `specs/roadmap.md`, marcar los cuatro success criteria de
  la sección "PoC #2" con su resultado, completar el "Output" con el nombre
  y la versión del paquete y el enlace al workflow de CI, y actualizar el "Gate Fase 0": marcar
  la línea del PoC #2 (cerrado / revertido a ruta local), completar la fila de Dependencies con
  `Microsoft.CodeAnalysis.CSharp` y el paquete de metadatos elegido, y cerrar el Discovery
  del paquete de metadatos. Si ambos PoCs quedan cerrados en positivo, dejar constancia explícita de
  que el Gate Fase 0 completo se cumple y Tier 0 puede arrancar.
- [ ] @judge · Revisar el veredicto: comprobar que SC-002 (carga y funciona en Revit vivo) tiene
  evidencia real anotada por el usuario y no una inferencia del agente, que SC-001/SC-003/SC-004
  (compilación sin Revit y ensamblados expuestos) se apoyan en el run de CI y no en el build de la
  máquina de desarrollo (que tiene Revit instalado y no puede probar esto), que la elección del
  paquete del Lote 1 está justificada frente a las alternativas descartadas y no es una elección a
  ciegas, y que las actualizaciones de `tech-spec.md` y `roadmap.md` son consistentes entre sí y con
  el veredicto. Igual que en el PoC #1, es el único punto de este PoC donde una revisión independiente
  se paga sola.
