# Reconocimiento — Paquete NuGet de metadatos de la API de Revit 2026

PoC: `002-poc-2-paquete-nuget-metadatos-api-revit` · Lote 1, tarea 1 (Reconocimiento)
Requirements: `specs/002-poc-2-paquete-nuget-metadatos-api-revit/requirements.md` (FR-001)
Fecha de consulta de todas las fuentes: **2026-08-17**

> Alcance de este documento: **solo hechos verificados**. Aquí no se elige el paquete ni se cierra
> ADR-008 — eso es una decisión posterior, tomada sobre estos hechos. No se ha escrito código, no se
> ha compilado nada y no se ha abierto ningún `.nupkg`.

---

## 1. Qué se buscó

FR-001 exige identificar **un paquete NuGet de solo metadatos que cubra la API de Revit 2026,
exponiendo como mínimo `RevitAPI` y `RevitAPIUI`**, sin asumir de antemano cuál es.

Se rastrearon candidatos reales contra la API de búsqueda de NuGet y, para cada uno, se buscó
evidencia primaria de:

1. Identificador **exacto** en nuget.org.
2. Publicador (autor y *owner* declarados) y si es **oficial de Autodesk o un tercero**.
3. Versión más reciente que **cubre Revit 2026** y su fecha de publicación.
4. Última versión publicada de cualquier línea (señal de si el proyecto sigue vivo).
5. Actividad del repositorio, si es público.
6. Si expone **`RevitAPI` y `RevitAPIUI`** como mínimo.
7. Si declara explícitamente ser **"solo metadatos"** (referencia que no redistribuye la DLL real).

Fuentes usadas, todas primarias: `azuresearch-usnc.nuget.org/query` (índice de búsqueda de NuGet),
`api.nuget.org/v3-flatcontainer/...` (lista de versiones), `api.nuget.org/v3-flatcontainer/.../*.nuspec`
(manifiesto real del paquete), `api.nuget.org/v3/registration5-semver1/...` (fechas de publicación y
estado *listed*), las fichas HTML de `nuget.org` (tamaño del paquete, TFM incluidos), y
`api.github.com` + `raw.githubusercontent.com` (actividad y ficheros de los repos). Ningún dato de la
tabla resumen procede de un blog ni de un snippet de buscador.

---

## 2. Veredicto de existencia — y el matiz que lo condiciona

**Sí existen paquetes NuGet que cubren la API de Revit 2026 completa exponiendo `RevitAPI` y
`RevitAPIUI`. Hay al menos cinco candidatos vivos. Ninguno es de Autodesk.**

Pero el hallazgo que de verdad condiciona el PoC es este:

> **"Solo metadatos" tiene dos lecturas distintas, y ningún candidato cumple las dos a la vez.**
>
> - **(a) Solo metadatos por *empaquetado*** — el ensamblado va en la carpeta `ref/` del `.nupkg`,
>   sirve únicamente para compilar y **nunca se copia a la salida** ni se redistribuye con el addin.
> - **(b) Solo metadatos por *contenido binario*** — el ensamblado está *desnudado*: solo firmas
>   públicas, sin implementación (estilo Refasmer). No es la DLL de Autodesk.
>
> El candidato con más tracción del ecosistema (**Nice3point**) cumple **(a) pero no (b)**: publica
> la **DLL original e íntegra** de la imagen de instalación de Revit (35,1 MB para `RevitAPI.dll` de
> 2026, §3.1.4), colocada en `ref/`. El único candidato que cumple **(b)** (**ricaun**) tiene el
> repositorio caído y una API recortada (§3.4).

Esto importa porque el *Context* de ADR-008 dice literalmente «`RevitAPI.dll` no es redistribuible».
Los paquetes que hacen viable el CI funcionan precisamente **porque un tercero la redistribuye**. Ese
riesgo no es del proyecto por sí solo, pero debe quedar escrito y no descubierto a posteriori.

**No existe paquete oficial de Autodesk con la API de Revit.** Verificado, no supuesto: §3.6.

---

## 3. Evidencia por candidato

Barrido inicial: `https://azuresearch-usnc.nuget.org/query?q=RevitAPI&take=50&prerelease=false`
(consultado 2026-08-17) devolvió 16 paquetes. Los relevantes se detallan abajo; los descartados,
en §5.

### 3.1 `Nice3point.Revit.Api.RevitAPI` + `Nice3point.Revit.Api.RevitAPIUI` — candidato principal

#### 3.1.1 Identidad y publicador

- Fichas: `https://www.nuget.org/packages/Nice3point.Revit.Api.RevitAPI/2026.4.10` y
  `https://www.nuget.org/packages/Nice3point.Revit.Api.RevitAPIUI/2026.4.10` (consultadas 2026-08-17)
- Identificadores exactos: **`Nice3point.Revit.Api.RevitAPI`** y **`Nice3point.Revit.Api.RevitAPIUI`**
  (paquetes separados, uno por ensamblado)
- Autor y *owner*: **`Nice3point`** — **tercero no oficial**, persona individual. La ficha muestra
  **prefijo reservado / owner verificado** en nuget.org (badge de *verified prefix*).
- Descargas totales: `...RevitAPI` **631.754**; `...RevitAPIUI` **619.892**
- Descargas de la versión 2026: `...RevitAPI` **53.367**; `...RevitAPIUI` **51.805**
- Familia completa publicada por el mismo repo (10 paquetes): `RevitAPI`, `RevitAPIUI`, `RevitAPIIFC`,
  `RevitNET`, `AdWindows`, `UIFramework`, `UIFrameworkServices`, `RevitAddInUtility`, `RevitAPIMacros`,
  `PackageContentsParser` (README del repo, §3.1.3)

#### 3.1.2 Versión que cubre 2026, y estado de las versiones

- `https://api.nuget.org/v3-flatcontainer/nice3point.revit.api.revitapi/index.json` (2026-08-17):
  **54 versiones**, de `2014.0.0` a `2027.2.0`. Línea 2026: `2026.0.0-preview.1.250108`,
  `2026.0.0-preview.2.250306`, `2026.0.4`, `2026.1.0`, `2026.2.0`, `2026.3.0`, `2026.4.0`, **`2026.4.10`**.
- `https://api.nuget.org/v3-flatcontainer/nice3point.revit.api.revitapiui/index.json`: la misma
  lista de versiones para la línea 2026.
- `https://api.nuget.org/v3/registration5-semver1/nice3point.revit.api.revitapi/index.json` y
  `.../nice3point.revit.api.revitapiui/index.json` (2026-08-17):

  | Versión | `published` | `listed` |
  |---|---|---|
  | 2026.0.4 | 1900-01-01 (convención NuGet de *unlisted*) | **false** |
  | 2026.1.0 | 1900-01-01 | **false** |
  | 2026.2.0 | 1900-01-01 | **false** |
  | 2026.3.0 | 1900-01-01 | **false** |
  | 2026.4.0 | 1900-01-01 | **false** |
  | **2026.4.10** | **2026-05-12T17:17:53Z** (`RevitAPI`) / `2026-05-12T17:17:55Z` (`RevitAPIUI`) | **true** |
  | 2027.1.0 | 2026-06-24T14:01:43Z | true |
  | 2027.2.0 | 2026-07-25T20:17:55Z | true |

  → **Versión más reciente que cubre Revit 2026: `2026.4.10`, publicada 2026-05-12.** El mantenedor
  deslista las versiones antiguas de cada línea anual y deja **solo la última listada**.
- Última versión de cualquier línea: **`2027.2.0` (2026-07-25)**.

#### 3.1.3 Actividad del repositorio

