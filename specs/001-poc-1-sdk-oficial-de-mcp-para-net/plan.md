# Plan — PoC #1: SDK oficial de MCP para .NET

Requirements: `specs/001-poc-1-sdk-oficial-de-mcp-para-net/requirements.md`

## Contexto de ejecución

Este PoC vale por el **veredicto que produce**, no por el código. Vive en
`pocs/001-poc-1-sdk-oficial-de-mcp-para-net/`, fuera de `src/`, y se descarta al cerrarlo (FR-010).
La calidad del código no es criterio de aceptación: no refactorizar, no abstraer, no generalizar.

**Aclaración del usuario incorporada — escalera de 3 peldaños.** Sustituye al "suspenso = vuelta a
Node" de FR-011 y FR-012:

1. **SDK estable y completo** (stdio + declaración con esquema tipado) → ADR-001 **confirmado**.
2. **SDK en preview, o sin esquema tipado** → no se usa el SDK, pero **no se cambia de lenguaje**:
   se implementa a mano el subconjunto mínimo de MCP en C# (JSON-RPC 2.0 sobre stdio con
   `initialize`, `tools/list`, `tools/call`). ADR-001 y ADR-004 **sobreviven**, sin cascada.
3. **Ni el SDK ni la implementación propia consiguen que Claude Code hable con el servidor** →
   entonces sí, Node y TypeScript, y ADR-004 se rehace.

Coste asumido del peldaño 2: MCP evoluciona y una implementación propia hay que mantenerla al paso
de lo que hable Claude Code. Es la razón de que sea plan B y no plan A.

**Qué no puede verificar ningún agente.** Los agentes compilan, publican y ejecutan procesos, pero
**no pueden abrir una sesión de Claude Code ni registrar un servidor MCP**. SC-001, SC-002 y SC-003
los confirma el usuario, igual que la verificación en Revit vivo. Un agente que reporte esos
criterios como cumplidos sin que el usuario lo haya confirmado está mintiendo.

---

## Lote 1 — Reconocimiento (bloquea todo lo demás)

- [x] @architect · Verificar el paquete del SDK: confirmar si existe el paquete `ModelContextProtocol` (repositorio `modelcontextprotocol/csharp-sdk`), su identificador exacto en NuGet, su versión más reciente, y si esa versión es estable o preview. Anotar fecha de último release y actividad del repositorio. Si no existe ningún SDK oficial de MCP para .NET, cerrar el PoC en negativo aquí mismo y saltar al Lote 4. **Resultado**: `ModelContextProtocol` 2.2.0, estable, repositorio activo (push 2026-08-13). Verificado en `RECONOCIMIENTO.md`.
- [x] @architect · Determinar el peldaño de la escalera: a partir de la verificación anterior y de la documentación del SDK, decidir si el paquete cubre transporte stdio Y declaración de herramientas con esquema tipado. Estable y completo → peldaño 1, se continúa con el SDK. Preview o sin esquema → peldaño 2, se continúa con implementación propia. Dejar la decisión por escrito con su razón antes de escribir una línea de código. **Resultado**: Peldaño 1. 2.2.0 es estable, stdio + typed schemas verificados en docs `/v2/` y samples del tag. Decisión documentada en `DECISION-PELDANO.md`.

---

## Lote 2 — El experimento (depende del Lote 1)

