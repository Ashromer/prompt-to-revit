# PROMPT_TO_REVIT — Pasarela de modelado por prompt

> Estado: **diseño cerrado, pendiente de 2 PoCs**. Nada implementado todavía.
> Entorno: Revit 2026 (**interfaz en inglés**), .NET 8, Claude Code vía MCP.
> Última actualización: 2026-08-17
>
> **Autoridad de los documentos.** Este fichero es el diseño: qué se construye y por qué es así.
> `specs/product-spec.md` formaliza el producto, `specs/tech-spec.md` fija el cómo técnico con sus
> ADRs, y `specs/roadmap.md` ordena la construcción. Cuando este documento y un ADR discrepen,
> **manda el ADR** y este fichero se actualiza. Los cambios de la sesión del 2026-08-17 están
> incorporados; el resumen está en §10.

---

## 1. Objetivo

Modelar y consultar en Revit **desde la conversación con Claude**, sin cerrar Revit, sin
recompilar el addin y sin interrumpir el modelado manual. Claude actúa de traductor entre
la intención en lenguaje natural y la API de Revit.

Requisitos que fijan el diseño:

- **R1** — Ejecutar código nuevo sin reiniciar Revit ni recompilar el addin.
- **R2** — No interrumpir el modelado en curso.
- **R3** — Todo lo que haga Claude debe ser reversible.
- **R4** — Claude debe poder **leer** el modelo, no solo escribir (si no, adivina).
- **R5** — Lo que funciona se acumula y se reutiliza.
- **R6** — La herramienta debe poder distribuirse si funciona. No exige instalador en la v1, pero
  prohíbe empotrar rutas, credenciales o supuestos sobre esta máquina concreta.

---

## 2. Arquitectura

```
Claude Code
    │  (protocolo MCP, stdio)
    ▼
Puente MCP  (C#, .NET 8, exe autocontenido)
    │  (named pipe, ACL del usuario actual)
    ▼
Addin Revit  (C#, .NET 8)
    ├── Listener del pipe        ← hilo propio, NO toca la API
    ├── Roslyn                   ← compila el C# recibido en memoria
    ├── ExternalEvent            ← salta al contexto API cuando Revit está ocioso
    ├── Ventana de aprobación    ← WPF modeless, muestra el C# antes de ejecutar
    ├── Transaction              ← una por ejecución, con nombre
    └── respuesta JSON           ← ids creados / datos / excepción con traza
```

**Un solo lenguaje: C# sobre .NET 8 en todo el proyecto.** El puente MCP era Node/TypeScript en la
versión anterior de este documento; ahora usa el SDK oficial de MCP para .NET. Razón: el autor
domina C# y no Node, y un `.exe` autocontenido no exige Node instalado en la máquina destino, que es
lo que hace viable R6. Ver ADR-001.

**Punto clave, y no ha cambiado:** el puente MCP *no puede* tocar la API de Revit. La API solo
existe dentro del proceso de Revit y solo en su hilo principal. El puente es transporte; el addin es
quien ejecuta. Todo camino, en las dos vías, termina en una llamada normal a la API.

Escribir el puente en C# **no elimina el segundo proceso, solo el segundo lenguaje**: Claude Code
arranca los servidores MCP como subproceso por stdio, y el addin vive dentro de Revit, así que no se
puede arrancar así. Siempre habrá dos procesos.

### El contrato compartido va en un proyecto sin Revit

`RevitBridge.Core` define los tipos de mensaje y no referencia ni la API de Revit ni Windows. Lo
usan el puente, el addin y los tests. Con los dos lados en el mismo lenguaje y el mismo repo, un
cambio de contrato rompe la compilación de ambos en el mismo commit, que es la garantía que se
buscaba. Ver ADR-004.

### Por qué `ExternalEvent` cumple R2

`ExternalEvent.Raise()` se llama desde el hilo del listener y **encola** la petición.
Revit la ejecuta cuando queda ocioso. Si el usuario está en mitad de un comando o con un
diálogo abierto, espera. No se interrumpe nada; se difiere.

