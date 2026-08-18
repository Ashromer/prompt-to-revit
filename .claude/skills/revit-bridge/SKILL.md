---
name: revit-bridge
description: Disciplina de uso de la pasarela PROMPT_TO_REVIT — modelar y consultar en Revit 2026 desde la conversación vía MCP. Impone el orden query → command → compile → exec, el contrato del snippet Roslyn, los niveles de aprobación y el triaje de errores. Actívala en cuanto la petición implique leer, crear o modificar algo en el modelo de Revit abierto, o invocando /revit-bridge.
---

# revit-bridge

> **Fuentes únicas de las reglas:** `DOCUMENTACION.md` (diseño: §3 vías, §4 contrato, §5 salvaguardas)
> y `~/.claude/revit_knowledge/revit_api_knowledge.md` (verdad operativa de la API). No dupliques
> reglas aquí; esta skill es el procedimiento, no el reglamento.

## Paso 0 — Contexto denso al empezar a trabajar sobre un documento (ADR-011, F3.1)

Antes de la primera consulta puntual en una sesión de trabajo sobre un documento, invoca
`run_command` con `ExportarContextoMasivo` y `ExportarGrafoTopologico`. Es lectura pura (auto-
aprobado), y evita descubrir niveles, hojas, inventario por categoría y el grafo puerta-habitación
a base de preguntas sueltas. No hace falta repetirlo dentro de la misma sesión salvo que el usuario
avise de un cambio grande en el modelo.

## La escalera. En este orden, siempre

**1. `/query` antes de nada.** Nunca escribas un nombre de tipo, familia, nivel o material a mano
(`"Generic - 200mm"`). Aunque Revit esté en inglés, los nombres dependen de la plantilla y de qué
familias estén cargadas en **este** documento. Resuelve los `ElementId` reales primero; el código
usa ids, no cadenas. Es lectura pura, sin transacción y sin aprobación: es gratis, úsalo siempre.

**2. `GET /commands` antes de improvisar.** Si existe un comando compilado que cubre la operación,
se usa ese. Es código ya probado. Roslyn no es la vía por defecto.

**3. `/compile` como dry-run.** Antes de ejecutar C# nuevo, compílalo sin ejecutarlo. Valida
sintaxis y tipos sin tocar el modelo. Un fallo de compilación cuesta ~1 s; el mismo fallo en
runtime dentro de una transacción cuesta mucho más y ensucia el historial de deshacer.

**4. `/exec` solo si ninguna herramienta anterior cubre la operación.** Es la escotilla de
emergencia. Al usarla, di en el mensaje por qué ningún comando del catálogo servía.

**5. `/rollback` como cinturón.** Borra los elementos creados en la sesión. No sustituye al Ctrl+Z
del usuario, lo complementa.

## Contrato del snippet

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

Reglas del código que generes:

- **Cota explícita de iteraciones en todo bucle.** Revit es monohilo y **no hay timeout real**: un
  bucle infinito congela Revit sin poder matarlo desde fuera sin perder el trabajo. Esta cota es la
  única defensa real. No la omitas nunca, ni en un bucle "que obviamente termina".
- **Devuelve los ids creados.** Sin ids no hay `/rollback` ni retroalimentación.
- **Nada de `System.IO`, `System.Net`, `System.Diagnostics.Process`, `Environment.Exit`,
  `Document.SaveAs`, `Document.Close`, ni `doc.Delete`.** El filtro sintáctico los rechaza antes de
  compilar, y además no tienen por qué aparecer en código de modelado. Si crees necesitarlos, el
  enfoque está mal. Para borrar existe una vía dedicada con previsualización y confirmación.
- **Ámbito por defecto = lo creado en esta sesión.** Modificar elementos preexistentes exige
  intención explícita del usuario; borrar, aprobación manual sin excepción.
- **Un solo documento.** No abras ni modifiques otros documentos ni documentos de familia.

## Aprobación: qué esperar

| Operación | Aprobación |
|---|---|
| `query`, `commands`, `compile` | automática siempre |
| `exec` | **manual siempre en la v1.** No existe "confiar durante N minutos" — se descartó a propósito (DOCUMENTACION.md §5.D.15): mientras el sistema no se demuestre, la revisión humana no debe tener agujeros |
| Creación masiva (`CrearMurosMasivo`, `CrearForjadosMasivo`, `CrearAberturasMasivo`, `ColocarMobiliarioMasivo`, tejados) | **manual siempre**, con resumen de cuántos elementos y dónde — no es específico de VLM/CAD, es la misma salvaguarda que aplica a cualquier lote grande |
| Borrar o modificar preexistentes | **siempre manual**, sin excepción |

