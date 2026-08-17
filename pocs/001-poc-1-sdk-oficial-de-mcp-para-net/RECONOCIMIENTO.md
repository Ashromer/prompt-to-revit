# Reconocimiento — SDK oficial de MCP para .NET

PoC: `001-poc-1-sdk-oficial-de-mcp-para-net` · Lote 1, tarea 1 (Reconocimiento)
Requirements: `specs/001-poc-1-sdk-oficial-de-mcp-para-net/requirements.md`
Fecha de consulta de todas las fuentes: **2026-08-17**

> Alcance de este documento: **solo hechos verificados**. No se decide aquí el peldaño de la
> escalera de 3 peldaños del plan — eso es la segunda tarea del lote. No se ha escrito código.

---

## 1. Qué se buscó

La hipótesis de partida (FR-006 y Assumptions de `requirements.md`) era, textualmente, **una
hipótesis sin verificar**: que existe un SDK oficial de MCP para .NET publicado en NuGet como
`ModelContextProtocol`, procedente del repositorio `modelcontextprotocol/csharp-sdk`.

Se buscó confirmar o refutar, con evidencia primaria:

1. Existencia del paquete y su identificador **exacto** en NuGet.
2. Versión más reciente publicada.
3. Si esa versión es **estable** o preview/rc/beta.
4. Fecha del último release.
5. Actividad del repositorio: último commit, número de releases, si está archivado.
6. Si es **oficial** (organización `modelcontextprotocol`) o de terceros.
7. Si la documentación acredita transporte **stdio** y **declaración de herramientas con
   esquema tipado**.

Se priorizaron fuentes primarias (nuget.org, API de NuGet, API de GitHub, documentación oficial
del SDK) sobre resultados de blogs, que se descartaron como evidencia.

---

## 2. Qué se encontró — veredicto de existencia

**El SDK oficial de MCP para .NET EXISTE, es oficial y su versión más reciente es ESTABLE.**

La hipótesis de FR-006 queda **confirmada** en sus tres partes: existe el repositorio
`modelcontextprotocol/csharp-sdk`, existe el paquete NuGet cuyo identificador exacto es
`ModelContextProtocol`, y la organización propietaria es `modelcontextprotocol`.

No procede, por tanto, el cierre en negativo del PoC ni el salto al Lote 4 previsto para el caso
de que no existiera ningún SDK oficial.

---

## 3. Evidencia

### 3.1 Paquete NuGet

- **https://www.nuget.org/packages/ModelContextProtocol/** (consultado 2026-08-17)
  - Identificador exacto: `ModelContextProtocol`
  - Última versión listada: **2.2.0**, marcada como estable (sin sufijo de prerelease)
  - Fecha de publicación de 2.2.0: **13/08/2026**
  - Propietario declarado: `ModelContextProtocol` (a Series of LF Projects, LLC)
  - Descargas totales acumuladas: ~24,2 millones
  - Licencia según la ficha de NuGet: Apache-2.0
  - Repositorio declarado: `https://github.com/modelcontextprotocol/csharp-sdk`
  - Compatibilidad de frameworks declarada: .NET 8.0, .NET Standard 2.0 y superiores
    (incluye .NET 9.0 y 10.0) → **cubre el `net8.0` del PoC**
  - Historial reciente visible: 2.2.0 (13/08/2026), 2.1.0 (05/08/2026), 2.0.0 (28/07/2026),
    1.4.1 (09/07/2026), 1.4.0 (04/06/2026)

