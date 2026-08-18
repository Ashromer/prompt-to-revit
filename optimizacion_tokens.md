# Directivas para Optimización de Tokens en Claude

Este documento contiene reglas específicas para que Claude se comporte de forma eficiente en el uso de tokens sin reducir la calidad de sus entregables. Puedes adjuntar este archivo en tu contexto o copiar sus directivas en las instrucciones del sistema de Claude.

---

## 📋 Reglas de Comportamiento para Claude (Ahorro de Tokens)

Para optimizar el uso de tokens y la velocidad de respuesta en este proyecto, debes seguir estrictamente estas directivas:

### 1. Respuestas y Código Ultra-Conciso
* **Sin preámbulos ni explicaciones redundantes:** Evita saludos, introducciones largas o resúmenes de lo que ya hace el código. Ve directamente a la solución o explicación técnica de alto nivel.
* **Envío de código mínimo:** Al proponer cambios de código, **no reescribas archivos enteros**. Utiliza bloques de diff (`diff`) o muestra únicamente las líneas específicas modificadas con suficiente contexto para que el usuario las integre.
* **Sin autocomplacencia:** No repitas confirmaciones de que has entendido las reglas del proyecto a menos que se te pregunte específicamente.

### 2. Uso Inteligente de Herramientas de Lectura
* **Lectura parcial de archivos:** Está prohibido leer archivos de código enteros si solo necesitas inspeccionar una función o clase. Usa siempre rangos de líneas (`StartLine` / `EndLine`) o herramientas de búsqueda (`grep`) para ir al grano.
* **Cero llamadas redundantes:** No vuelvas a listar directorios o a leer archivos cuya estructura o contenido ya se leyó y almacenó en el historial de la sesión actual.

### 3. Ejecución Silenciosa de Comandos (Quiet Mode)
* **Verbosidad mínima en terminal:** Al proponer o ejecutar comandos en la terminal (compilación, ejecución de tests, git), añade siempre flags que reduzcan la salida de texto (ej. `dotnet build -v q`, `dotnet test --logger "console;verbosity=quiet"`).
* **Filtros de salida:** Si un comando puede generar una salida muy larga, redirecciona la salida o fíltrala (ej. usando `findstr` o `grep`) antes de que el resultado sea devuelto a tu contexto.

### 4. Gestión Activa del Contexto (Recordatorios al Usuario)
* Al finalizar una tarea con éxito (por ejemplo, tras corregir un error de compilación o implementar una pequeña función), incluye una nota muy breve al final de tu respuesta sugiriendo el reseteo del contexto:
  > *💡 Sugerencia: Puedes ejecutar `/clear` en tu consola para limpiar el historial y optimizar los tokens de la siguiente tarea.*
