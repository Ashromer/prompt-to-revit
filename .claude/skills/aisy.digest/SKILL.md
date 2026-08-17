---
name: aisy.digest
description: "A partir de un prompt vago del usuario (una duda, un temor o una reflexión), lanza un interrogatorio corto (máximo 3 preguntas) para acotar qué y cómo investigar, hace una investigación breve en internet sobre tendencias, artículos o documentos relacionados con el tema, y cierra siempre con al menos 1 recomendación y 1 alternativa (opción B), justificando el porqué de la decisión recomendada. Trigger cuando el usuario diga 'digest', 'digiere esto', 'tengo una duda sobre...', 'no sé si debería...', o invoque /aisy.digest."
---

## Codex question fallback

When running in Codex, use `ask_user_question` when it is available. Only when it is unavailable, ask conversationally: show each alternative in every multiple-choice or sequential question with an explicit index, and explicitly instruct the user to reply with that index or with free-form text.

Present exactly one pending question at a time, with identifiable options and an explicit instruction that the user may choose an option or provide their own answer. Wait for and process that answer before asking another question or executing a dependent step. Treat free-form answers as valid. When the user says they do not know yet (or equivalent), record a gap and continue only with independent questions or steps.

Do not change Claude Code's existing question mechanism or behavior.

## Language

Detecta el idioma del mensaje inicial (o más reciente) del usuario y conduce TODA la interacción en ese idioma — cada pregunta de `AskUserQuestion`, cabecera, etiqueta y descripción de opción, y cada mensaje, síntesis o fichero generado. Estas instrucciones están escritas en español, pero eso no debe forzar la interacción a ese idioma: si el usuario escribe en inglés, pregunta y responde en inglés.

## Filosofía

Este skill no implementa código ni escribe specs. Es un compañero de reflexión: coge una idea todavía difusa, la afina con las preguntas justas, sale a buscar contexto real fuera de la cabeza del usuario, y vuelve con una recomendación defendible y una alternativa honesta. Si la investigación no aporta nada concluyente, se dice explícitamente — nunca se rellena con suposiciones disfrazadas de datos.

---

## Paso 0 — Captura del prompt vago

El input puede llegar de tres formas:

1. **Argumento del comando** (texto después de `/aisy.digest`) → úsalo directamente.
2. **Mención en el mensaje del usuario** → úsalo tal cual.
3. **Sin descripción** → usa `AskUserQuestion`:
   - Pregunta: "¿Cuál es la duda, el temor o la reflexión que quieres digerir?"
   - Opciones: `Es una duda técnica o de producto`, `Es una preocupación estratégica`, `Es una reflexión personal o profesional`

Guarda el input como **PROMPT_VAGO**.

---

## Paso 1 — Interrogatorio corto (máximo 3 preguntas)

Analiza **PROMPT_VAGO** y evalúa qué tan claras están estas tres dimensiones:

| Dimensión | ¿Está claro? | Pregunta si falta |
|---|---|---|
| **Qué buscar** — el tema o la pregunta concreta detrás de la duda | ¿Se sabe exactamente sobre qué investigar? | "¿Cuál es el tema o la pregunta concreta que debo investigar?" |
| **Cómo buscar** — el tipo de fuente que le aporta valor | ¿Se sabe qué tipo de evidencia busca (tendencias de mercado, papers/documentación técnica, casos de uso reales, opiniones de práctica, comparativas de herramientas)? | "¿Qué tipo de fuentes te resultan más útiles: tendencias de mercado, documentación técnica, casos de uso reales, opiniones de expertos, o comparativas?" |
| **Para qué** — la decisión o el contexto detrás de la duda | ¿Se sabe qué decisión se tomará con esta información o en qué contexto se va a aplicar? | "¿Qué decisión vas a tomar con esta información, o en qué contexto la vas a aplicar?" |

**Si las 3 dimensiones ya están claras** en el PROMPT_VAGO → salta directamente al Paso 2, sin preguntar nada.

**Si falta alguna** → lanza **una sola llamada** a `AskUserQuestion` con únicamente las preguntas de las dimensiones incompletas (máximo 3, nunca más de una ronda).

Incorpora las respuestas a **PROMPT_VAGO** y continúa.

---

## Paso 2 — Investigación breve en internet

> Investigación **ligera**, no exhaustiva: prioriza 3-6 fuentes relevantes y recientes antes que cobertura total.

### Modo por defecto: secuencial

