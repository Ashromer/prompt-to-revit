# PoC #2 — Paquete NuGet de metadatos de la API de Revit
Feature Branch: 002-poc-2-paquete-nuget-metadatos-api-revit

Created: 2026-08-17

Status: Draft

Input: User description: "PoC #2 — Paquete NuGet de metadatos de la API de Revit, desde specs/roadmap.md sección \"PoC #2 — Paquete NuGet de metadatos de la API de Revit\""

## User Scenarios & Testing (mandatory)

### User Story 1 - Compilar el addin sin Revit instalado (Priority: P1)

Como desarrollador (o como CI), quiero compilar el addin trivial (un `IExternalApplication` que añade un botón al ribbon) referenciando únicamente un paquete NuGet de metadatos de la API de Revit, en una máquina donde Revit 2026 no está instalado, y obtener una compilación limpia en Debug y Release.

Why this priority: Es la hipótesis central de ADR-008 y del PoC. Si esto no se cumple, todo lo demás (CI, distribución a terceros) queda bloqueado y se cae a referencia por ruta local.

Independent Test: En un entorno sin Revit instalado (runner de CI o contenedor), ejecutar `dotnet build` en Debug y en Release sobre el proyecto del addin trivial referenciando el paquete NuGet; confirmar que ambas compilaciones terminan sin errores.

Acceptance Scenarios:

1. Given una máquina sin Revit 2026 instalado y el proyecto del addin trivial referenciando el paquete NuGet de metadatos, When se ejecuta `dotnet build` en configuración Debug, Then la compilación termina sin errores.
2. Given el mismo proyecto, When se ejecuta `dotnet build` en configuración Release, Then la compilación termina sin errores.
3. Given el paquete NuGet de metadatos, When se inspeccionan los ensamblados que expone, Then incluyen como mínimo RevitAPI y RevitAPIUI.

### User Story 2 - Verificar que el DLL compilado con el paquete carga y funciona igual que el compilado contra las DLL locales (Priority: P1)