- `https://api.github.com/repos/Nice3point/RevitApi` (2026-08-17):
  - `full_name`: `Nice3point/RevitApi` · `owner.login`: `Nice3point`
  - `description`: "Libraries for Revit plugin development"
  - `archived`: **false** · `disabled`: **false** · `fork`: **false**
  - `stargazers_count`: **89** · `open_issues_count`: **1**
  - `created_at`: **2022-01-31T21:07:42Z** → ~4,5 años de historia
  - `pushed_at`: **2026-08-13T07:17:45Z** → último push **4 días antes** de la consulta
  - `license`: **MIT** · `topics`: `dependencies`, `revit-api`
- `https://api.github.com/repos/Nice3point/RevitApi/commits?per_page=5` (2026-08-17):
  `d533110` renovate-bot 2026-08-13 "Update dependency Polyfill to 11.2.0" · `9867c69` Nice3point
  2026-08-11 "Add .editorconfig" · `9fbe7ea` Nice3point 2026-08-10 "Build 25.4.60.9" · `3f02ac0`
  Nice3point 2026-08-10 "Build 24.3.60.12" · `62257ed` Nice3point 2026-08-10 "[SDK] Pin 100 Feature Band"
- `https://api.github.com/repos/Nice3point/RevitApi/releases?per_page=15` (2026-08-17): releases
  `2025.4.60` (2026-08-10), `2024.3.60` (2026-08-10), `2027.2.0` (2026-07-25), `2027.1.0` (2026-06-24),
  `2027.0.20` (2026-05-12), `2026.4.10` (2026-05-12) — todas `prerelease: false`, `draft: false`.
  → **Dato de mantenimiento relevante: en agosto de 2026 sigue publicando parches de las líneas 2024
  y 2025**, no solo de la última. No abandona las líneas antiguas al saltar de año.
- Consumo del ecosistema (ficha de nuget.org de `...RevitAPIUI`): **19 paquetes NuGet y 9 repos de
  GitHub** declaran dependencia, entre ellos `RevitLookup` (1,4 k estrellas) y `RevitAddInManager`
  (488 estrellas).

#### 3.1.4 Qué contiene realmente el paquete — evidencia directa

Esta es la parte que decide la lectura de "solo metadatos". Tres fuentes primarias:

- **README del repo** (`https://raw.githubusercontent.com/Nice3point/RevitApi/main/README.md`,
  2026-08-17), textualmente: **«Only original files from the latest Revit installation image are used.»**
- **Ficheros versionados en el repo**
  (`https://api.github.com/repos/Nice3point/RevitApi/contents/Nice3point.Revit.Api/Content/2026.4.10`,
  2026-08-17) — carpeta de la versión 2026.4.10:

  | Fichero | Tamaño (bytes) |
  |---|---|
  | `RevitAPI.dll` | **35.120.472** |
  | `RevitAPI.xml` | 12.509.342 |
  | `RevitAPIUI.dll` | **2.851.672** |
  | `RevitAPIUI.xml` | 1.061.282 |
  | `AdWindows.dll` | 2.361.696 |
  | `RevitAPIIFC.dll` / `.xml` | 421.720 / 2.365.636 |
  | `RevitAPIMacros.dll` / `.xml` | 108.376 / 342.614 |
  | `RevitAddInUtility.dll` / `.xml` | 61.784 / 70.404 |
  | `RevitNET.dll` | 208.216 |
  | `UIFramework.dll` | 1.131.352 |
  | `UIFrameworkServices.dll` | 666.968 |
  | `PackageContentsParser.dll` | 21.280 |

  → 35 MB de `RevitAPI.dll` es **la DLL completa de Autodesk**, no un ensamblado de referencia
  desnudado. Y los `.xml` de documentación van incluidos (IntelliSense con los comentarios de la API).
  **Existen `RevitAPI` y `RevitAPIUI` para 2026: confirmado por fichero, no por descripción.**

- **`.csproj` de empaquetado**
  (`https://raw.githubusercontent.com/Nice3point/RevitApi/main/Nice3point.Revit.Api/Nice3point.Revit.Api.csproj`,
  2026-08-17), verbatim en lo relevante:

  ```xml
  <RevitFramework Condition="'$(RevitFramework)' == ''">net10.0</RevitFramework>
  <TargetFramework>$(RevitFramework)</TargetFramework>
  <IncludeBuildOutput>false</IncludeBuildOutput>
  <NoWarn>$(NoWarn);NU5131</NoWarn>
  ...
  <Content Include="Content\$(Version)\$(LibraryName).*" PackagePath="ref\$(TargetFramework)\"/>
  ```

  → El ensamblado se empaqueta en **`ref\<TFM>\`**, no en `lib\`. Una carpeta `ref/` sin `lib/` es,
  por definición de NuGet, **referencia de solo compilación: no aporta activo de runtime y no se copia
  a la carpeta de salida**. La supresión de `NU5131` (aviso "reference sin lib") es coherente con eso.
  **Es "solo metadatos" en el sentido (a) del §2: comportamiento de compilación, no contenido binario.**

- **Manifiesto publicado**
  (`https://api.nuget.org/v3-flatcontainer/nice3point.revit.api.revitapi/2026.4.10/nice3point.revit.api.revitapi.nuspec`,
  2026-08-17), verbatim:

  ```xml
  <id>Nice3point.Revit.Api.RevitAPI</id>
  <version>2026.4.10</version>
  <authors>Nice3point</authors>
  <license type="file">License.md</license>
  <projectUrl>https://github.com/Nice3point/RevitApi</projectUrl>
  <description>Library for Revit plugin development</description>
  <repository type="git" url="https://github.com/Nice3point/RevitApi"
              commit="a6ccafba5df2d929298199f43d6c081b2cd64f0d" />
  <dependencies>
    <group targetFramework="net8.0-windows7.0" />
  </dependencies>
  ```

  → **TFM del paquete 2026: `net8.0-windows7.0`**, que es exactamente el target del proyecto
  (`net8.0-windows`, §Entorno de `CLAUDE.md`). **Sin dependencias transitivas.**
  Tamaño del `.nupkg`: **5,85 MB** (`...RevitAPI` 2026.4.10) y **802,55 KB** (`...RevitAPIUI` 2026.4.10),
  coherente con comprimir 35,1 MB y 2,85 MB respectivamente.

- **Licencia**: `https://raw.githubusercontent.com/Nice3point/RevitApi/main/LICENSE.md` (2026-08-17)
  → **MIT, copyright 2022 Nice3point**. **No menciona en ningún punto los ensamblados de Autodesk ni
  su redistribución.** Es decir: la licencia MIT cubre el trabajo de empaquetado del mantenedor, no
  los binarios de Autodesk que viajan dentro. Ver §6.

### 3.2 `Revit_All_Main_Versions_API_x64` — candidato serio

- Ficha: `https://www.nuget.org/packages/Revit_All_Main_Versions_API_x64/2026.0.0` (2026-08-17)
- Identificador exacto: **`Revit_All_Main_Versions_API_x64`**
- Autores / *owners*: **`Matthew.Taylor,WSP`** — **tercero no oficial** (WSP es una ingeniería, no
  Autodesk). `copyright` declarado en el nuspec: **`Autodesk`**.
- Descargas totales: **974.377** (el más descargado de todos los candidatos)
- `https://api.nuget.org/v3-flatcontainer/revit_all_main_versions_api_x64/index.json` (2026-08-17):
  **47 versiones**, de `0.0.1-alpha` / `2011.0.0` a `2027.0.2`. Línea 2026: **`2026.0.0-beta` y
  `2026.0.0` únicamente**. Línea 2027: `2027.0.0-beta`, `2027.0.0-beta2`, `2027.0.0`, `2027.0.2`.