- [x] @mcp-developer · Crear el proyecto del PoC: proyecto `net8.0` mínimo en `pocs/001-poc-1-sdk-oficial-de-mcp-para-net/`, fuera de la solución de `src/`. Sin referencias a Revit, sin named pipes, sin Roslyn: el aislamiento es el diseño del experimento (FR-003). Según el peldaño del Lote 1, con el paquete del SDK o con implementación propia del subconjunto JSON-RPC.
- [x] @mcp-developer · Declarar las dos herramientas: una sin parámetros y otra con parámetros tipados con esquema, ambas devolviendo respuestas fijas (FR-001). Los nombres y descripciones deben ser reconocibles en la lista de herramientas de Claude Code.
- [x] @mcp-developer · Añadir la herramienta de error: una tercera herramienta que devuelve deliberadamente un contenido con `ok: false`, `fase: runtime`, `error` y una `traza` multilínea, como **respuesta correcta** y no como error de protocolo (FR-004). La traza debe incluir un marcador único al principio y otro al final, para poder detectar un recorte por comparación exacta.
- [x] @mcp-developer · Servir por stdio y documentar el registro: dejar el servidor arrancable por stdio y escribir en el README del PoC los pasos exactos para registrarlo en Claude Code, porque ese registro lo hará el usuario a mano (FR-002).
- [x] @mcp-developer · Instrumentar los métodos JSON-RPC ejercitados: registrar en un fichero de texto local qué métodos del protocolo invoca realmente Claude Code durante las pruebas, con su frecuencia. Barato de implementar y convierte el peldaño 2 en una cantidad conocida en vez de un salto a ciegas, se acabe usando o no.
- [x] @mcp-developer · Publicar autocontenido: `dotnet publish -c Release -r win-x64 --self-contained` y comprobar que la publicación termina sin error y que el ejecutable arranca en la máquina de desarrollo (FR-005, SC-004). NO intentar verificarlo en un entorno sin .NET: ese criterio se aplazó al empaquetado.

---

## Lote 3 — Verificación (depende del Lote 2, la ejecuta el usuario)

- [x] @tester · Preparar el guion de verificación: escribir la secuencia exacta de comprobaciones que el usuario debe ejecutar en su sesión de Claude Code, con el resultado esperado de cada una y un hueco para anotar el observado. Cubre SC-001 (las herramientas aparecen con su esquema), SC-002 (los tres casos: sin parámetros, con parámetros válidos, con parámetros que violan el esquema) y SC-003 (la traza llega íntegra, verificada por los marcadores de inicio y fin).
- [ ] @tester · Recoger y diagnosticar los resultados: a partir de lo que el usuario anote, reportar qué criterios se cumplieron y cuáles no, con la evidencia real. Para cada fallo, distinguir si es del SDK, del registro del servidor en Claude Code, o del propio PoC — los tres se manifiestan igual desde fuera y esa distinción es la Edge Case abierta que volverá a aparecer en el Tier 0. Anotar cómo se distinguió.

---

## Lote 4 — Veredicto y cierre (depende del Lote 3)

- [ ] @architect · Escribir el veredicto: documento corto en la carpeta del PoC con el resultado de cada criterio, el peldaño de la escalera en el que quedó el proyecto, y la razón. Si el veredicto es negativo, dejar constancia de qué criterio concreto falló y de si ADR-004 queda afectado (FR-008, FR-009).
- [ ] @architect · Actualizar el TechSpec: sustituir los `TBD` del SDK en Tech Stack y Dependencies por el nombre e identificador exactos del paquete y su versión; marcar como resuelto el Discovery bloqueante correspondiente; y actualizar ADR-001 con el veredicto y, si aplica, con la escalera de 3 peldaños como decisión registrada (FR-007, SC-006).
- [ ] @architect · Actualizar el roadmap y los requirements: marcar los criterios del PoC #1 en `specs/roadmap.md` con su resultado para que el Gate Fase 0 pueda evaluarse sin ambigüedad (SC-005), y enmendar FR-011 y FR-012 del `requirements.md` para reflejar la escalera de 3 peldaños en lugar del "suspenso = vuelta a Node".
- [ ] @judge · Revisar el veredicto: comprobar que cada criterio declarado como cumplido tiene evidencia real confirmada por el usuario y no una inferencia, que el peldaño elegido se sigue de los hechos registrados, y que las actualizaciones de TechSpec y roadmap son consistentes entre sí. Un veredicto equivocado aquí arrastra la estructura entera del proyecto, así que es el único punto de este PoC donde una revisión independiente se paga sola.
