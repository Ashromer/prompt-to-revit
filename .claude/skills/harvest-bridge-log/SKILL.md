---
name: harvest-bridge-log
description: Destila el log JSONL de la pasarela (%APPDATA%\RevitBridge\log\YYYY-MM.jsonl) en dos productos — snippets estables que graduan a comando compilado, y errores recurrentes que se vuelcan a revit_api_knowledge.md. Actívala cuando el usuario diga "cosecha el log", "qué gradúa", "revisa el log del bridge", o invoque /harvest-bridge-log.
---

# harvest-bridge-log

Implementa el ciclo de aprendizaje de `DOCUMENTACION.md` §6. Esto **no es fine-tuning**: es
acumular un corpus que se carga como contexto. Lo más valioso del log no es la geometría — es que
captura **este entorno concreto**: esta plantilla, estas familias cargadas, los nombres de tipo
reales. Eso es justo lo que ningún conocimiento general aporta, y es el modo de fallo principal.

## Paso 1 — Leer el log

`%APPDATA%\RevitBridge\log\YYYY-MM.jsonl`, una línea JSON por ejecución:

```json
{"ts":"...","intencion":"...","via":"roslyn","fuente":"...","fase":"runtime","ok":false,
 "error":"...","ids_creados":[],"duracion_ms":340}
```

Por defecto el mes en curso. Si el usuario pide un rango, leer los ficheros que toquen. Si no existe
el directorio, decirlo y parar: no hay nada que cosechar todavía y el bridge quizá no se ha usado.

## Paso 2 — Candidatos a graduar

Un snippet gradúa a comando compilado cuando **se usa y se mantiene estable**. Señales, en orden
de peso:

1. **Repetición**: la misma `intencion` (o equivalente semántico) aparece ≥3 veces.
2. **Estabilidad**: sus últimas ejecuciones son `ok`, sin variaciones de la `fuente` entre ellas.
3. **Coste**: `duracion_ms` alto o muchos intentos previos fallidos antes de acertar — graduarlo
   ahorra ese descubrimiento cada vez.

No gradúes por elegancia ni por "esto seguro que hace falta". El catálogo se puebla con lo que
**realmente se usa**, no con lo que alguien imaginó de antemano. Un comando que nadie invoca es
superficie de mantenimiento a cambio de nada.

Para cada candidato, propón: nombre del comando, firma (parámetros tipados que sustituyen a los
valores hardcodeados del snippet), y el snippet consolidado. **El nombre debe ser idéntico** en el
servidor MCP y en el commandset del addin, o el modelo no lo encuentra y no hay error que lo delate.

## Paso 3 — Errores recurrentes

Agrupar las líneas con `ok:false` por causa real, no por texto del mensaje (el mismo problema llega
con redacciones distintas, y a veces con `Message` vacío). Separar:

- **`fase: compilacion`** → rotura de API por versión o un tipo que no existe en 2026. Va a la tabla
  de roturas de `revit_api_knowledge.md`.
- **`fase: runtime`** → supuesto falso sobre el documento (id inexistente, nombre de tipo que no está
  en esta plantilla, nivel duplicado). Es lo más valioso: es conocimiento de **este entorno**.
- **Ruido**: fallos de un solo intento que se corrigieron acto seguido. No ensuciar la base de
  conocimiento con ellos.

Un error merece entrada en `revit_api_knowledge.md` si es **recurrente y no obvio**. Si aparece una
vez y era un despiste, se descarta.

## Paso 4 — Entregar

Un informe con:

1. **Resumen**: ejecuciones totales, tasa de `ok`, reparto `roslyn` vs `command` (si el ratio de
   Roslyn no baja con el tiempo, el catálogo no está madurando y hay que decirlo).
2. **Candidatos a graduar**, con la evidencia (cuántas veces, desde cuándo, estables sí/no).
3. **Entradas propuestas para `~/.claude/revit_knowledge/revit_api_knowledge.md`**, ya redactadas
   en el estilo del fichero (patrón / causa / solución), listas para pegar.
4. **Lo que descartaste y por qué** — una línea por descarte. Es lo que evita cosechar dos veces
   el mismo ruido.

No escribas en `revit_api_knowledge.md` ni crees comandos sin que el usuario apruebe la propuesta.
Graduar un comando es código nuevo en producción: pasa por el flujo normal
(`/aisy.specify-feature` → `plan` → `implement`), no por un parche directo.