Como desarrollador, quiero comparar el addin compilado contra el paquete NuGet de metadatos con el mismo addin compilado contra las DLL locales de `C:\Program Files\Autodesk\Revit 2026\`, cargando ambos en una instalación real de Revit 2026, para confirmar que el resultado es equivalente.

Why this priority: Es la comprobación que realmente cierra la hipótesis: no basta con que compile, el DLL producido tiene que comportarse en Revit igual que uno compilado por el método actual (referencia local), que es el que usan los plugins existentes del autor.

Independent Test: Compilar el addin trivial dos veces (una contra el paquete NuGet, otra contra las DLL locales), cargar ambos `.addin`/DLL en una misma sesión de Revit 2026 (o en dos sesiones sucesivas) y confirmar que en ambos casos el botón aparece en el ribbon y ejecuta la acción esperada.

Acceptance Scenarios:

1. Given el addin compilado contra el paquete NuGet de metadatos, When se abre Revit 2026 con el addin registrado, Then el botón aparece en el ribbon.
2. Given el botón visible en el ribbon, When se pulsa, Then se ejecuta la acción esperada sin error.
3. Given el addin compilado contra las DLL locales (referencia de comparación), When se repite el mismo procedimiento de carga y pulsación del botón, Then el comportamiento observado es equivalente al del build con el paquete NuGet.

### User Story 3 - Workflow de CI que compila y pasa los tests sin Revit (Priority: P2)

Como mantenedor del proyecto, quiero un workflow de CI que compile el addin y ejecute sus tests en un runner sin Revit instalado, para que cualquier cambio se valide automáticamente sin depender de una máquina con Revit.

Why this priority: Es uno de los success criteria explícitos y parte del Output del PoC (workflow de CI funcionando), pero depende de que las Historias 1 y 2 ya hayan demostrado que la compilación sin Revit es viable.

Independent Test: Disparar el workflow de GitHub Actions (por ejemplo mediante un push o un `workflow_dispatch`) y confirmar que el job compila el addin y ejecuta la suite de tests mínima creada para el PoC en verde, en un runner que no tiene Revit instalado.

Acceptance Scenarios:

1. Given un push al repositorio, When se dispara el workflow de CI, Then el job compila el addin en un runner sin Revit instalado sin errores.
2. Given la compilación en verde, When el workflow ejecuta la suite de tests, Then los tests pasan sin necesitar Revit instalado en el runner.

## Edge Cases

- Si el paquete NuGet de metadatos no cubre algún ensamblado que el proyecto final necesite además de RevitAPI y RevitAPIUI, no bloquea el veredicto del PoC: se acepta el paquete igual y el ensamblado que falte se resuelve caso por caso más adelante, cuando se sepa cuál es y haga falta de verdad.
- ¿Qué ocurre si el addin compila sin errores contra el paquete pero falla al cargar en Revit 2026 (por ejemplo, por un desajuste de versión entre los metadatos del paquete y el runtime real de Revit 2026)?
- ¿Cómo se maneja el caso en que el paquete cubre la API pero su publicador es un tercero no oficial y deja de mantenerse o de actualizarse para futuras versiones de Revit?

## Requirements (mandatory)

### Functional Requirements

- FR-001: El PoC MUST identificar un paquete NuGet de solo metadatos que cubra la API de Revit 2026, exponiendo como mínimo los ensamblados RevitAPI y RevitAPIUI. La identificación (nombre, publicador y versión exactos) se hace mediante una tarea de reconocimiento previa, equivalente al Lote 1 del PoC #1 (`pocs/001-poc-1-sdk-oficial-de-mcp-para-net/RECONOCIMIENTO.md`), no se asume de antemano ni se elige a ciegas.
- FR-002: El PoC MUST implementar un addin trivial (un `IExternalApplication` que añade un botón al ribbon) que sirva como caso de prueba común para ambos métodos de compilación.
- FR-003: El addin trivial MUST compilar en Debug y en Release referenciando únicamente el paquete NuGet de metadatos, en una máquina sin Revit 2026 instalado.
- FR-004: El PoC MUST compilar también el mismo addin trivial referenciando las DLL locales de `C:\Program Files\Autodesk\Revit 2026\`, como término de comparación.
- FR-005: El DLL resultante de la compilación contra el paquete NuGet MUST cargar en Revit 2026 y su botón de ribbon MUST funcionar, de forma equivalente al DLL compilado contra las DLL locales. La evidencia que certifica "funciona equivalente" es la confirmación manual del usuario en una sesión real de Revit 2026, anotada por escrito (mismo patrón que `pocs/001-poc-1-sdk-oficial-de-mcp-para-net/GUION-VERIFICACION.md`): ningún agente puede confirmarlo, solo puede prepararlo y recoger lo anotado.
- FR-006: El PoC MUST establecer un workflow de **GitHub Actions** que compile el addin y ejecute su suite de tests en un runner sin Revit instalado, ya que el repositorio vive en GitHub.
- FR-007: El PoC MUST dejar registrados el nombre exacto y la versión del paquete NuGet elegido en las secciones Tech Stack y Dependencies de `tech-spec.md`.
- FR-008: El PoC MUST cerrar el ítem de Discovery abierto en `tech-spec.md` ("¿Qué paquete de metadatos de la API de Revit se usa, en qué versión, y cubre 2026 completo?") con la decisión tomada.
- FR-009: Si el PoC falla (el paquete no compila sin Revit, o el DLL resultante no carga/funciona igual que el compilado localmente), el proyecto MUST caer a referencia por ruta local, documentando que esto implica la desaparición del CI de compilación y que la distribución a terceros exigirá tener Revit instalado para compilar.
- FR-010: Como no existe todavía ninguna suite de tests aplicable al addin trivial de este PoC, el PoC MUST crear una suite mínima propia, solo para ejercitar el flujo de CI sin Revit — sin objetivo de cobertura, igual que el resto del código del PoC.

### Key Entities (include if feature involves data)

- Paquete NuGet de metadatos de la API de Revit: dependencia externa que sustituye la referencia directa a `RevitAPI.dll`/`RevitAPIUI.dll` instaladas localmente; nombre y versión exactos son el resultado principal a determinar por este PoC.
- Addin trivial de prueba: `IExternalApplication` mínimo con un botón de ribbon, compilado dos veces (contra el paquete NuGet y contra las DLL locales) para servir de comparación.
- Workflow de CI: pipeline que compila el addin y ejecuta tests en un runner sin Revit instalado.

## Success Criteria (mandatory)

### Measurable Outcomes

- SC-001: El addin trivial compila sin errores en Debug y en Release, en una máquina sin Revit 2026 instalado, referenciando el paquete NuGet de metadatos.
- SC-002: El DLL compilado contra el paquete NuGet carga en Revit 2026 y el botón del ribbon funciona, de forma indistinguible del DLL compilado contra las DLL locales, confirmado y anotado por el usuario en una sesión real de Revit (FR-005).
- SC-003: El paquete NuGet expone, como mínimo, los ensamblados RevitAPI y RevitAPIUI necesarios para el proyecto.
- SC-004: Un workflow de CI compila el addin y ejecuta su suite de tests en verde, en un runner sin Revit instalado.
- SC-005: El nombre y la versión del paquete quedan anotados en Tech Stack y Dependencies de `tech-spec.md`, y el ítem de Discovery correspondiente queda cerrado.

## Assumptions

- Se dispone de una máquina con Revit 2026 instalado para realizar la carga y comparación descritas en la Historia 2.
- Se dispone de un entorno sin Revit instalado (runner de CI o contenedor) para verificar la compilación de las Historias 1 y 3.
- El addin trivial usado en el PoC no necesita cubrir más superficie de la API de Revit que un `IExternalApplication` con un botón de ribbon; el objetivo es probar la cadena de compilación y carga, no la funcionalidad del plugin final.
- Los dos PoCs de la Fase 0 (#1 SDK MCP para .NET y #2 este) son independientes y no comparten código ni salida entre sí, según indica `roadmap.md`.
- No existe todavía un workflow base de CI sobre el que extender: el workflow de GitHub Actions de FR-006 se crea desde cero como parte de este PoC.
- El nombre, publicador y versión del paquete NuGet no se asumen de antemano: los determina la tarea de reconocimiento de FR-001, antes de escribir el addin trivial.
