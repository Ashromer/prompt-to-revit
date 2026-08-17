# PoC #1 — SDK oficial de MCP para .NET
Feature Branch: 001-poc-1-sdk-oficial-de-mcp-para-net

Created: 2026-08-17

Status: Draft

Input: User description: "el PoC #1 y sobre documentacion pisa el contenido con las nuevas respuestas. Al final de la documentaciñón y de las preguntas generadas por el orquestador, surge el proyecto también, por lo que las preguntas y respuestas realziadas deben sobrescribir lo anterior y armonizar todo el contenido de la carpeta"

## User Scenarios & Testing (mandatory)

### User Story 1 - Claude descubre e invoca herramientas servidas desde C# (Priority: P1)

Como autor del proyecto, arranco un servidor MCP escrito en C# sobre .NET 8, lo registro en Claude Code, y compruebo que Claude ve las herramientas que he declarado y puede invocarlas con parámetros. Nada de Revit, nada de pipes, nada de Roslyn: solo el protocolo, aislado, para saber si el SDK aguanta antes de construir el proyecto entero encima.

Why this priority: es la hipótesis central del PoC y lo único que bloquea ADR-001. Si esto no funciona, el proceso puente vuelve a Node y TypeScript y hay que rehacer también ADR-004, porque el contrato compartido deja de ser código compilado y pasa a ser un esquema duplicado. Las otras dos historias no tienen sentido si esta falla.

Independent Test: se puede probar por completo abriendo una sesión de Claude Code con el servidor registrado, pidiendo la lista de herramientas y llamando a la que lleva parámetros. Entrega valor por sí sola: un veredicto sobre ADR-001.

Acceptance Scenarios:

1. Given el servidor del PoC registrado como servidor MCP en Claude Code, When se consulta la lista de herramientas disponibles, Then aparecen las dos herramientas declaradas por el PoC con su esquema visible.
2. Given la herramienta con parámetros tipados, When se invoca con valores válidos, Then devuelve la respuesta fija esperada sin error de protocolo.
3. Given la herramienta sin parámetros, When se invoca, Then devuelve su respuesta fija sin error de protocolo.
4. Given la herramienta con parámetros tipados, When se invoca con un valor que no respeta el esquema, Then el error se manifiesta de forma diagnosticable y queda registrado cómo se manifiesta.

### User Story 2 - Un fallo viaja dentro de una respuesta correcta (Priority: P2)

Verifico que el SDK permite devolver una respuesta MCP correcta cuyo contenido describe un fallo, con los campos `ok`, `fase`, `error` y `traza`, y que ese contenido llega íntegro a Claude sin que el cliente lo resuma ni lo recorte.

Why this priority: ADR-007 depende de que esto sea posible. Si el SDK obliga a marcar el fallo como error de protocolo, o si el contenido llega recortado, se pierde la traza, que es el único dato que permite a Claude corregirse, y el triaje por fase deja de funcionar. Es la segunda decisión que este PoC puede tumbar, pero solo importa si la Historia 1 ha pasado.

Independent Test: invocando una herramienta del PoC que devuelve deliberadamente un contenido de error con una traza larga, y comprobando que llega completa.

Acceptance Scenarios:

1. Given una herramienta que devuelve un contenido con `ok: false`, `fase: runtime` y una traza multilínea, When se invoca desde Claude Code, Then la respuesta se recibe como llamada correcta y la traza llega íntegra.
2. Given ese mismo contenido, When se compara lo recibido con lo emitido, Then coinciden carácter por carácter en el campo de traza.

### User Story 3 - El puente se distribuye sin exigir runtime instalado (Priority: P3)

Publico el PoC como ejecutable autocontenido `win-x64` y confirmo que arranca, para saber que la vía de distribución sin exigir runtime es viable.

Why this priority: es la ventaja concreta que justificó elegir C# sobre Node, y sostiene ADR-010. No bloquea el arranque del proyecto como las otras dos. La verificación en un entorno limpio sin .NET 8 se **aplaza al empaquetado**: aquí solo se comprueba que la publicación autocontenida funciona, no que el binario resultante sea autosuficiente en una máquina ajena.

Independent Test: `dotnet publish --self-contained` y arranque del binario resultante.

Acceptance Scenarios:

1. Given el proyecto del PoC, When se publica como autocontenido `win-x64`, Then la publicación termina sin error y produce un ejecutable.
2. Given ese ejecutable, When se arranca en la máquina de desarrollo, Then responde al protocolo MCP igual que el proyecto sin publicar.

## Edge Cases

- **SDK con stdio pero sin esquema tipado**: resuelto, es suspenso (FR-012). No se acepta como resultado parcial.
- **Paquete solo en preview**: resuelto, es suspenso (FR-011).
- ¿Cómo se distingue un fallo del SDK de un fallo de registro del servidor en Claude Code? Los dos se manifiestan igual desde fuera: la herramienta no aparece. El PoC debe dejar constancia de cómo lo distinguió, porque volverá a aparecer en el Tier 0.
- **Truncado de contenidos por tamaño**: no se fija umbral en este PoC (SC-003). Si durante la prueba se observa un recorte, se anota el tamaño al que ocurre; si no se observa, no se investiga.
- ¿Qué pasa si el ejecutable autocontenido arranca pero tarda mucho en responder al handshake? Un arranque lento puede leerse como servidor caído.

## Requirements (mandatory)

### Functional Requirements