La petición bloquea con `TaskCompletionSource` + timeout hasta que el handler termina.
Nunca devolver "aceptado" a ciegas: sin respuesta real, Claude trabaja sin retroalimentación.

### Por qué named pipe y no HTTP

El HTTP sobre `127.0.0.1` existía para cruzar la frontera entre Node y C#. Con los dos lados en
.NET esa razón desaparece, y un named pipe con ACL restringida al usuario actual **elimina de raíz**
la superficie que §5.E advertía: no hay puerto que escuchar ni token que gestionar, rotar o filtrar.
La autorización la hace el sistema operativo, no código propio. Ver ADR-002.

Contrapartida aceptada: se pierde poder depurar con curl o Postman. Se cubre con un cliente de pipe
de línea de comandos en el proyecto de tests.

---

## 3. Las dos vías

Las dos usan la API de Revit. Lo que cambia es **quién escribe las llamadas y cuándo se
compilan**.

| | **Commandset** | **Roslyn** |
|---|---|---|
| Escribe la lógica | El usuario, de antemano | Claude, en la conversación |
| Compilación | Con el addin | En memoria, al vuelo |
| Capacidad nueva | Recompilar + reiniciar Revit | Inmediata |
| Superficie de la API | Solo lo previsto | Toda |
| Fiabilidad | Alta (código probado) | Puede fallar a la primera |
| Latencia | Inmediata | ~1-2 s la 1.ª, décimas después |
| Riesgo | Acotado | Ejecución de código arbitrario |

### Cómo se reparte el trabajo

**Roslyn es el laboratorio; el commandset es producción.** Lo exploratorio va por Roslyn; lo
que se demuestra estable **gradúa** a comando compilado (ver §6).

La decisión de vía la toma Claude, pero **el sesgo se diseña en cómo se expone cada una**:

- El commandset se publica como **herramientas MCP individuales**, tipadas, con esquema.
  Aparecen en la lista de herramientas y son la opción natural.
- Roslyn se publica como **una sola herramienta** cuya descripción dice explícitamente que es
  una escotilla de emergencia: *usar solo si ninguna otra herramienta cubre la operación*.
- Regla dura en el `CLAUDE.md` del proyecto: antes de usar `exec_csharp`, consultar la lista
  de comandos disponibles.

Esa asimetría **es el mecanismo**, no cosmética. Aplanarla "para que la API quede más limpia"
devuelve a Roslyn al camino por defecto.

### Python: eje distinto, descartado

"Commandset vs Roslyn" es *cuándo se compila*. "C# vs Python" es *qué lenguaje*. Son
ortogonales:

| | C# | Python |
|---|---|---|
| Precompilado | DLL de comandos | — |
| Dinámico | Roslyn | pyRevit / RPS |

La casilla "Python precompilado" está vacía porque Python es interpretado: un bridge de Python
**ya es** el equivalente a Roslyn. No aporta nada que Roslyn no dé y duplica runtime.
**Descartado** para este proyecto.

---

## 4. Contrato de la pasarela

Los nombres se conservan de la versión anterior aunque el transporte ya no sea HTTP: son nombres de
**operación**, no rutas.

| Operación | Transacción | Aprobación | Uso |
|---|---|---|---|
| `/commands` | no | auto | Lista comandos compilados disponibles |
| `/query` | **no** | auto | Solo lectura: niveles, tipos, símbolos, parámetros |
| `/compile` | no | auto | Dry-run: compila sin ejecutar, devuelve diagnósticos |
| `/exec` | sí | **manual siempre** | Ejecuta C# dentro de `Transaction` |
| `/command` | sí | según | Invoca un comando compilado del catálogo |
| `/rollback` | sí | manual | Borra los elementos creados en la sesión |

### Firma del snippet Roslyn

```csharp
public static class Script
{
    public static object Execute(UIApplication uiapp)
    {
        var doc = uiapp.ActiveUIDocument.Document;
        // ...
        return new { ids = new[] { 12345 } };
    }
}
```

### Respuesta