- `https://api.nuget.org/v3/registration5-semver1/revit_all_main_versions_api_x64/index.json` (2026-08-17):
  `2026.0.0-beta` **2025-03-24**, **`2026.0.0` 2025-04-03T00:48:17Z**, `2027.0.0` **2026-04-13**,
  `2027.0.2` **2026-05-05T22:53:22Z**.
  → **Versión más reciente que cubre 2026: `2026.0.0`, publicada 2025-04-03.**
  **Consecuencia: la línea 2026 nunca recibió parche.** Frente a las 6 versiones de la línea 2026 de
  Nice3point (que siguen las actualizaciones 2026.1 … 2026.4 de Revit), aquí hay una sola, de la
  fecha de salida del producto. Si Revit 2026 cambió su API en una update, este paquete no lo refleja.
- **Contenido**: `summary` del nuspec de la 2026.0.0 es **«2026 only.»** y el de la 2027.0.2,
  **«2027 only»** → pese al nombre y a la `description` heredada («Revit 2011/…/2026 API x64»), **cada
  versión del paquete lleva solo los ensamblados de su año**. La `description` enumera
  **`RevitAPI.dll`, `RevitAPIUI.dll`, `AdWindows.dll`, `UIFramework.dll`** más `RevitAPI.xml` /
  `RevitAPIUI.xml`, y añade textualmente que las referencias van con **«'Copy Local' set to False»**.
  → **Expone `RevitAPI` y `RevitAPIUI`: sí.** «Copy Local = False» es de nuevo el sentido (a) de
  "solo metadatos". **No declara en ningún punto ser un ensamblado desnudado**; la `description` dice
  «Files from installation of … Revit (one-box) … 2026», o sea: DLL originales.
- Tamaño del `.nupkg` 2026.0.0: **7,6 MB**. TFM incluido en el paquete: **`net8.0`** (no
  `net8.0-windows`; consumible igualmente desde `net8.0-windows`). Sin dependencias.
- **Sin repositorio público**: el nuspec **no declara `projectUrl` ni `repository`**, y la ficha de
  nuget.org tampoco los muestra. **No hay ninguna señal de actividad verificable más allá del ritmo de
  publicación en NuGet** — no hay commits, ni issues, ni código de empaquetado auditable. Tampoco
  declara licencia (`licenseUrl` ausente).

### 3.3 `Speckle.Revit.API` — candidato serio, pero de propósito ajeno

- Ficha: `https://www.nuget.org/packages/Speckle.Revit.API` (2026-08-17)
- Identificador exacto: **`Speckle.Revit.API`** · *owner*: **`specklesystems`** — **tercero no
  oficial** (empresa, no Autodesk)
- `https://api.nuget.org/v3-flatcontainer/speckle.revit.api/index.json` (2026-08-17): **7 versiones**
  (`2022.0.2`, `2022.0.2.1`, `2023.0.0`, `2024.0.0`, `2025.0.0`, **`2026.0.0`**, `2027.0.0`)
- **Versión que cubre 2026: `2026.0.0`, publicada 2025-04-08** (10.860 descargas).
  Última de cualquier línea: `2027.0.0`, **2026-05-11** (474 descargas). Tamaño: 6,76 MB.
- Descripción: **«Includes RevitAPI.dll and RevitAPIUI.dll»** → **expone ambos: sí**. Descargas
  totales: 113.815. Sin dependencias.
- **No declara ser solo metadatos.** Una versión por año, sin parches intermedios (mismo patrón que
  §3.2). El paquete es una **dependencia interna de los conectores de Speckle**, no un producto
  pensado para terceros: si Speckle cambia de estrategia, deja de publicarse sin previo aviso.

### 3.4 `ricaun.RevitAPI.Fake.References` — el ÚNICO "solo metadatos" en sentido estricto

- Ficha: `https://www.nuget.org/packages/ricaun.RevitAPI.Fake.References` y
  `.../ricaun.RevitAPI.Fake.References.RevitAPI/2026.0.1` (2026-08-17)
- Identificadores exactos: **`ricaun.RevitAPI.Fake.References`** (metapaquete) →
  **`ricaun.RevitAPI.Fake.References.RevitAPI`** + **`ricaun.RevitAPI.Fake.References.RevitAPIUI`**
- Autor: **Luiz Henrique Cassettari** · *owner*: **`ricaun.io`** — **tercero no oficial**, individual
- Nuspec de `ricaun.RevitAPI.Fake.References` 2026.0.1
  (`https://api.nuget.org/v3-flatcontainer/ricaun.revitapi.fake.references/2026.0.1/ricaun.revitapi.fake.references.nuspec`,
  2026-08-17): `description` = **«RevitAPI and RevitAPIUI with only public class/interface/enum and
  without WPF/Drawing references.»** · `copyright` © 2025 ricaun · `tags` revit, revitapi, dotnet ·
  `projectUrl` y `repository` = `https://github.com/ricaun-io/RevitAPI.Fake`
  (commit `b07a632f09bc81d6c78ff0b4132fede249f29df3`) · TFM: **`net45`, `netstandard2.0`, `net6.0`**
  (no hay `net8.0-windows`) · depende de los dos subpaquetes en `>= 2026.0.1`.
- README (según la ficha de nuget.org): **«Each fake file was generated inside Revit by creating each
  file with the public methods and properties of the original API»** y **«The WPF and Drawing
  reference was removed and the methods and properties as well from the fake files.»**
  → **Es solo metadatos en el sentido (b): binario desnudado, no la DLL de Autodesk.** Lo confirma el
  tamaño: `ricaun.RevitAPI.Fake.References.RevitAPI` 2026.0.1 pesa **1,47 MB** repartido entre **tres**
  TFM, frente a los 35,1 MB de la DLL real.
- Versiones: `https://api.nuget.org/v3-flatcontainer/ricaun.revitapi.fake.references/index.json`
  (2026-08-17) → **42 versiones**, de `2019.0.0-rc.1` a **`2026.0.1`**. **No hay línea 2027.**
  Fecha de la 2026.0.1 según la ficha de nuget.org: **2025-08-05**. Descargas de la 2026.0.1: **715**;
  totales del metapaquete: 22.877.
- **Repositorio caído**: `https://api.github.com/repos/ricaun-io/RevitAPI.Fake` devuelve **404**, y
  `https://github.com/ricaun-io/RevitAPI.Fake` también **404**. La organización `ricaun-io` **sí
  existe y tiene 84 repos públicos** (`https://api.github.com/orgs/ricaun-io`, creada 2021-11-24), y
  una búsqueda `org:ricaun-io fake` devuelve `total_count: 0`. → **El repo declarado en el manifiesto
  del paquete ya no es público: no hay forma de auditar cómo se generan los ensamblados ni señal
  alguna de actividad.**
- **Limitación funcional documentada por el propio autor**: quitar WPF/Drawing implica quitar «los
  métodos y propiedades» que los usan. Buena parte de la superficie de `RevitAPIUI` para el ribbon es
  WPF (`PushButtonData.LargeImage` es `System.Windows.Media.ImageSource`). **No se ha verificado qué
  miembros concretos faltan**, pero es un riesgo directo contra el addin trivial de FR-002 y contra la
  ventana de aprobación del proyecto final.

### 3.5 `Chuongmep.Revit.Api.RevitAPI` — candidato marginal

- Ficha: `https://www.nuget.org/packages/Chuongmep.Revit.Api.RevitAPI` (2026-08-17)
- *Owner*: **`chuongmep`** — **tercero no oficial**, individual. Descripción idéntica a la de
  Nice3point («Library for Revit plugin development») → clon del mismo enfoque.
- **Versión que cubre 2026: `2026.0.0`, publicada 2025-05-03**, con **475 descargas**. Es también la
  última publicada: **no hay línea 2027**. Descargas totales: 18.066. Tamaño: 5,78 MB. TFM: `net8.0`.
- **Repositorio caído**: `https://api.github.com/repos/chuongmep/RevitAPI` devuelve **404**, pese a
  ser el `projectUrl`/`repository` declarado.