- **https://api.nuget.org/v3-flatcontainer/modelcontextprotocol/index.json** (consultado 2026-08-17)
  - Total de versiones publicadas: **53**
  - Versión más antigua: `0.1.0-preview.1.25171.12`
  - Versiones **estables** (sin `-preview` / `-rc`): `1.0.0`, `1.1.0`, `1.2.0`, `1.3.0`, `1.4.0`,
    `1.4.1`, `2.0.0`, `2.1.0`, `2.2.0` → **9 releases estables**
  - Las 8 versiones más recientes del array son: `2.0.0-preview.1`, `2.0.0-preview.2`,
    `2.0.0-preview.3`, `2.0.0-rc.1`, `2.0.0-rc.2`, `2.0.0`, `2.1.0`, `2.2.0`. Las tres últimas
    no llevan sufijo de prerelease.

- **https://api.nuget.org/v3-flatcontainer/modelcontextprotocol.core/index.json** (consultado 2026-08-17)
  - El paquete hermano `ModelContextProtocol.Core` también llega a **2.2.0** estable.
  - Nota: unos snippets de buscador daban `ModelContextProtocol.Core 1.2.0` como última versión;
    la consulta directa al índice de NuGet lo **refuta** (datos de buscador cacheados/obsoletos).
    Por eso no se ha usado ningún dato de versión procedente de snippets.

### 3.2 Repositorio GitHub

- **https://api.github.com/repos/modelcontextprotocol/csharp-sdk** (consultado 2026-08-17)
  - `full_name`: `modelcontextprotocol/csharp-sdk`
  - `archived`: **false** · `disabled`: **false** · `fork`: **false**
  - `owner login`: `modelcontextprotocol` → **es la organización oficial**, no un tercero
  - `description`: "The official C# SDK for Model Context Protocol servers and clients.
    Maintained in collaboration with Microsoft."
  - `stargazers_count`: 4479 · `open_issues_count`: 161
  - `created_at`: 2025-03-10T18:17:41Z
  - `pushed_at`: **2026-08-17T06:40:25Z** → último push **el mismo día de la consulta**
  - `updated_at`: 2026-08-17T08:36:09Z
  - `license.spdx_id`: `NOASSERTION`

- **https://api.github.com/repos/modelcontextprotocol/csharp-sdk/releases** (consultado 2026-08-17)

  | Tag | prerelease | draft | published_at |
  |---|---|---|---|
  | v2.2.0 | false | false | 2026-08-13T08:45:54Z |
  | v2.1.0 | false | false | 2026-08-05T03:49:08Z |
  | v2.0.0 | false | false | 2026-07-28T21:27:41Z |
  | v2.0.0-rc.2 | true | false | 2026-07-28T03:31:54Z |

- **https://api.github.com/repos/modelcontextprotocol/csharp-sdk/releases/tags/v1.0.0** (consultado 2026-08-17)
  - `tag_name`: `v1.0.0` · `prerelease`: **false** · `published_at`: **2026-02-25T01:21:19Z**
  - Cuerpo: "This is the first stable release of the ModelContextProtocol C# SDK."
  - Dato de madurez relevante: **el SDK dejó de ser preview hace ~6 meses**, no días.

- **https://github.com/modelcontextprotocol/csharp-sdk/releases** (consultado 2026-08-17)
  - Página HTML con al menos 10 releases visibles y paginación; la última (v2.2.0) marcada como
    estable. La serie 2.0.x se describe como soporte de la especificación MCP `2026-07-28`, y la
    1.4.x como la generación estable anterior.

### 3.3 Documentación oficial: stdio

- **https://csharp.sdk.modelcontextprotocol.io/v1/concepts/getting-started.html** (consultado 2026-08-17)
  - El ejemplo mínimo de servidor de la guía oficial es, textualmente:

    ```csharp
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();
    ```
  - Instrucción de instalación: `dotnet add package ModelContextProtocol`.
  - La guía indica que el paquete `ModelContextProtocol` es el adecuado "when you're building a
    client or a **stdio-based server** and want hosting, dependency injection, and
    attribute-based tool/prompt/resource discovery".
  - → **stdio soportado y documentado como caso de uso principal del paquete `ModelContextProtocol`.**

### 3.4 Documentación oficial: esquema tipado