La ventana del addin muestra el C# antes de ejecutarlo. Escribe el snippet para que **se lea**:
el usuario tiene que poder aprobarlo de un vistazo. Un snippet ilegible es un snippet que se
rechaza, y con razón. La revisión humana es la salvaguarda más importante del diseño.

## Triaje de errores

La respuesta trae `fase`: `compilacion` | `runtime` | `ok`.

| `fase` | Qué significa | Qué hacer |
|---|---|---|
| `compilacion` | El C# no compila contra Revit 2026 | Casi siempre una API rota por versión: `ElementId.IntegerValue`→`.Value`, `NewFloor`→`Floor.Create`, `DisplayUnitType`→`ForgeTypeId`. Consultar la tabla de §7 de `DOCUMENTACION.md` |
| `runtime` | Compiló y falló contra el modelo | Casi siempre un id o un supuesto sobre el documento. **Volver a `/query`**, no reintentar el mismo snippet |
| `ok` con resultado vacío | El filtro no encontró nada | El nombre/categoría no existe en esta plantilla. `/query` de nuevo, no insistir |

Varias excepciones de la API llegan con **`Message` vacío**: si el error no dice nada, mirar
`ex.GetType().Name` y la cadena de `InnerException` completa en la traza.

**Dos fallos seguidos en la misma operación = parar y preguntar.** No entres en bucle de reintentos
contra el modelo del usuario.

## Modelar desde un plano (CAD o PDF/imagen) — ADR-012 y ADR-013

Dos vías distintas según la fuente, con la misma regla de fondo: **nunca generar el lote completo
sin anclar la escala y sin que el usuario vea un resumen antes de que exista en el modelo.**

### Desde CAD (DXF/DWG)

1. `cad_list_layers(ruta)` — lista las capas con conteo de líneas/polilíneas/inserts. **No
   adivines** qué capa es "muros" por el nombre a ciegas: confírmalo con el usuario en el chat si
   hay ambigüedad (nombres de capa varían por despacho).
2. `cad_calibrate_scale(ruta)` — si `confianza` no es `DesdeCabecera` (fichero `Unitless` o
   unidades sin factor conocido), **pide al usuario la escala explícitamente** antes de seguir. No
   asumas metros ni milímetros por defecto.
3. `cad_extract_geometry(ruta, nombreCapa, factorAMetros)` — devuelve segmentos ya en metros, en el
   formato que `CrearMurosMasivo` acepta directamente.
4. Antes de invocar `CrearMurosMasivo`/`CrearForjadosMasivo`/`CrearAberturasMasivo` con el
   resultado: dilo en el chat ("voy a crear N muros en el nivel X") — la ventana de aprobación del
   addin lo exige igualmente, pero narrarlo antes ayuda al usuario a juzgar con contexto.
5. Arcos de polilínea (bulge) se teselan automáticamente a ≤12° de barrido: el recuento de "muros
   creados" no coincidirá con el recuento de segmentos del plano original si hay curvas — es
   esperado, no un error.

### Desde PDF o imagen (croquis, escaneo, foto de un plano)

No hace falta ninguna herramienta especial de visión: interpreta la imagen directamente (ya tienes
visión nativa), y genera el JSON de coordenadas a mano, con este orden obligatorio:

1. **Ancla la escala primero.** Busca una cota legible en el plano (acotación o escala gráfica). Si
   no hay ninguna, pídesela al usuario — nunca infieras una escala absoluta solo de proporciones de
   píxeles, es el modo de fallo más probable de todo el flujo.
2. **Genera un solo muro de prueba** con `CrearMuroRecto` (no el batch) en la posición y longitud
   que debería tener según el plano y la escala del paso 1.
3. **El usuario lo compara visualmente** contra el plano antes de seguir. Si no coincide, corrige
   la escala o la lectura del plano — no sigas adelante con un ancla que no se confirmó.
4. Solo entonces, lanza el lote completo con `CrearMurosMasivo`/`CrearForjadosMasivo`.
5. Un PDF con geometría vectorial real (no una imagen escaneada) es mejor candidato para la vía CAD
   si se puede exportar a DXF/DWG — la interpretación visual es para cuando no hay otra opción, no
   la vía por defecto si existe un fichero CAD real detrás.

## Antes de empezar a trabajar

Que el usuario **guarde**, sobre archivo local, nunca directamente sobre modelo central compartido.

## Al terminar algo que funcionó

Dilo explícitamente: un snippet que se usa y se mantiene estable **gradúa** a comando compilado
(ver `/harvest-bridge-log`). Y si el error fue nuevo y no obvio, apuntarlo para
`revit_api_knowledge.md`.