- **No declara ser solo metadatos.** Una sola versión para 2026, sin parches.

### 3.6 ¿Hay algo oficial de Autodesk? — verificado, y la respuesta es no

Esto se comprobó explícitamente para no dar por hecho que no existe:

- `https://azuresearch-usnc.nuget.org/query?q=owner%3Aautodesk&take=30&prerelease=true` (2026-08-17):
  **22 paquetes con `owners: Autodesk`** — `AutoCAD.NET`, `AutoCAD.NET.Core`, `AutoCAD.NET.Model`
  (v26.0.0), `Autodesk.Forge.*`, `ForgeUnits.*`, `Autodesk.ProductInterface.PowerMILL/PowerSHAPE`…
  → **Autodesk sí publica en nuget.org, e incluso publica la API de .NET de AutoCAD.**
- `https://azuresearch-usnc.nuget.org/query?q=owner%3Aautodesk+revit&take=30&prerelease=true`
  (2026-08-17): **`totalHits: 1`** → **`Autodesk.Forge.DesignAutomation.Revit`**, v`2027.0.0`,
  autores `Autodesk Forge`, owner `Autodesk`.
- Ese único paquete **no sirve**: nuspec 2027.0.0
  (`https://api.nuget.org/v3-flatcontainer/autodesk.forge.designautomation.revit/2027.0.0/autodesk.forge.designautomation.revit.nuspec`,
  2026-08-17) → `description` **«Design Automation Bridge for Revit»**, copyright «Autodesk Inc.»,
  licencia por fichero, sin dependencias, sin `<references>` ni `<frameworkAssemblies>`.
  **Tamaño del `.nupkg` de la versión 2026.0.0: 20,82 KB** (ficha de nuget.org,
  `https://www.nuget.org/packages/Autodesk.Forge.DesignAutomation.Revit/2026.0.0`, publicada
  **2025-03-31**). 20,82 KB **no puede contener** una `RevitAPI.dll` de 35 MB.
  → Es únicamente `DesignAutomationBridge`; **no expone `RevitAPI` ni `RevitAPIUI`**.
- Versiones: 27 en total (`2018.0.0-beta1` … `2026.0.0`, `2027.0.0`).
  `2027.0.0` publicada **2026-04-07**.

**Conclusión de §3.6: no hay paquete NuGet oficial de Autodesk que exponga la API de Revit. Cualquier
candidato viable es forzosamente de un tercero no oficial** (edge case de `requirements.md`, §6).

---

## 4. Tabla resumen — los 7 puntos por candidato

| Candidato (id exacto NuGet) | Publicador · ¿oficial? | Última versión que cubre **2026** | Fecha de esa versión | Última versión de cualquier línea | Repo público / actividad | ¿Expone `RevitAPI` + `RevitAPIUI`? | ¿"Solo metadatos" declarado? |
|---|---|---|---|---|---|---|---|
| **`Nice3point.Revit.Api.RevitAPI`** + **`.RevitAPIUI`** | `Nice3point` (individual, prefijo verificado) · **tercero** | **`2026.4.10`** | **2026-05-12** | `2027.2.0` (2026-07-25) | `Nice3point/RevitApi`, **no archivado**, `pushed_at` **2026-08-13**, 89 ★, 1 issue, MIT, creado 2022-01-31 | **Sí** (paquete por ensamblado; `RevitAPI.dll` 35,1 MB y `RevitAPIUI.dll` 2,85 MB versionados en el repo) | **(a) sí / (b) no** — empaqueta en `ref\net8.0-windows7.0\`, pero es la DLL original: *«Only original files from the latest Revit installation image are used»* |
| **`Revit_All_Main_Versions_API_x64`** | `Matthew.Taylor,WSP` · **tercero** | **`2026.0.0`** | **2025-04-03** | `2027.0.2` (2026-05-05) | **Ninguno declarado** — sin `projectUrl` ni `repository`; solo el ritmo de publicación en NuGet | **Sí** (`description`: RevitAPI.dll, RevitAPIUI.dll, AdWindows.dll, UIFramework.dll + XML) | **(a) parcial / (b) no** — declara *«'Copy Local' set to False»*, pero son *«files from installation»* |
| **`Speckle.Revit.API`** | `specklesystems` (empresa) · **tercero** | **`2026.0.0`** | **2025-04-08** | `2027.0.0` (2026-05-11) | Repo de Speckle no consultado; paquete auxiliar de sus conectores | **Sí** (*«Includes RevitAPI.dll and RevitAPIUI.dll»*) | **No** |
| **`ricaun.RevitAPI.Fake.References`** (+ `.RevitAPI`, `.RevitAPIUI`) | `ricaun.io` / L. H. Cassettari · **tercero** | **`2026.0.1`** | **2025-08-05** | `2026.0.1` — **no hay 2027** | `ricaun-io/RevitAPI.Fake` → **404, repo ya no público** (la org existe, 84 repos) | **Sí** (dos subpaquetes) | **(b) SÍ, el único** — *«only public class/interface/enum»*, *«WPF and Drawing reference was removed»*. TFM `net45`/`netstandard2.0`/`net6.0` |
| **`Chuongmep.Revit.Api.RevitAPI`** | `chuongmep` (individual) · **tercero** | **`2026.0.0`** | **2025-05-03** (475 descargas) | `2026.0.0` — **no hay 2027** | `chuongmep/RevitAPI` → **404** | Sí (por nombre de paquete) | **No** |
| `Autodesk.Forge.DesignAutomation.Revit` | **`Autodesk`** · **OFICIAL** | `2026.0.0` | 2025-03-31 | `2027.0.0` (2026-04-07) | Sin repo declarado | **NO** — 20,82 KB, solo `DesignAutomationBridge` | n/a |
| `Autodesk.Revit.SDK` | `Zhmayev Yaroslav` / `CodeCavePro`,`salaros` · **tercero pese al nombre** | `2026.0.0.9999` | **2025-04-14** | `2026.0.0.9999` — **no hay 2027** | `CodeCavePro/revit-sdk`, no archivado, `pushed_at` **2025-05-05**, 24 ★ | **No verificado** (30,97 MB; `description` habla de *code samples and documentation*) | **No** |
| `Revit.RevitApi.x64` / `Revit.RevitApiUI.x64` | `sanderobdeijn` · tercero | **ninguna** | — | `2023.0.0` | — | Sí, pero solo hasta 2023 | — |

---

## 5. Descartados y por qué

1. **`Revit.RevitApi.x64` / `Revit.RevitApiUI.x64`** (Sander Obdeijn) — **no llega a 2026**.
   `https://api.nuget.org/v3-flatcontainer/revit.revitapi.x64/index.json` (2026-08-17): 9 versiones,
   `2015.0.0` … **`2023.0.0`**, y ahí acaba. **Abandonado para el propósito de este PoC.**
2. **`Autodesk.Revit.SDK`** (CodeCavePro / salaros) — **el nombre engaña: no es de Autodesk**. Owner
   real: `salaros`, `CodeCavePro`. `projectUrl` apunta a la página de Autodesk, pero el `repository`
   real es `CodeCavePro/revit-sdk` (24 ★, `pushed_at` 2025-05-05, sin licencia declarada). Última
   versión **`2026.0.0.9999`, 2025-04-14**; **no hay 2027** → señal de no seguir el ciclo del producto.
   Además su `description` es la del **SDK** (ejemplos y documentación), no la de un paquete de
   referencias, y **no se ha podido verificar si incluye `RevitAPI.dll`/`RevitAPIUI.dll`**. Descartado
   por ambigüedad de contenido + una línea de versión de retraso, no por falta de 2026.