- **https://csharp.sdk.modelcontextprotocol.io/v1/concepts/tools/tools.html** (consultado 2026-08-17)
  - "Tools can be defined in several ways: Using the `McpServerToolAttribute` attribute on
    methods within a class marked with `McpServerToolTypeAttribute`".
  - "Tool parameters are described using **JSON Schema 2020-12**. JSON schemas are
    **automatically generated from .NET method signatures** when the `[McpServerTool]` attribute
    is applied. Parameter types are mapped to JSON Schema types."
  - Mapeo documentado: string → `string`, entero → `integer`, float/double → `number`,
    bool → `boolean`, tipos complejos → `object` con `properties`.
  - Los `[Description]` alimentan el campo `description` del esquema generado.
  - Ejemplo oficial:

    ```csharp
    [McpServerToolType]
    public class MyTools
    {
        [McpServerTool, Description("Echoes the input message back")]
        public static string Echo([Description("The message to echo")] string message)
            => $"Echo: {message}";
    }
    ```
  - → **declaración de herramientas con esquema tipado derivado de tipos C#: soportada y
    documentada**, exactamente en la forma que anticipaba el plan (`[McpServerTool]`).

- **https://github.com/modelcontextprotocol/csharp-sdk** — el README lista los paquetes de la
  familia: `ModelContextProtocol.Core` (cliente / API de servidor de bajo nivel, dependencias
  mínimas), `ModelContextProtocol` (paquete principal, con hosting e inyección de dependencias),
  `ModelContextProtocol.AspNetCore` (servidores MCP sobre HTTP), y las extensiones
  `ModelContextProtocol.Extensions.Apps` y `ModelContextProtocol.Extensions.Tasks`.

---

## 4. Tabla resumen de los 7 puntos

| # | Punto a verificar | Hallazgo | Estado | Fuente |
|---|---|---|---|---|
| 1 | Existencia e identificador exacto en NuGet | Existe. ID exacto: **`ModelContextProtocol`** (familia: `.Core`, `.AspNetCore`, `.Extensions.Apps`, `.Extensions.Tasks`) | **Verificado** | nuget.org/packages/ModelContextProtocol |
| 2 | Versión más reciente | **2.2.0** | **Verificado** | nuget.org + API flatcontainer + API GitHub releases |
| 3 | Estable o preview | **Estable** (sin sufijo; `prerelease: false` en GitHub). 9 releases estables desde `1.0.0`. La 2.0.0 tuvo previews/rc, pero la línea actual es estable | **Verificado** | api.nuget.org flatcontainer + api.github.com |
| 4 | Fecha del último release | **2026-08-13** (NuGet 13/08/2026; GitHub `2026-08-13T08:45:54Z`) | **Verificado** | nuget.org + api.github.com |
| 5 | Actividad del repositorio | **No archivado** (`archived: false`). Último push **2026-08-17T06:40:25Z** (día de la consulta). 4479 estrellas, 161 issues abiertas. Creado 2025-03-10. 53 versiones en NuGet, 9 estables; ≥10 releases en GitHub (total exacto no determinado, ver §5) | **Verificado** (salvo el recuento total de releases) | api.github.com/repos/... |
| 6 | Oficial o de terceros | **Oficial**: organización propietaria `modelcontextprotocol`; descripción del repo: "The official C# SDK ... Maintained in collaboration with Microsoft"; propietario en NuGet: `ModelContextProtocol (a Series of LF Projects, LLC)` | **Verificado** | api.github.com + nuget.org |
| 7a | Soporte de transporte **stdio** | **Sí**, documentado: `WithStdioServerTransport()` en el ejemplo mínimo oficial; el paquete `ModelContextProtocol` se recomienda explícitamente para "stdio-based server" | **Verificado** | csharp.sdk.modelcontextprotocol.io/v1/concepts/getting-started.html |
| 7b | Declaración de herramientas con **esquema tipado** | **Sí**, documentado: `[McpServerToolType]` + `[McpServerTool]` + `[Description]`; JSON Schema **2020-12 generado automáticamente desde la firma del método .NET** | **Verificado** | csharp.sdk.modelcontextprotocol.io/v1/concepts/tools/tools.html |