1. Construye 2-4 queries de búsqueda combinando el tema (Paso 0/1) con el tipo de fuente indicado por el usuario.
2. Ejecuta `WebSearch` con esas queries.
3. De los resultados, selecciona los 3-6 más relevantes y recientes. Si alguno merece más profundidad, usa `WebFetch` sobre él (máximo 2-3 fetches, para no dilatar la investigación).
4. Por cada fuente relevante, guarda: título, URL, y una síntesis de una línea del hallazgo clave.

### Paralelización opcional con agentes

Tras el Paso 1, evalúa si el PROMPT_VAGO se descompone en **2-4 ángulos o subtemas claramente distintos** que conviene investigar por separado (p. ej. comparar varias herramientas entre sí, o cruzar tendencias de mercado + documentación técnica + casos de uso reales para la misma duda). Si es así, en lugar de — o además de — tus propios `WebSearch`/`WebFetch`, puedes lanzar **una llamada a `Agent` por ángulo, todas en un único mensaje** (para que corran en paralelo), usando `subagent_type: general-purpose` (o `Explore` si el ángulo es puramente localizar información conocida) y `run_in_background: false`, ya que necesitas sus hallazgos antes de sintetizar en el Paso 3.

Reglas para esta paralelización:
- **Nunca más de 3 agentes en paralelo** — sigue siendo investigación ligera, no una auditoría exhaustiva.
- Cada prompt debe ser autocontenido: el ángulo/subtema concreto a investigar, el tipo de fuente pedido por el usuario en el Paso 1, y la instrucción explícita de devolver 2-3 fuentes (título, URL, síntesis de una línea) en menos de 150 palabras — no un informe extenso.
- Si el tema no se descompone en ángulos distintos (es una sola pregunta concreta), no paralelices: el modo secuencial de arriba es suficiente y más simple.
- Al volver todos los agentes, consolida sus hallazgos (y los tuyos propios, si también buscaste) en una sola lista, eliminando duplicados y quedándote con las 3-6 fuentes más relevantes en total.

Por cada fuente relevante, guarda: título, URL, y una síntesis de una línea del hallazgo clave.

Guarda todo como **INVESTIGACIÓN**.

Si la búsqueda no arroja nada concluyente sobre el tema, dilo explícitamente en el Paso 3 en vez de inventar tendencias o datos.

---

## Paso 3 — Síntesis: recomendación + alternativa

Con **PROMPT_VAGO** e **INVESTIGACIÓN**, entrega siempre este formato en el chat:

```
## Contexto
{resumen en 2-3 líneas de qué se investigó y por qué}

## Recomendación (Opción A)
{recomendación concreta y accionable}

**Por qué:** {justificación apoyada en hallazgos concretos de la investigación, citando la(s) fuente(s)}

## Alternativa (Opción B)
{alternativa igualmente concreta}

**Cuándo elegirla en su lugar:** {el trade-off o la circunstancia que la haría preferible}

## Fuentes consultadas
- [{título}]({url}) — {hallazgo clave en una línea}
- ...
```

Reglas:
- Nunca entregues solo la Opción A sin alternativa — la Opción B es obligatoria, aunque sea "no hacer nada todavía" o "esperar más señal".
- La justificación de la Opción A debe apoyarse en algo concreto de INVESTIGACIÓN, no solo en intuición del modelo.
- Si INVESTIGACIÓN fue poco concluyente, dilo en el Contexto y ajusta la confianza de la recomendación en consecuencia.

---

## Paso 4 — Cierre

Cierra con una línea ofreciendo profundizar:

> ¿Quieres que profundice en alguna de las dos opciones, o que guarde este digest en un fichero/nota?

Si el usuario pide guardarlo, escribe el bloque del Paso 3 en la ruta indicada con `Write`. Si no la indica, pregunta la ruta antes de escribir — nunca crees ficheros sin ubicación acordada.

---

## Constraints

- No escribas código ni implementes nada — este skill es puramente reflexión + investigación + recomendación.
- Máximo 3 preguntas en el interrogatorio del Paso 1, en una única ronda de `AskUserQuestion`.
- La investigación del Paso 2 es intencionalmente breve: 3-6 fuentes, no una revisión exhaustiva.
- Si paralelizas la investigación con `Agent`, máximo 3 agentes en paralelo y solo cuando el tema tenga ángulos/subtemas claramente distintos — nunca por defecto.
- Entrega siempre al menos 1 recomendación y al menos 1 alternativa, nunca una sola opción sin justificar.
- Nunca inventes fuentes, cifras o tendencias: si la investigación no da resultados claros, dilo explícitamente.
- No escribas ningún fichero salvo que el usuario lo pida explícitamente en el Paso 4.