3. **`Autodesk.Forge.DesignAutomation.Revit`** — **único paquete oficial de Autodesk relacionado con
   Revit, y no expone la API** (20,82 KB, §3.6). Descartado por contenido, no por publicador.
4. Paquetes de la misma familia Nice3point que **no son ensamblados de la API** (`Nice3point.Revit.Toolkit`,
   `Nice3point.Revit.Extensions`) y `xml.Revit.Toolkit`: fuera de alcance de FR-001.

---

## 6. El edge case de `requirements.md`: publicador tercero no oficial

`requirements.md` pregunta explícitamente: *«¿Cómo se maneja el caso en que el paquete cubre la API
pero su publicador es un tercero no oficial y deja de mantenerse…?»*. Constancia explícita, tal como
pide FR-001, **sin que esto descarte a ningún candidato**:

- **Los siete candidatos que exponen la API son de terceros no oficiales. No hay alternativa oficial**
  (§3.6). Elegir "tercero" no es una concesión: es la única opción disponible.
- **Riesgo de mantenimiento, por candidato**: el más mitigado es Nice3point — repo público, MIT,
  push hace 4 días, parches de líneas antiguas en agosto de 2026, prefijo verificado en NuGet, 19
  paquetes y 9 repos dependientes. Los más expuestos son ricaun y Chuongmep: **repositorio declarado
  caído (404)**, sin línea 2027.
- **Riesgo de "bus factor"**: Nice3point, ricaun y Chuongmep son **personas individuales**.
  `Revit_All_Main_Versions_API_x64` está respaldado por una ingeniería (WSP) pero **no publica código
  ni repositorio**, así que nadie puede continuarlo si su autor para. Speckle es una empresa, pero el
  paquete es infraestructura interna suya.
- **Mitigación disponible para cualquiera de ellos y barata**: los `.nupkg` son inmutables en
  nuget.org y la versión se fija en el `.csproj`. Si el mantenedor desaparece, el paquete de Revit
  2026 ya publicado **sigue restaurándose indefinidamente**; lo que se pierde es la cobertura de
  versiones futuras de Revit — que es exactamente el momento en que ya haría falta reevaluar todo.
  Además, el fallback de FR-009 (referencia por ruta local) sigue siendo viable en cualquier momento:
  **la decisión es reversible con un cambio de `<Reference>`**.
- **Riesgo distinto y menos comentado: la licencia.** Nice3point publica bajo **MIT** un `.nupkg` que
  contiene la `RevitAPI.dll` **original de Autodesk**, y su `LICENSE.md` **no menciona a Autodesk**
  (§3.1.4). Lo mismo aplica a `Revit_All_Main_Versions_API_x64` (`copyright: Autodesk`, sin licencia
  declarada) y a `Speckle.Revit.API`. **Ninguno acredita permiso de Autodesk para redistribuir.** Esto
  contradice el *Context* de ADR-008 («`RevitAPI.dll` no es redistribuible») en un punto sensible:
  la solución al problema es que otro asuma el riesgo de redistribuir. **No es un bloqueo del PoC**
  — es práctica universal en el ecosistema de plugins de Revit — pero debe constar antes de que
  «distribución a terceros» sea un objetivo declarado del proyecto. `ricaun.RevitAPI.Fake.References`
  es el **único candidato que no tiene este problema**, porque genera un binario propio.

---

## 7. Limitaciones y lo que NO se ha verificado

Honestidad sobre la calidad de la evidencia, mismo criterio que el PoC #1:

1. **No se ha abierto ningún `.nupkg`.** Que el ensamblado del paquete de Nice3point 2026.4.10 esté
   en `ref/net8.0-windows7.0/` se deduce de **dos** fuentes primarias concordantes (el `.csproj` de
   empaquetado, que fija `PackagePath="ref\$(TargetFramework)\"`, y el nuspec publicado, que declara
   el grupo `net8.0-windows7.0`), más la ficha de nuget.org, que marca `net8.0-windows7.0` como TFM
   *incluido en el paquete*. **Es inferencia sólida, no inspección directa.** La comprobación
   definitiva es trivial y llega sola al compilar: si estuviera en `lib/`, la DLL de 35 MB aparecería
   en la carpeta de salida del addin.
2. **El `.csproj` de empaquetado leído es el de `main` (línea 2027, `RevitFramework` por defecto
   `net10.0`), no el commit exacto `a6ccafba…` con el que se publicó 2026.4.10.** El nuspec de esa
   versión confirma que el TFM efectivo fue `net8.0-windows7.0`, así que el mecanismo es el mismo,
   pero el fichero exacto de aquel commit no se ha leído.
3. **Contenido de `Autodesk.Revit.SDK` no determinado** (§5.2): 30,97 MB podrían ser ejemplos y
   documentación, o incluir las DLL. No se resolvió porque el candidato ya estaba descartado por otras
   razones. Si alguien lo reabre, hay que mirar dentro del paquete.
4. **Discrepancia menor en la API de registro de ricaun.** La lectura de
   `registration5-semver1/ricaun.revitapi.fake.references/index.json` reportó 23 versiones y no
   localizó `2026.0.1` como *listed* (solo `2026.0.1-rc` como *unlisted*), mientras el índice
   flatcontainer y la ficha HTML de nuget.org **sí** muestran `2026.0.1` publicada el 2025-08-05 con
   715 descargas. Muy probablemente truncado o mal resumido al procesar la respuesta. **Se toma como
   correcta la ficha + flatcontainer**: `2026.0.1` existe. Dato marcado como el punto flojo de §3.4.
5. **La `description` de `Revit_All_Main_Versions_API_x64` contradice su `summary`** («Revit
   2011/…/2026 API x64» vs «2026 only.»). Se toma el `summary` como correcto por ser específico de la
   versión, y porque el tamaño del `.nupkg` (7,6 MB) **es incompatible** con llevar 16 años de
   ensamblados. Es interpretación razonada, no cita literal de una fuente que lo aclare.
6. **No se ha verificado la superficie de API concreta que falta en los *fake references* de ricaun.**
   El autor dice que se quitaron «los métodos y propiedades» que usan WPF/Drawing; **qué miembros
   exactamente, no consta**. Si ese candidato llegara a considerarse, hay que compilar el addin
   trivial contra él antes de nada.
