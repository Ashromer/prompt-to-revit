---
name: harvest-orchestration-log
description: Destila `.claude/orchestration-log.md` (log exhaustivo del propio ciclo specify/plan/implement) en cambios puntuales a CLAUDE.md, roadmap.md o la agrupación en lotes, sin releer el log crudo en el camino normal. Actívala cuando el usuario diga "cosecha el log de orquestación", "qué hemos aprendido del proceso", "revisa cómo está yendo la agrupación de lotes", o invoque /harvest-orchestration-log.
---

# harvest-orchestration-log

Análogo a `/harvest-bridge-log` pero para el **proceso de desarrollo**, no para el producto. El log
de `.claude/orchestration-log.md` es exhaustivo a propósito y nadie lo relee por defecto; esta skill
es el único momento en que se lee entero, y solo cuando el usuario lo pide.

## Paso 1 — Leer el log

`.claude/orchestration-log.md` completo. Es pequeño por diseño (una entrada corta por lote/feature,
no por línea de código), así que leerlo entero aquí no es el problema que la skill existe para
evitar — el problema era releerlo en cada arranque de subagente.

## Paso 2 — Detectar patrones

- **Saltos que se repiten sin incidencias** (ej. "architect omitido" en 3 lotes seguidos sin que
  `judge` haya pedido cambios de diseño) → candidato a formalizar como regla permanente en
  `CLAUDE.md` (tabla de reparto de agentes) en vez de decidirse lote a lote.
- **Saltos que causaron un CHANGES_REQUESTED o un fallo** → la ceremonia que se saltó hacía falta;
  proponer revertir ese salto para el resto del tier.
- **Coste real por lote** (si el usuario aporta el dato de tokens/tiempo desde su dashboard) versus
  la estimación hecha al planificar la agrupación — ajustar la estimación del siguiente tier con el
  dato real en vez de la proyección.

## Paso 3 — Entregar

1. **Resumen**: nº de lotes cerrados, saltos aplicados y su tasa de éxito (¿algún salto causó
   retrabajo?).
2. **Propuestas de regla permanente** para `CLAUDE.md` §Reparto de agentes, redactadas y listas
   para pegar.
3. **Recomendación para el siguiente tier**: mantener la misma agrupación, ajustarla, o volver a más
   ceremonia en los puntos donde falló.
4. **Descartes**: entradas del log que no aportan patrón (ruido de un solo lote).

No edites `CLAUDE.md` ni `roadmap.md` sin que el usuario apruebe la propuesta — igual que
`/harvest-bridge-log`, esto es proceso, no un parche directo.