```json
{
  "ok": false,
  "fase": "runtime",          // "compilacion" | "runtime" | "ok"
  "resultado": null,
  "ids_creados": [],
  "error": "InvalidOperationException: ...",
  "traza": "...",
  "duracion_ms": 340
}
```

Devolver la excepción **completa**, con `InnerException` y `ex.GetType().Name` como respaldo:
varias excepciones de la API de Revit llegan con `Message` vacío.

**Un fallo de ejecución viaja como respuesta MCP correcta**, con este JSON como contenido. El error
de protocolo se reserva para lo que no es un fallo de ejecución: Revit cerrado, pipe caído, timeout
del transporte. Marcar un fallo de ejecución como error de protocolo arriesga que el cliente lo
resuma o lo recorte, y la traza es el único dato útil. Ver ADR-007.

---

## 5. Salvaguardas

Cinco capas. Ninguna basta sola.

### A. Antes de ejecutar

1. **`/query` antes de nada.** Prohibido escribir nombres de tipo a mano
   (`"Generic - 200mm"`). Primero se resuelven los `ElementId` reales contra el documento
   abierto; el código generado usa ids, no cadenas. Aunque Revit esté en inglés, los nombres
   dependen de la plantilla y de qué familias estén cargadas.
2. **Dry-run por defecto** (`/compile`): Roslyn hace `Emit` sin ejecutar. Valida sintaxis y
   tipos sin tocar el modelo. Barato, y atrapa la mayoría de los fallos de Claude.
   Fallo en compilación cuesta ~1 s; fallo en runtime dentro de una transacción cuesta mucho más.
3. **Filtro sintáctico previo.** Un `CSharpSyntaxWalker` recorre el árbol *antes* de compilar
   y rechaza el snippet si aparece `System.IO`, `System.Net`, `System.Diagnostics.Process`,
   `Environment.Exit`, `Document.SaveAs`, `Document.Close`. Nada de esto tiene por qué
   aparecer en código de modelado. **También `doc.Delete`**, hasta que exista su vía dedicada con
   previsualización (C.9); el filtro debe cubrir los intentos de rodearlo por alias o reflexión.
4. **Límites**: tamaño máximo del snippet, y todo bucle generado debe tener cota explícita
   de iteraciones.

### B. Contexto de ejecución

5. **Siempre `ExternalEvent`.** Jamás tocar la API desde el hilo del listener.
6. **Una `Transaction` con nombre por ejecución**: `"Claude: <intención>"`. Dos efectos —
   se deshace con un solo Ctrl+Z, y en el historial de deshacer se ve exactamente qué vino
   de Claude y qué no.
7. **`TransactionGroup` para operaciones multipaso**: `Assimilate()` al terminar bien,
   `RollBack()` ante cualquier excepción. Un fallo a mitad no deja restos.
8. **`IFailuresPreprocessor` que trague *warnings* pero NO *errors*.** Los warnings del commit
   bloquean el lote con un diálogo; los errores deben revertir, no auto-resolverse.

### C. Alcance del daño

9. **Sin borrados implícitos.** `doc.Delete` solo por una vía dedicada que primero devuelva
   una previsualización (cuántos elementos, de qué categorías) y exija confirmación.
10. **Ámbito por defecto = lo creado en esta sesión.** Modificar elementos preexistentes exige
    intención explícita.
11. **El bridge nunca escribe en disco.** Ni `Save`, ni `SaveAs`, ni exportaciones. Guarda el
    usuario.
12. **Un solo documento.** No abrir ni modificar otros documentos ni documentos de familia
    de forma implícita.
13. **El registro JSONL es la única verdad de los ids creados.** `/rollback` los reconstruye
    leyendo el log de la sesión, no una lista en memoria: así **sobrevive a una caída de Revit**,
    que es precisamente cuando más falta hace poder deshacer. Debe tolerar un log truncado.
    Cinturón además del Ctrl+Z. Ver ADR-006.

### D. Revisión humana