- FR-001: El PoC MUST declarar al menos dos herramientas MCP, una sin parámetros y otra con parámetros tipados con esquema.
- FR-002: El PoC MUST servir esas herramientas por stdio y ser registrable como servidor MCP en Claude Code.
- FR-003: El PoC MUST devolver respuestas fijas, sin tocar la API de Revit, sin named pipes y sin Roslyn. El aislamiento es el diseño del experimento, no una simplificación.
- FR-004: El PoC MUST demostrar que un fallo puede viajar dentro de una respuesta correcta, con los campos `ok`, `fase`, `error` y `traza`, y que el contenido llega íntegro.
- FR-005: El PoC MUST publicarse como ejecutable autocontenido `win-x64` y arrancar en la máquina de desarrollo. La verificación en un entorno sin .NET 8 instalado queda **aplazada al empaquetado**: no forma parte de este PoC y no condiciona su veredicto.
- FR-006: El PoC MUST arrancar tomando como punto de partida el paquete `ModelContextProtocol` (SDK oficial de C#, repositorio `modelcontextprotocol/csharp-sdk`). Es un punto de partida **sin verificar**, no un hecho: confirmar que existe, con qué nombre e identificador exactos y en qué versión, es parte del trabajo del PoC.
- FR-007: Al cerrar, el PoC MUST anotar el nombre y la versión exactos del paquete en el Tech Stack y en Dependencies de `specs/tech-spec.md`, sustituyendo los `TBD`.
- FR-008: Al cerrar, el PoC MUST dejar por escrito el veredicto sobre ADR-001, confirmado o revertido, y marcar como resuelto el Discovery bloqueante correspondiente del TechSpec.
- FR-009: Si el veredicto es negativo, el PoC MUST dejar constancia de qué criterio falló y de que ADR-004 queda afectado en cascada.
- FR-010: El proyecto del PoC MUST vivir en `pocs/001-poc-1-sdk-oficial-de-mcp-para-net/`, fuera de `src/`, y se descarta o archiva al cerrarlo. F0.1 levanta el monorepo definitivo desde cero: el código de experimento no se arrastra al producto.
- FR-011: Si el paquete del SDK solo está disponible en versión **preview**, el PoC MUST cerrarse en **suspenso**. No se construye el proyecto entero sobre una API que puede cambiar bajo los pies; en ese caso gana el ecosistema maduro de TypeScript y ADR-001 se revierte.
- FR-012: Si el SDK cubre el transporte stdio pero **no** la declaración de herramientas con esquema tipado, el PoC MUST cerrarse en **suspenso**. Sin esquema, los comandos compilados dejan de parecer la opción natural frente a la escotilla de Roslyn, y esa asimetría es el mecanismo que mantiene a Roslyn fuera del camino por defecto (§3 de `DOCUMENTACION.md`). No se acepta como resultado parcial.

### Key Entities

- **Herramienta declarada**: la unidad que Claude descubre e invoca. Atributos relevantes para el experimento: nombre, descripción y esquema de parámetros. Sin persistencia.
- **Respuesta del PoC**: el contenido devuelto, con la forma que fija el contrato de la pasarela (`ok`, `fase`, `resultado`, `error`, `traza`). Aquí es fija y simulada; su valor es comprobar que el SDK la transporta sin alterarla.

## Success Criteria (mandatory)

### Measurable Outcomes

- SC-001: Las dos herramientas declaradas aparecen listadas en una sesión real de Claude Code, con su esquema visible.
- SC-002: Los tres casos se ejecutan una vez cada uno, sin ningún error de protocolo: herramienta sin parámetros, herramienta con parámetros válidos, y herramienta con parámetros que violan el esquema. Es un criterio binario: o el protocolo funciona o no. Repetir la misma llamada no añade información.
- SC-003: Una traza multilínea emitida dentro de una respuesta correcta se recibe íntegra, verificada por comparación exacta del campo. Sin tamaño mínimo fijado: basta con una traza normal, y el límite real se descubrirá con uso real.
- SC-004: El ejecutable autocontenido se publica y arranca en la máquina de desarrollo. La ejecución en un entorno sin .NET 8 instalado queda **aplazada al empaquetado** y no se evalúa en este PoC.
- SC-005: Los cuatro criterios de éxito del PoC en `specs/roadmap.md` quedan marcados con su resultado, y el Gate Fase 0 puede evaluarse sin ambigüedad respecto a este PoC.
- SC-006: `specs/tech-spec.md` queda sin ningún `TBD` atribuible al SDK de MCP: nombre, versión y veredicto de ADR-001 escritos.

## Assumptions

- Existe un SDK oficial de MCP para .NET publicado como paquete NuGet, presumiblemente `ModelContextProtocol` del repositorio `modelcontextprotocol/csharp-sdk`. Es la hipótesis del PoC y el punto de partida acordado, **no un hecho verificado**; si no existe, el PoC se cierra en negativo de inmediato.
- Claude Code permite registrar un servidor MCP local que se arranca como subproceso por stdio, que es el mecanismo asumido en toda la arquitectura.
- El PoC no necesita Revit instalado ni abierto en ningún momento.
- El entorno de desarrollo es Windows 10 con .NET 8 y `win-x64` como único target relevante.
- No hace falta un entorno limpio sin .NET 8: ese criterio se aplazó al empaquetado.
- El PoC #2 avanza en paralelo y no comparte código ni salida con este, según la nota de paralelización del roadmap.
- El PoC vive fuera de `src/` y es desechable, así que su calidad de código no es un criterio: solo cuenta el veredicto que produce.