Dato adicional relevante para el PoC (no pedido, pero condiciona el arranque): la ficha de NuGet
declara compatibilidad con **.NET 8.0** y .NET Standard 2.0+, así que el target `net8.0` del
entorno del proyecto está cubierto.

---

## 5. Limitaciones, discrepancias y lo que NO se ha verificado

Honestidad sobre la calidad de la evidencia:

1. **Recuento total de releases en GitHub: no determinado con fiabilidad.** La consulta a
   `api.github.com/.../releases?per_page=100` devolvió solo 5 objetos, incoherente con los ≥10
   visibles en la página HTML de releases; lo más probable es truncado de la respuesta al
   procesarla (los cuerpos de release son largos). Dato firme equivalente y suficiente: **53
   versiones publicadas en NuGet, 9 de ellas estables**.
2. **Discrepancia de años en una lectura de la página HTML de releases.** Una lectura de
   `github.com/.../releases` fechó v2.2.0 en "August 13, 2024". Se contrastó contra la API de
   GitHub, que devuelve `2026-08-13T08:45:54Z`, coherente con NuGet (13/08/2026). **Se toma 2026
   como correcto**; el "2024" fue un error de lectura, no un dato de la fuente.
3. **Discrepancia menor de licencia.** NuGet declara Apache-2.0; la API de GitHub devuelve
   `license.spdx_id: NOASSERTION` (fichero de licencia no autodetectado). No es material para el
   PoC; si la licencia acaba importando para distribución, hay que leer el `LICENSE` del repo.
4. **Fecha del último commit**: se usa `pushed_at` de la API (2026-08-17T06:40:25Z) como proxy.
   No se ha consultado el SHA ni el autor del último commit; no aportaba nada al veredicto.
5. **`csharp.sdk.modelcontextprotocol.io/concepts/...` redirige a `/v1/concepts/...`.** Las URLs
   sin `/v1/` devolvieron solo el aviso de redirección. Las citas de §3.3 y §3.4 provienen de las
   URLs `/v1/` ya resueltas. **Consecuencia a tener en cuenta: la documentación citada está bajo
   el prefijo `/v1/`, mientras el paquete va por la línea 2.x.** No se ha verificado si existe un
   set de documentación específico para 2.x ni si `WithStdioServerTransport()` y los atributos
   cambian de forma en 2.x. Es el punto flojo de este reconocimiento y conviene cerrarlo con la
   compilación real del PoC.
6. **Blogs y snippets de buscador descartados** como evidencia: coincidían con la documentación
   oficial, pero al menos un snippet daba versiones obsoletas (§3.1). Nada de la tabla resumen
   depende de una fuente secundaria.
7. **No verificado ni verificable por un agente**: que Claude Code registre y hable con el
   servidor (SC-001, SC-002, SC-003). Nada de este documento sustituye esa confirmación del
   usuario. Tampoco se ha compilado ni ejecutado nada.
8. **No verificado**: compatibilidad concreta entre la versión de la especificación MCP que
   implementa la serie 2.0.x (`2026-07-28`, según las notas de release) y la que habla el Claude
   Code instalado en la máquina del usuario. Es un riesgo real de versión de protocolo y aparecerá
   en la fase de prueba, no aquí.

---

## 6. Consecuencia procedimental

- La condición de cierre en negativo ("si NO existe ningún SDK oficial de MCP para .NET") **no se
  cumple**. El PoC **no** se cierra aquí y **no** se salta al Lote 4.
- La elección de peldaño de la escalera del plan queda **abierta deliberadamente**: es la segunda
  tarea del Lote 1. Este documento solo aporta los hechos sobre los que decidirla.