14. **La ventana del addin muestra el C# antes de ejecutarlo**, con botón Aprobar/Rechazar.
    Es la salvaguarda más importante: el usuario lee el código antes de que toque el modelo.
    Es **modeless**, para no romper R2. Un snippet que no se puede juzgar de un vistazo está mal
    escrito.
15. **Niveles de aprobación:**
    - `query`, `commands`, `compile` → automático siempre.
    - `exec` → **manual siempre en la v1**. La opción "confiar durante 30 min" queda **fuera**:
      mientras el sistema no se haya demostrado, la revisión humana no debe tener agujeros. Se
      acepta la fricción a cambio.
    - Borrar o modificar preexistentes → **siempre manual**, sin excepción.
16. **Aprobación caducada = rechazo automático.** Siendo modeless, la ventana puede quedar
    desatendida. Al agotarse el plazo la petición se descarta **sin ejecutar**: el caso por defecto
    es no tocar el modelo. Esperar indefinidamente dejaría una aprobación huérfana que podría
    ejecutarse mucho después, contra un modelo que ya cambió. Ver ADR-009.
17. **Log a disco *antes* de ejecutar**, no después: si Revit cae, queda la evidencia de qué
    lo tumbó.

### E. Aislamiento del canal

18. **Guardar antes de cada sesión de trabajo con el bridge.** Sobre archivo local, nunca
    directamente sobre un modelo central compartido.
19. **Named pipe local con ACL del usuario actual.** Sin puerto abierto, sin token, sin superficie
    de red. Esto es ejecución de código arbitrario: la versión anterior lo mitigaba con
    `127.0.0.1` + token en cabecera; un pipe lo elimina de raíz. No reintroducir un socket a la
    escucha, ni detrás de un flag de depuración. Ver ADR-002.

### Limitación conocida: no hay timeout real

Revit es monohilo. Si un snippet entra en bucle infinito, **congela Revit y no se puede matar
desde fuera** sin perder el trabajo. No hay solución técnica limpia. Mitigaciones: cota
obligatoria de iteraciones en el código generado (A.4), dry-run, y revisión humana del código
(D.14). Es el punto débil real del diseño y conviene tenerlo presente.

El timeout del transporte **corta la espera del puente, no la ejecución en Revit**. No redactarlo
nunca como si la operación se hubiera cancelado.

---

## 6. Registro y aprendizaje

Cada ejecución deja una línea en `%APPDATA%\RevitBridge\log\YYYY-MM.jsonl`:

```json
{"ts":"2026-08-17T10:32:11","intencion":"crear niveles cada 3 m","via":"roslyn",
 "fuente":"...","fase":"runtime","ok":false,
 "error":"InvalidOperationException: The level already exists at this elevation",
 "ids_creados":[],"duracion_ms":340}
```

La línea se escribe **antes** de ejecutar y se completa después. Es también la fuente de verdad de
`/rollback` (C.13), así que no es solo telemetría: es estructura.

Dos productos derivados:

- **Snippets que funcionan → comandos compilados.** Lo que se usa y se mantiene estable
  gradúa al DLL de utilidades. El catálogo se puebla con lo que realmente se usa, no con lo
  que alguien imaginó de antemano. Cuanto más maduro, menos improvisación hace falta.
  El procedimiento de cosecha está en la skill `/harvest-bridge-log`.
- **Errores recurrentes → skill.** Se destilan a `.claude/skills/revit-bridge/` y a
  `revit_knowledge/revit_api_knowledge.md`.

Esto **no es fine-tuning**. Es acumular un corpus que se carga como contexto — el mismo
mecanismo que ya se usa a mano en `revit_api_knowledge.md`, pero alimentado automáticamente.

Lo más valioso del log no es la geometría: es que captura **este entorno concreto** —
plantillas, familias cargadas, nombres de tipos reales. Eso es justo lo que ningún
conocimiento general puede aportar, y es el modo de fallo principal de Claude.

**Señal de salud del proyecto**: si la proporción de ejecuciones que necesitan Roslyn no baja
entre dos cosechas consecutivas, el catálogo no está madurando.

---

## 7. Fuentes de código probado