7. **No se ha verificado que ninguno de estos paquetes compile.** Nada en este documento sustituye
   SC-001. En particular, **compilar en esta máquina no probaría Historia 1**: aquí Revit 2026 está
   instalado (`C:\Program Files\Autodesk\Revit 2026\`), y solo un runner de GitHub Actions sin Revit
   puede demostrarlo.
8. **No verificado ni verificable por un agente**: SC-002 (que el DLL cargue en Revit 2026 vivo y el
   botón funcione igual que el compilado contra las DLL locales). Eso lo confirma el usuario por
   escrito, patrón `GUION-VERIFICACION.md` del PoC #1.
9. **No verificado**: si `2026.4.10` corresponde a una *update* concreta de Revit 2026 y si la máquina
   del usuario tiene esa misma update instalada. Un desajuste de *build* entre los metadatos y el
   runtime real es exactamente el segundo edge case de `requirements.md`, y aparecerá al cargar en
   Revit, no aquí.
10. **Blogs y snippets de buscador descartados como evidencia.** La única búsqueda web usada
    (sobre el contenido de `Autodesk.Forge.DesignAutomation.Revit`) sirvió solo para orientar; la
    conclusión de §3.6 se apoya en el nuspec y en el tamaño del paquete, ambos de la API de NuGet.

---

## 8. Consecuencia procedimental

- **FR-001 tiene solución**: existen paquetes que cubren Revit 2026 exponiendo `RevitAPI` y
  `RevitAPIUI`. **No procede el cierre en negativo del PoC ni activar FR-009 en esta fase.**
- **La elección del paquete queda deliberadamente abierta**: es la decisión siguiente, no la de este
  documento. Los hechos que la condicionan están en §4 y §6.
- **Se aporta, sin carácter vinculante, cuál es el candidato con mejor evidencia**:
  `Nice3point.Revit.Api.RevitAPI` + `Nice3point.Revit.Api.RevitAPIUI` **2026.4.10** — único que
  combina TFM exacto (`net8.0-windows7.0`), empaquetado `ref/` de solo compilación, línea 2026
  parcheada hasta la update 4, repositorio público con actividad de esta misma semana, MIT, prefijo
  verificado en NuGet y adopción real del ecosistema. Su debilidad conocida y documentada es que
  **redistribuye la DLL íntegra de Autodesk** (§6), no que esté abandonado.
- **Se señala una tensión con la letra de FR-001** para que la resuelva quien decida, no un agente por
  su cuenta: FR-001 pide «solo metadatos», y **el único candidato que lo es en sentido binario
  estricto (`ricaun.RevitAPI.Fake.References`) tiene el repositorio caído, no llega a 2027 y le falta
  la superficie WPF**. Si «solo metadatos» se interpreta como *(a) referencia de solo compilación que
  no se redistribuye con el addin* — que es lo que ADR-008 realmente necesita para habilitar el CI —
  hay varios candidatos válidos. **Si se interpreta como (b), el PoC se queda prácticamente sin
  opciones viables.** Esa interpretación hay que fijarla antes de escribir el addin trivial.

---

# DECISIÓN — elección del paquete y versión exacta

> Sección añadida el **2026-08-17**, en la tarea 2 del Lote 1 (`plan.md`). Todo lo anterior
> (§1–§8) es reconocimiento y **no se modifica**. Esta parte decide sobre aquellos hechos.

## 9. La ambigüedad de §2, resuelta por el usuario

La tensión que §8 dejó deliberadamente abierta — qué significa «solo metadatos» en FR-001 — **la
ha resuelto el usuario, no un agente**:

> **«Solo metadatos» se interpreta como la lectura (a): por *empaquetado*.** Es decir, referencia
> de solo compilación (`ref/` sin `lib/`), que no se copia a la salida ni se redistribuye con el
> addin, **aunque el `.nupkg` siga conteniendo la DLL original e íntegra de Autodesk**.

Razón registrada de esa elección: es lo que ADR-008 necesita **en la práctica** — compilar sin
Revit instalado y habilitar el CI. La lectura (b) (binario desnudado de implementación) es más
estricta pero deja el PoC sin opciones viables (§3.4).

**Consecuencias inmediatas de esa resolución:**

- `ricaun.RevitAPI.Fake.References` **queda fuera de alcance**: era el único candidato de lectura
  (b) y, además, su repositorio declarado está en 404 (§3.4). No se evalúa aquí.
- Los candidatos elegibles se reducen a los tres que cumplen (a) y cubren 2026 con `RevitAPI` +
  `RevitAPIUI`: **Nice3point** (§3.1), **`Revit_All_Main_Versions_API_x64`** (§3.2) y
  **`Speckle.Revit.API`** (§3.3).
- El riesgo de licencia que la lectura (b) habría evitado **se asume conscientemente** y queda
  documentado como salvedad conocida no bloqueante en §11.

## 10. Decisión

### 10.1 El par de paquetes y la versión, exactos

**Decisión: `Nice3point.Revit.Api.RevitAPI` y `Nice3point.Revit.Api.RevitAPIUI`, ambos en la
versión exacta `2026.4.10`.**

| Campo | Valor |
|---|---|
| Paquete 1 (id exacto NuGet) | `Nice3point.Revit.Api.RevitAPI` |
| Paquete 2 (id exacto NuGet) | `Nice3point.Revit.Api.RevitAPIUI` |
| Versión de ambos | **`2026.4.10`** — **fija, sin rango flotante** |
| Publicador | `Nice3point` (tercero no oficial, individual; prefijo verificado en nuget.org) |
| Publicación | 2026-05-12 (`RevitAPI` 17:17:53Z · `RevitAPIUI` 17:17:55Z) |
| TFM del paquete | `net8.0-windows7.0` |
| Dependencias transitivas | ninguna (grupo `net8.0-windows7.0` vacío en ambos nuspec) |
| Licencia del `.nupkg` | MIT (fichero `License.md`) — ver salvedad de §11 |
| Commit de origen de ambos | `a6ccafba5df2d929298199f43d6c081b2cd64f0d` (mismo en los dos nuspec → par coherente, no dos builds distintos) |

Son **dos paquetes separados** porque el mantenedor publica un paquete por ensamblado (§3.1.1);
eso es exactamente el caso previsto en el enunciado de esta tarea («o par de paquetes, si
`RevitAPI`/`RevitAPIUI` vienen en paquetes separados»).

### 10.2 Forma exacta de declararlo en el `.csproj` (vinculante para el Lote 2)

```xml
<PackageReference Include="Nice3point.Revit.Api.RevitAPI"   Version="[2026.4.10]" />
<PackageReference Include="Nice3point.Revit.Api.RevitAPIUI" Version="[2026.4.10]" />
```

Tres precisiones que **no son estilo, son parte de la decisión**:

1. **Notación de corchetes `[2026.4.10]` = versión exacta.** En NuGet, `Version="2026.4.10"` sin
   corchetes significa «≥ 2026.4.10» y puede resolver hacia arriba; `[2026.4.10]` fija el rango a
   esa única versión. Aquí no hay dependencias transitivas que pudieran empujarla, así que en la
   práctica resolvería igual, pero el corchete elimina la ambigüedad y hace la intención legible.
2. **Se rechaza explícitamente la sintaxis que recomienda el propio README del paquete**:
   `<PackageReference Include="Nice3point.Revit.Api.RevitAPI" Version="$(RevitVersion).*"/>`
   (verbatim del README, consultado 2026-08-17). Ese comodín `.*` es un **rango flotante**: una
   compilación futura podría traer una `2026.5.x` distinta a la verificada en Revit vivo y el PoC
   dejaría de significar lo que dice. El enunciado de esta tarea prohíbe el rango flotante, y con
   razón: el veredicto del PoC solo vale para el binario que se verificó.
3. **Nada de `HintPath`, nada de rutas locales** en `PocRevitAddin.Nuget.csproj` (FR-003). La
   referencia por ruta local vive únicamente en `PocRevitAddin.Local.csproj` (FR-004), que es el
   término de comparación.

No hace falta `ExcludeAssets="runtime"` ni `PrivateAssets`: un paquete con `ref/` y sin `lib/` no
aporta activo de runtime por definición (§3.1.4). **Y eso es justamente lo que hay que comprobar,
no añadir a ciegas** — ver §12.

## 11. Por qué este y no los otros dos (ADR)

**Decisión** → `Nice3point.Revit.Api.RevitAPI` + `.RevitAPIUI` `2026.4.10`.
**Alternativas descartadas** → `Revit_All_Main_Versions_API_x64` `2026.0.0` · `Speckle.Revit.API`
`2026.0.0`. (`ricaun.RevitAPI.Fake.References`: fuera de alcance por §9;
`Chuongmep.Revit.Api.RevitAPI`, `Autodesk.Revit.SDK`, `Revit.RevitApi.x64`: ya descartados en §5 y
§3.5.)

Comparativa de los tres elegibles, **solo con datos ya verificados en §3**:

| Criterio | Nice3point 2026.4.10 | `Revit_All_Main_Versions_API_x64` 2026.0.0 | `Speckle.Revit.API` 2026.0.0 |
|---|---|---|---|
| Cobertura de la **línea** 2026 | 6 versiones, hasta la update 4 | **1 sola**, la del lanzamiento | **1 sola** |
| Fecha de la versión de 2026 | **2026-05-12** | 2025-04-03 | 2025-04-08 |
| TFM del paquete | **`net8.0-windows7.0`** (= target del proyecto) | `net8.0` | no verificado |
| Repositorio auditable | **sí**, `Nice3point/RevitApi`, push 2026-08-13 | **no** (sin `projectUrl` ni `repository`) | de Speckle, no consultado |
| Licencia declarada | MIT (fichero) | **ninguna** | no verificada |
| Prefijo verificado en NuGet | **sí** | no consta | n/a |
| Adopción por terceros | **19 paquetes + 9 repos** dependientes | 974 k descargas, sin dependientes documentados | dependencia interna de sus conectores |
| Empaquetado de solo compilación | **`ref\net8.0-windows7.0\` verificado en el `.csproj` de empaquetado** | declarado en prosa («'Copy Local' set to False»), sin fuente auditable | no declarado |

**Los cuatro motivos que deciden, por orden de peso:**

1. **Es el único cuya línea 2026 sigue las updates del producto.** Nice3point publicó
   `2026.0.4 → 2026.1.0 → 2026.2.0 → 2026.3.0 → 2026.4.0 → 2026.4.10`; los otros dos publicaron
   **una única versión, en abril de 2025, y no la volvieron a tocar**. El segundo edge case de
   `requirements.md` es precisamente «desajuste de versión entre los metadatos del paquete y el
   runtime real de Revit 2026»: unos metadatos congelados en el lanzamiento son, por construcción,
   el candidato con más probabilidad de sufrirlo si la máquina del usuario está actualizada.
2. **Es el único con `net8.0-windows7.0`, que es exactamente el target del addin** (`net8.0-windows`,
   §Entorno de `CLAUDE.md`). Los otros publican `net8.0`, consumible desde `net8.0-windows` pero con
   una capa más de resolución de assets que no aporta nada aquí. Menos superficie donde equivocarse.
3. **Es el único con la afirmación de "solo compilación" verificable en el código de empaquetado**,
   no en la descripción del paquete: `PackagePath="ref\$(TargetFramework)\"` + supresión de `NU5131`
   (§3.1.4). En la lectura (a), que es la que el usuario ha fijado, **esa es la propiedad decisiva
   del paquete** — y en los otros dos solo hay prosa comercial respaldándola.
4. **Es el único auditable y con salida en caso de abandono.** Repo público, MIT, push hace 4 días,
   parches de las líneas 2024 y 2025 publicados en agosto de 2026. `Revit_All_Main_Versions_API_x64`
   no publica ni repositorio ni licencia: si su autor para, **nadie puede continuarlo, y ni siquiera
   se sabe bajo qué términos se está usando**. La ausencia de licencia declarada es, de hecho, peor
   posición jurídica que el MIT discutible de Nice3point (§11.1).

**Motivo específico para descartar `Speckle.Revit.API`**, más allá de lo anterior: es
**infraestructura interna de los conectores de Speckle**, no un producto pensado para consumo por
terceros (§3.3). Su continuidad depende de una decisión de producto ajena al ecosistema de
plugins; no hay compromiso alguno con consumidores externos. Un cambio de estrategia de Speckle lo
retira sin previo aviso y sin que exista comunidad que lo herede.

**Lo que NO decidió la elección**, para que conste: el número de descargas.
`Revit_All_Main_Versions_API_x64` tiene más descargas totales (974 k vs 632 k) y aun así se
descarta. Volumen histórico no es mantenimiento actual.

### 11.1 Por qué `2026.4.10` y no otra versión de la línea 2026

- **Es la única versión de la línea 2026 que está *listed*** en nuget.org. Las cinco anteriores
  (`2026.0.4`, `2026.1.0`, `2026.2.0`, `2026.3.0`, `2026.4.0`) están **deslistadas**
  (`listed: false`, `published: 1900-01-01`, §3.1.2): el mantenedor deslista de forma sistemática
  todas las versiones antiguas de cada línea anual. Una versión deslistada sigue siendo
  restaurable si se fija exacta, pero **depender de un binario que su autor retiró del escaparate
  es tomar deliberadamente la opción peor mantenida**.
- **No se elige una versión de la línea 2027** (`2027.2.0` es la más reciente del repositorio):
  el proyecto tiene como único target Revit 2026 (`tech-spec.md`, Constraints: «Solo Revit 2026»).
- **Interpretación del número, marcada como inferencia**: por el patrón de los mensajes de commit
  del repo («Build 25.4.60.9», «Build 24.3.60.12», §3.1.3), `2026.4.10` parece corresponder a la
  **update 4 de Revit 2026**. **No está verificado** contra un número de build oficial de Autodesk,
  y no hace falta para decidir: ver el riesgo real en §12.3.

## 12. Salvedad conocida y NO bloqueante: licencia y redistribución

> **Esta subsección está escrita para ser leída y citada tal cual desde `VEREDICTO.md` (Lote 5),
> sin necesidad de volver a investigar nada.**

**El hecho, verificado (§3.1.4 y §6):**

- El `.nupkg` de `Nice3point.Revit.Api.RevitAPI` **2026.4.10** contiene la **`RevitAPI.dll`
  original e íntegra de Autodesk** (35.120.472 bytes), no un ensamblado de referencia desnudado.
  Lo mismo con `RevitAPIUI.dll` (2.851.672 bytes) y con los `.xml` de documentación de la API.
  El propio README lo dice verbatim: **«Only original files from the latest Revit installation
  image are used.»**
- El `.nupkg` se publica bajo **licencia MIT, copyright 2022 Nice3point**
  (`https://raw.githubusercontent.com/Nice3point/RevitApi/main/LICENSE.md`, consultado 2026-08-17).
  Ese `LICENSE.md` **no menciona a Autodesk en ningún punto**, ni sus ensamblados, ni ningún permiso
  de redistribución.
- Por tanto: **no consta permiso documentado de Autodesk para redistribuir esos binarios.** La
  licencia MIT cubre el trabajo de empaquetado del mantenedor, no los binarios de Autodesk que
  viajan dentro del paquete.

**La tensión con ADR-008, dicha sin rodeos:** el *Context* de ADR-008 justifica el paquete de
metadatos porque «`RevitAPI.dll` no es redistribuible». La solución adoptada **no elimina esa
redistribución: la delega en un tercero.** El proyecto deja de redistribuir la DLL, y a cambio
depende de un paquete que sí la redistribuye.

**Por qué NO bloquea el PoC ni la decisión:**

1. **No hay alternativa que lo evite y sea viable.** Los otros dos candidatos elegibles tienen
   exactamente el mismo problema, y uno de ellos agravado: `Revit_All_Main_Versions_API_x64`
   declara `copyright: Autodesk` en su nuspec **y no declara licencia alguna**. El único candidato
   sin este problema (`ricaun`, binario propio generado) quedó fuera de alcance por §9 y tiene el
   repositorio en 404.
2. **No hay paquete oficial de Autodesk que exponga la API** (§3.6, verificado: `owner:autodesk
   revit` devuelve un único paquete de 20,82 KB que no contiene la API).
3. **Es práctica universal y visible en el ecosistema de plugins de Revit**: `RevitLookup` (1,4 k ★)
   y `RevitAddInManager` (488 ★), entre otros 19 paquetes y 9 repos públicos, consumen estos mismos
   paquetes.
4. **El riesgo es de consumo, no de distribución del producto propio**: el paquete se usa para
   compilar, sus binarios no se copian a la salida (§10.2) y **nada de Autodesk viaja dentro del
   artefacto que este proyecto distribuye**. Lo que queda en el `.csproj` es una dependencia de
   restauración desde nuget.org.

**Dónde SÍ importa, y por eso debe quedar visible:**

- ADR-008 tiene entre sus motivaciones explícitas «complica que un tercero compile el proyecto, que
  ahora es un objetivo». En el momento en que **«distribución a terceros» pase de motivación a
  objetivo declarado con compromiso**, esta salvedad hay que reevaluarla: quien compile el proyecto
  descargará de nuget.org un paquete con binarios de Autodesk redistribuidos sin permiso acreditado.
- **Riesgo concreto y su forma de materializarse**: si Autodesk solicitase la retirada de estos
  paquetes de nuget.org, la restauración dejaría de funcionar para todo el mundo, incluido el CI.
  No es hipotético en el sentido de imposible: es el escenario que la licencia no cubre.
- **Mitigación disponible, barata y ya identificada** (§6): los `.nupkg` de nuget.org son
  inmutables y la versión está fijada exacta, así que basta con **archivar los dos `.nupkg` de
  `2026.4.10`** (o alimentar una carpeta de paquetes local) para que el proyecto siga compilando
  aunque desaparezcan del feed público. Y el fallback de FR-009 (referencia por ruta local) sigue
  siendo viable en cualquier momento: **la decisión es reversible con un cambio de `<Reference>`**,
  al coste de perder el CI de compilación.

**Redacción lista para `VEREDICTO.md`** (copiar y pegar, ajustando solo el resultado):

> El paquete elegido (`Nice3point.Revit.Api.RevitAPI` / `.RevitAPIUI` `2026.4.10`) es «solo
> metadatos» en el sentido de **empaquetado** — `ref/` sin `lib/`, no se copia a la salida — pero
> **no** en el sentido de contenido binario: el `.nupkg` incluye la `RevitAPI.dll` original de
> Autodesk (35,1 MB) bajo licencia MIT del empaquetador, **sin permiso de redistribución
> acreditado por Autodesk**. Interpretación de «solo metadatos» fijada por el usuario el
> 2026-08-17. No bloquea el PoC (ningún candidato viable lo evita y no existe paquete oficial de
> Autodesk), pero **debe reevaluarse antes de declarar «distribución a terceros» como objetivo con
> compromiso**. Mitigación identificada: archivar los `.nupkg` de la versión fijada; salida:
> FR-009 (referencia por ruta local), reversible con un cambio de `<Reference>` a cambio de perder
> el CI de compilación. Detalle completo y fuentes: `RECONOCIMIENTO.md` §12.

## 13. Riesgos abiertos que esta decisión NO cierra

Se listan para que el Lote 2 y el Lote 3 los ataquen a propósito, no por sorpresa.

1. **`ref/` es inferencia sólida, no inspección directa** (§7.1). No se ha abierto el `.nupkg`.
   **Falsación trivial y obligatoria en el Lote 3**: si tras `dotnet build` aparece una
   `RevitAPI.dll` de ~35 MB en `bin\Debug\net8.0-windows\`, la lectura (a) es falsa para este
   paquete y hay que replantear. Si no aparece, queda confirmada por observación.
2. **Nada de lo decidido aquí demuestra que compile** (§7.7). SC-001 solo lo prueba el runner de
   GitHub Actions: en esta máquina Revit 2026 está instalado y un build local en verde no
   distingue entre «resolvió por el paquete» y «resolvió por otra vía».
3. **Desajuste de update entre metadatos y runtime** (§7.9, edge case de `requirements.md`): no
   está verificado si la máquina del usuario tiene la update 4 de Revit 2026 o una anterior. Para
   **este** PoC el riesgo es muy bajo — el addin trivial solo usa `IExternalApplication`,
   `RibbonPanel`/`PushButtonData`, `IExternalCommand` y `TaskDialog`, superficie que existe desde
   hace más de una década y no puede haberse introducido en una update de 2026. Para el proyecto
   final sí importa. **Comprobación barata para quien ejecute el Lote 2/3**, si hiciera falta:
   `(Get-Item 'C:\Program Files\Autodesk\Revit 2026\RevitAPI.dll').VersionInfo.FileVersion`.
4. **Bus factor**: `Nice3point` es una persona individual (§6). Mitigado, no eliminado: repo
   público con licencia MIT sobre el empaquetado, y los `.nupkg` ya publicados son inmutables.
5. **Riesgo de licencia de §12**, que se asume conscientemente.

## 14. Estado de FR-009

**No se activa.** Existen paquetes que cubren Revit 2026 completo exponiendo `RevitAPI` +
`RevitAPIUI` (§2, §4), se ha elegido uno con versión exacta (§10) y **no procede el cierre en
negativo del PoC en el Lote 1 ni el salto al Lote 5**. El Lote 2 continúa según `plan.md`.

---

## 15. Cierre — resultado de los Lotes 2, 3 y 4 (2026-08-18)

> Añadido al terminar la verificación en Revit vivo. No modifica ninguna decisión de §9-§14, solo
> confirma con hechos posteriores lo que ahí quedaba pendiente de comprobar.

- **§13.1 (¿el ensamblado está realmente en `ref/`, no en `lib/`?) — falsado en positivo.** Tras
  `dotnet build` sobre `PocRevitAddin.Nuget.csproj`, `bin\Debug\net8.0-windows\` **no** contiene
  `RevitAPI.dll`/`RevitAPIUI.dll`, solo el DLL propio del addin. La lectura (a) fijada por el
  usuario en §9 se sostiene para este paquete concreto: ya no es inferencia, es observación directa.
- **§13.2 (¿compila de verdad sin Revit?) — confirmado por CI, no por esta máquina.** Workflow de
  GitHub Actions en un runner `windows-latest` sin Revit instalado, en verde: `dotnet build` Debug y
  Release limpios, `dotnet test` 3/3. Evidencia:
  https://github.com/Ashromer/prompt-to-revit/actions/runs/32069521493. SC-001 y SC-004 cumplidos.
- **SC-003 (expone `RevitAPI`/`RevitAPIUI`) — confirmado por inspección, no por el README.**
  `dotnet list package` sobre el build NuGet resuelve `Nice3point.Revit.Api.RevitAPI [2026.4.10]` y
  `Nice3point.Revit.Api.RevitAPIUI [2026.4.10]` como referencias directas.
- **SC-002 (¿carga en Revit vivo y funciona igual que el build local?) — confirmado por el
  usuario.** `GUION-VERIFICACION.md` completo: build NuGet solo y build Local solo, cada uno con
  ribbon + tooltip + `TaskDialog` correctos; tabla de equivalencia con **veredicto: Equivalente**.
  Hallazgo real pero no bloqueante durante la verificación: cargar los dos addins de PRUEBA a la vez
  (no lo que pide Historia 2, que es cargar cada build por separado) produce un choque de nombre de
  panel entre los dos `.addin` — texto de la excepción anotado por el usuario, documentado en la
  nota de la sección 2 de `GUION-VERIFICACION.md` (`Captura.PNG` no captura ese diálogo: es de la
  sesión posterior, con el build Local ya en solitario). No es el paquete NuGet fallando contra el
  runtime: **no activa FR-009**.
- **§13.3 (desajuste de update Revit vs. metadatos) — sin incidencia, y confirmado por observación.**
  Ambos builds cargaron y funcionaron sin diálogo de error de carga de addin. Además, la barra de
  título de `Captura.PNG` muestra **"Autodesk Revit 2026.4"**: la máquina del usuario está en la
  update 4, la misma que los metadatos `2026.4.10`. Sigue abierto para el proyecto final (superficie
  de API más amplia que la de este addin trivial).

**Conclusión: los cinco success criteria de `requirements.md` (SC-001 a SC-005) quedan cumplidos.
FR-009 no se activa en ningún punto. ADR-008 queda confirmado con la salvedad de licencia de §12,
no bloqueante.** Veredicto completo y decisión formal sobre ADR-008: `VEREDICTO.md`.
