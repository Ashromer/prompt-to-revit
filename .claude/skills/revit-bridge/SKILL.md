---
name: revit-bridge
description: Disciplina de uso de la pasarela PROMPT_TO_REVIT — modelar y consultar en Revit 2026 desde la conversación vía MCP. Impone el orden query → command → compile → exec, el contrato del snippet Roslyn, los niveles de aprobación y el triaje de errores. Actívala en cuanto la petición implique leer, crear o modificar algo en el modelo de Revit abierto, o invocando /revit-bridge.
---

# revit-bridge

> **Fuentes únicas de las reglas:** `DOCUMENTACION.md` (diseño: §3 vías, §4 contrato, §5 salvaguardas)
> y `~/.claude/revit_knowledge/revit_api_knowledge.md` (verdad operativa de la API). No dupliques
> reglas aquí; esta skill es el procedimiento, no el reglamento.

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
| `exec` en ámbito de sesión | manual por defecto (con opción "confiar 30 min") |
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

## Antes de empezar a trabajar

Que el usuario **guarde**, sobre archivo local, nunca directamente sobre modelo central compartido.

## Al terminar algo que funcionó

Dilo explícitamente: un snippet que se usa y se mantiene estable **gradúa** a comando compilado
(ver `/harvest-bridge-log`). Y si el error fue nuevo y no obvio, apuntarlo para
`revit_api_knowledge.md`.