### DECISIÓN: addin propio desde cero

Verificado el 17-ago-2026. **No se adopta ningún commandset externo como base.**

| Repo | Estado verificado | Veredicto |
|---|---|---|
| [`mcp-servers-for-revit/revit-mcp-commandset`](https://github.com/mcp-servers-for-revit/revit-mcp-commandset) | **ARCHIVADO**. Último push 25-feb-2026. 57 ★ / **53 forks** | Descartado: muerto y fragmentado en forks |
| [`LuDattilo/revit-mcp-server`](https://github.com/LuDattilo/revit-mcp-server) | Activo, MIT. 138 herramientas. Creado 28-mar-2026, último push 3-jul-2026. 42 ★, 3 issues, **140 commits** | Descartado como dependencia; útil como referencia |
| [`mcp-servers-for-revit/revit-mcp`](https://github.com/mcp-servers-for-revit/revit-mcp) | Servidor MCP (TypeScript) | Referencia del protocolo |

Razones del descarte:

- El commandset "oficial" está **archivado** y su ecosistema se ha fragmentado (53 forks sobre
  57 stars). No hay una base canónica viva sobre la que construir.
- `LuDattilo` declara la matriz correcta (2023-24 → .NET 4.8, **2025-26 → .NET 8**,
  2027 → .NET 10 preview), pero **140 commits para 138 herramientas** — un commit por
  herramienta — sugiere generación masiva, no código madurado contra modelos reales. Con
  42 stars y 3 issues, esas herramientas no están siendo ejercitadas por nadie.
- Un proyecto individual sosteniendo 5 versiones de Revit y 3 targets de .NET es una
  superficie de mantenimiento insostenible.

Ambos son MIT: se **leen** para copiar patrones, no se **dependen**.

Consecuencia práctica: los nombres de comando del catálogo son propios, y deben coincidir
**exactamente** entre el puente y el addin o el modelo no los encuentra, sin error diagnosticable.
Al no haber upstream, la coincidencia se garantiza estructuralmente: contrato compartido en
`RevitBridge.Core`, un solo repo, un solo commit (ADR-004).

### DECISIÓN: referencias de la API por paquete NuGet de metadatos

`RevitAPI.dll` **no es redistribuible**: vive en `C:\Program Files\Autodesk\Revit 2026\`.
Referenciarla por ruta local, que es lo que hacen los plugins existentes del autor, exige Revit
instalado para compilar: imposibilita el CI y complica que un tercero compile el proyecto, que
ahora es un objetivo (R6).

Decisión: referenciar por **paquete NuGet de solo metadatos** (tipo `Nice3point.Revit.Api.*`).
Nombre y versión exactos **sin verificar todavía**: es el PoC #2. Ver ADR-008.

### Corpus de referencia de la API

- [`jeremytammik/the_building_coder_samples`](https://github.com/jeremytammik/the_building_coder_samples) — el más rico (respaldo de ~2000 artículos del blog)
- [`jeremytammik/RevitSdkSamples`](https://github.com/jeremytammik/RevitSdkSamples) — SDK oficial
- [`ADN-DevTech/RevitTrainingMaterial`](https://github.com/ADN-DevTech/RevitTrainingMaterial) — labs oficiales de Autodesk

### DECISIÓN: Roslyn directo, no Westwind.Scripting

[`RickStrahl/Westwind.Scripting`](https://github.com/RickStrahl/Westwind.Scripting) (evaluado
17-ago-2026) sí aporta cosas reales: `AddDefaultReferencesAndNamespaces()` resuelve el juego de
referencias, **cachea ensamblados por código idéntico**, formatea errores con nº de línea,
soporta `AlternateAssemblyLoadContext` para descarga, y va sobre .NET 8 desde v2.0.

Aun así se descarta:

- Los **10+ MB son de Roslyn, no del wrapper**: se pagan igual. La decisión real es solo
  "wrapper vs directo", y el wrapper ahorra ~200 líneas.
- **Riesgo específico de Revit**: todos los addins comparten AppDomain. Si otro addin carga
  otra versión de `Microsoft.CodeAnalysis`, hay conflicto. A menos superficie de dependencia,
  menos riesgo.
- El resto de su API (plantillas Handlebars, `Evaluate()` de expresiones, async) es
  funcionalidad que aquí **no se quiere**: interesa un único camino estrecho — compilar una
  clase de forma fija y ejecutarla en `ExternalEvent`.

Se usa **Roslyn directamente**, leyendo el código de Westwind como referencia para la
resolución de referencias y la caché por hash del snippet.

También refuerza la decisión que el wrapper esconde exactamente las tres cosas que aquí hay que
controlar con precisión: el `Emit` sin ejecutar del dry-run, el juego de referencias contra los
ensamblados de Revit, y el `AssemblyLoadContext` colectible. Ver ADR-003.

### Advertencia: caducidad de versión

**No existe un dataset de snippets verificados para LLM.** Lo que hay es código humano de
referencia, y la mayoría apunta a Revit 2015-2020. Roturas de API que impiden compilar contra 2026:

| Antiguo | Actual | Desde |
|---|---|---|
| `NewFloor(...)` | `Floor.Create(...)` | 2022 |
| `ElementId.IntegerValue` | `ElementId.Value` | 2024 |
| `DisplayUnitType` / `UnitType` | `ForgeTypeId` / `SpecTypeId` | 2021-22 |
| `NewAlignment` con retorno | devuelve `void` | 2026 |
| .NET Framework 4.8 | .NET 8 | 2025 |

Ese corpus vale como **referencia conceptual**, no para copiar y pegar. Refuerza §6: el corpus
que de verdad sirve es el propio, generado por el log contra esta plantilla, estas familias y
Revit 2026.

---

## 8. Notas de implementación

- **`AssemblyLoadContext(name, isCollectible: true)`** (.NET 8) permite descargar el ensamblado
  tras cada ejecución. Sin eso se acumula una assembly por snippet. Si el snippet deja una
  referencia viva el ALC no descarga: es fuga de memoria, no de corrección.
- **Caché de ensamblados por hash del snippet**: código idéntico no se recompila. Es lo que hace
  Westwind y merece copiarse. Reduce la latencia de las iteraciones repetidas y baja la presión
  sobre el ALC, porque hay menos ensamblados que descargar.
- **Juego de referencias para Roslyn**: es el punto pejiguero. En la práctica, filtrar
  `AppDomain.CurrentDomain.GetAssemblies()` por las no dinámicas con `Location` no vacío, y
  añadir explícitamente `RevitAPI.dll`, `RevitAPIUI.dll` y `RevitBridge.Utils`.
- **AppDomain compartido**: todos los addins de Revit conviven en el mismo AppDomain. Si otro
  addin carga otra versión de `Microsoft.CodeAnalysis`, hay conflicto, y no es un fallo que se
  pueda prevenir desde aquí. Es la razón principal para minimizar la superficie de dependencias
  y hay que tenerlo presente al diagnosticar fallos raros de carga.
- **DLL de utilidades propio referenciado en cada compilación**, para que el código generado
  pueda invocar lo ya probado (`CurveLoop.CreateViaThicken`, pipelines de offset, etc.) en vez
  de rederivarlo y repetir errores ya resueltos.
- **Los comandos del catálogo viven en ese mismo DLL**, marcados por atributo y descubiertos por
  reflexión al arrancar. Graduar un snippet es añadir un método marcado. Nombres duplicados deben
  fallar al arrancar, no en silencio. Ver ADR-005.
- **La costura de abstracción es lo que hace testable el proyecto.** Las interfaces se declaran en
  `RevitBridge.Core` (sin Revit, sin Windows) y solo el adaptador las implementa contra la API.
  Todo lo demás se testea con xUnit y Revit cerrado. Si aparece lógica que merece test dentro del
  adaptador, se saca: el adaptador es la única capa sin red de seguridad.
- `Dispatcher.InvokeAsync` (no `Invoke`) para log/progreso desde el hilo de la API.
- **Revit cerrado es el caso normal**, no un error: el puente arranca como subproceso de Claude
  Code y puede estar vivo con Revit apagado. Igual que el addin puede estar cargado y no ejecutar
  nada durante minutos porque el usuario tiene un diálogo abierto. Ninguno de los dos es un fallo.

---

## 9. Estado y plan

El plan de construcción está en `specs/roadmap.md`: **Fase 0 con dos PoCs bloqueantes**, y luego
tres tiers de complejidad creciente (fundamentos y lectura → ejecución con salvaguardas → catálogo
y ciclo de aprendizaje).

Los dos PoCs bloquean el arranque porque cada uno valida un ADR del que depende la estructura
completa:

- [ ] **PoC #1 — SDK oficial de MCP para .NET.** Si falla, el puente vuelve a Node y TypeScript y
      ADR-004 se rehace en cascada. Requisitos en `specs/001-poc-1-sdk-oficial-de-mcp-para-net/`.
- [ ] **PoC #2 — Paquete NuGet de metadatos de la API.** Si falla, desaparece el CI y la
      distribución exige Revit instalado para compilar.

Lo que estaba pendiente en la versión anterior y ya está resuelto:

- [x] Flujo de trabajo del día a día → skill `/revit-bridge` y la regla de precedencia de `CLAUDE.md`
- [x] `CLAUDE.md` del proyecto con la precedencia commandset → Roslyn
- [x] Esqueleto del addin → módulos en `specs/tech-spec.md` §Module Design, construcción ordenada
      en el Tier 0 del roadmap
- [x] Servidor MCP y declaración de herramientas → decidido C# con SDK oficial, pendiente de
      validar en el PoC #1
- [x] Conjunto inicial de comandos `/query` → F2.2 del roadmap, derivado de la lista de §7

---

## 10. Cambios de la sesión del 2026-08-17

| Sección | Antes | Ahora | Motivo |
|---|---|---|---|
| §2 | Servidor MCP en Node/TypeScript | **C# con el SDK oficial de MCP para .NET** | Un solo lenguaje, el del autor; `.exe` autocontenido sin exigir Node (R6). ADR-001 |
| §2, §4, §5.E | HTTP sobre `127.0.0.1` + token | **Named pipe con ACL de usuario** | Con los dos lados en .NET el HTTP pierde su razón; el pipe elimina la superficie de red en vez de mitigarla. ADR-002 |
| §7 | Commandset adoptable de upstream | **Addin propio desde cero**, con evidencia | Verificado: el oficial está archivado, el alternativo no está ejercitado |
| §7 | Evaluar `Westwind.Scripting` | **Roslyn directo**, con el riesgo de AppDomain como razón principal | Menos superficie de dependencia en un AppDomain compartido. ADR-003 |
| §7 | Referencias por ruta local implícitas | **Paquetes NuGet de metadatos** | Sin ello no hay CI ni build reproducible. ADR-008 |
| §5.C.13 | Registro de ids en sesión | **JSONL como única verdad** | Sobrevive a una caída de Revit. ADR-006 |
| §5.D.15 | "Confiar durante 30 min" como opción | **Fuera de la v1** | Mientras el sistema no se demuestre, la revisión humana no debe tener agujeros |
| §5.D.14, D.16 | Ventana sin comportamiento definido al caducar | **Modeless + rechazo automático** | Respeta R2; el caso por defecto es no tocar el modelo. ADR-009 |
| §4 | Sin política de propagación de errores | **Fallo dentro de respuesta correcta** | Garantiza que la traza llegue íntegra. ADR-007 |
| §8 | — | **Caché por hash del snippet** y **riesgo de AppDomain compartido** | Hallazgos de la evaluación de Westwind, aprovechables aunque se descarte |
| §1 | Herramienta personal | **R6: distribuible si funciona** | Decisión del autor en la sesión. ADR-010 |
| §9 | 5 puntos pendientes | **Plan en `specs/roadmap.md`**, 4 de 5 resueltos | La constitución del proyecto los cerró |
