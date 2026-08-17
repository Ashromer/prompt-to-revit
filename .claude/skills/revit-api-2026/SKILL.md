---
name: revit-api-2026
description: Disciplina de la API de Revit 2026 con .NET 8 — carga el conocimiento acumulado antes de escribir C# contra RevitAPI.dll, y avisa de las roturas de API por versión y las colisiones de tipos WPF/WinForms/Revit. Actívala en cualquier tarea que toque la API de Revit, transacciones, geometría, familias o el addin, o invocando /revit-api-2026.
---

# revit-api-2026

> **Fuente única de las reglas:** `~/.claude/revit_knowledge/revit_api_knowledge.md`. Manda sobre
> todo lo demás y es más detallado que cualquier resumen. **Leerlo antes de escribir código**, no
> después de que falle. No dupliques sus reglas aquí.
> El diseño de este proyecto está en `DOCUMENTACION.md`; el agente que implementa es
> `revit-developer` (`.claude/agents/revit-developer.md`).

## Lo que hay que tener en la cabeza sin consultar

- **Revit 2026, .NET 8** (`net8.0-windows`, `win-x64`). Interfaz de Revit **en inglés**.
- **`ExternalEvent` o nada.** La API existe solo dentro del proceso de Revit y solo en su hilo
  principal. Cualquier camino que llegue a la API desde el `HttpListener`, un timer, un `Task` o un
  handler pasa por `ExternalEvent.Raise()`. Tocarla desde otro hilo es un crash, no un warning.
- **Una `Transaction` con nombre por operación**: `"Claude: <intención>"`. Se deshace con un Ctrl+Z
  y en el historial se ve qué vino de Claude y qué no. `TransactionGroup` + `Assimilate()`/`RollBack()`
  para multipaso; `SubTransaction` con `RollBack()` en el `catch` para reintentos.
- **Pies, no milímetros**: `private const double MmToFt = 1.0 / 304.8;`
- **`Message` vacío**: muchas excepciones de la API no dicen nada. Loguear siempre
  `ex.GetType().Name` y la cadena de `InnerException` como respaldo.
- **`Dispatcher.InvokeAsync`**, nunca `Invoke`, para log o progreso desde el hilo de la API.
- **El DLL queda bloqueado con Revit abierto** → compilar con Revit cerrado. Y regenerar un `.rfa`
  en disco **no** actualiza la familia ya cargada en un proyecto.

## Roturas de API que impiden compilar contra 2026

El código de referencia que hay por internet apunta mayormente a Revit 2015-2020. Vale como
**referencia conceptual, no para copiar y pegar**:

| Antiguo | Actual | Desde |
|---|---|---|
| `NewFloor(...)` | `Floor.Create(...)` | 2022 |
| `ElementId.IntegerValue` | `ElementId.Value` | 2024 |
| `DisplayUnitType` / `UnitType` | `ForgeTypeId` / `SpecTypeId` | 2021-22 |
| `NewAlignment` con retorno | devuelve `void` | 2026 |
| .NET Framework 4.8 | .NET 8 | 2025 |

## Colisiones de tipos — cualificar o aliasar, no adivinar

Con `UseWindowsForms=true` y `Autodesk.Revit.DB`/`UI` en el mismo fichero:

| Símbolo | Choca con | Arreglo |
|---|---|---|
| `MessageBox` | `System.Windows.Forms` | `System.Windows.MessageBox.Show(...)` |
| `Path` | `System.Windows.Shapes.Path` vs `System.IO.Path` | no importar `Shapes`; cualificar las formas |
| `ComboBox`, `TextBox` | `System.Windows.Controls` vs `Autodesk.Revit.UI` | alias o cualificar |
| `Point` | `System.Windows.Point` vs `DB.Point` | cualificar en el sitio |
| `Color` | `Media` vs `DB` | cualificar |
| `Visibility` | la propiedad de instancia `UIElement.Visibility` | cualificar el **enum** completo |

## Verificar sin Revit

La geometría y todo lo que sea matemática pura (offsets, distancias, secuencias) **se verifica en
una consola .NET**, no en Revit. Evita el ciclo compilar-abrir-Revit-probar, que es lento y donde
el error no da información. Compilar en **Debug y Release** es el mínimo para cerrar una tarea;
"compila" no es "funciona", y solo el usuario confirma el nivel 3 (Revit vivo).

## Al cerrar sesión

Si apareció un patrón nuevo, una rotura de API o un error no obvio con su solución, **actualizar
`~/.claude/revit_knowledge/revit_api_knowledge.md`**. Es lo que hace que el siguiente proyecto
empiece más arriba.
