# SnapTime - Requisitos de UI

## 1) Decisión tecnológica
- La UI será una aplicación Blazor WebAssembly ejecutándose en localhost.
- Se comunica con el backend vía API REST.

## 2) Objetivo funcional de la UI
La UI debe permitir al usuario operar todo el flujo de análisis y revisión de forma clara, rápida y controlada.

## 3) Estructura visual del MVP
- **Pantalla única** con paneles (sin navegación entre páginas).
- **Panel superior**: estado del proceso, métricas básicas y botones de control. Ocupa todo el ancho.
- **Panel izquierdo** (20%): estructura de carpetas en árbol estilo Windows.
- **Panel central** (60%): grid de miniaturas. Barra de breadcrumb con contador de archivos. Sin Virtualize — carga manual con `@foreach` + `CancellationTokenSource`. Navegación interna (doble click subcarpeta) independiente del árbol izquierdo. Estados visuales: círculo de 16px (gris pendiente, verde correcto, rojo error, #ffc107 sin sugerencia, azul con sugerencia). Vídeos con `<video preload="metadata">`, badge ▶. Miniaturas servidas desde disco vía `GET /api/thumbnails/from-file?path=`, sin dependencia de BD.
- **Panel derecho** (20%): dos subpaneles apilados verticalmente con `display: flex; flex-direction: column`. Arriba, detalle de la foto seleccionada (metadatos, evidencias, barra de confianza; botones Aceptar/Rechazar como placeholders no funcionales hasta F7). Abajo, chat conversacional Ollama.
- **Ventana modal de configuración**, abierta desde un botón del panel superior.

## 4) Requisitos funcionales de UI

### 4.1) Selección de ruta raíz
- No hay input de texto. La ruta se selecciona exclusivamente desde el árbol del panel izquierdo.
- Al hacer clic en el nombre de una carpeta del árbol, se resalta como seleccionada (solo una a la vez).
- Un toggle "Incluir subcarpetas" (checkbox) controla si el escaneo es recursivo (por defecto: sí).
- La ruta seleccionada y el flag de subcarpetas se pasan al panel superior (4.4) para el botón "Escanear".
- Si no hay carpeta seleccionada, se usa `sample/` como ruta por defecto.

### 4.2) Panel central: grid de archivos multimedia
- Cuadrícula de archivos con miniatura (fotos) o vídeo nativo con `<video preload="metadata">` (vídeos, badge ▶右下).
- Indicadores visuales sobre la miniatura/icono: círculo de estado de 16px que muestra el **`MediaStatus`** del archivo (gris = Pending, verde = Correct, rojo = Error, #ffc107 = NoSuggestion, azul = HasSuggestion). Además, cuando una sugerencia se acepta (`SuggestionStatus = Approved`), el círculo cambia a **azul oscuro (#1565C0)** para reflejar el estado de revisión.
- Al hacer clic en una miniatura, se muestra el detalle en el panel derecho (no inline). Doble click en subcarpeta navega dentro del grid independientemente del árbol izquierdo.
- **Solo visualización.** Sin acciones (aceptar/rechazar) desde el grid. Sin checkboxes ni selección múltiple. La aprobación/rechazo de sugerencias se hace desde el panel de detalle (individual) o desde los botones de lote en el panel superior.
- **Sin Virtualize:** reemplazado por `@foreach` manual + llamada asíncrona con `CancellationTokenSource` para cancelar peticiones en curso al navegar rápido.
- Las miniaturas se sirven desde disco (`GET /api/thumbnails/from-file?path=`), no desde BD.

### 4.3) Vista de detalle
- Se abre en la sección superior del panel derecho (no inline en el grid).
- Muestra: ruta completa, tamaño, fechas EXIF (4 tags: Original, SubSec, Creado, Modificado), fechas filesystem (ctime/mtime). Para archivos escaneados: fecha sugerida, heurística aplicada, barra de confianza visual (0-100, verde ≥80, amarillo 50-79, rojo <50) y lista de evidencias. Para archivos no escaneados: metadatos básicos leídos directamente del disco vía `GET /api/media-assets/from-file?path=`, sin evidencias ni barra de confianza.
- Si no hay foto seleccionada, muestra placeholder "Selecciona una foto".
- Al navegar (breadcrumb, subir, doble click subcarpeta) la selección se limpia automáticamente.
- Botones Aceptar/Rechazar: placeholders no funcionales hasta F7. En F7 se activan solo si:
  1. El proceso de escaneo no está en ejecución (idle, completed o cancelled).
  2. El archivo tiene una recomendación (`SuggestedDate` no nulo).
  Si no se cumplen ambas, los botones se muestran deshabilitados (atenuados).

### 4.4) Panel superior
- **ScanPanel** (sección izquierda del panel superior):
  - Botón "Escanear" — se deshabilita durante la ejecución del scan, se rehabilita al finalizar o cancelar.
  - Botón "Cancelar" — visible solo durante el scan.
  - Progreso: "Procesando N de M archivos".
  - Al escanear una carpeta ya escaneada, se fuerza el reescaneo completo (datos previos reemplazados).
- Botón para abrir configuración (modal).
- Métricas: resumen de confianza post-scan (altas, revisar, sugerir corrección).
- Estados de proceso visibles: idle, scanning, cancelled, completed, error.
- Botones de revisión por lote: Aceptar Carpeta / Rechazar Carpeta (carpeta actual visible) y Aceptar Total / Rechazar Total (todo lo escaneado). Los botones de lote abren modal de confirmación con resumen antes de aplicar.
- Los botones de lote se habilitan solo si el scan no está activo y hay al menos un archivo con recomendación en el ámbito correspondiente.
- Botón "Limpiar": elimina todos los datos de escaneo de la base de datos (assets, metadatos, evidencias, auditoría), conservando la configuración. Se muestra siempre activo cuando hay datos escaneados. Requiere confirmación explícita en modal antes de ejecutar.

### 4.5) Pantalla de configuración (modal)
- Ventana modal flotante sobre el grid.
- Permite modificar umbral global de confianza.
- Permite activar/desactivar heurísticas individualmente.
- Permite ajustar pesos de heurísticas habilitadas.
- Aplica los cambios en runtime.

### 4.6) Paginación
- Tamaño de página configurable: 20, 50, 100 y "Todas".
- "Todas" desactiva paginación numérica; las miniaturas se cargan bajo demanda (lazy loading) conforme entran en viewport.
- Orden por defecto en MVP: nombre.
- El usuario puede cambiar el criterio de ordenación.

### 4.7) Aplicación de cambios
- Modal con lista de cambios: archivo, fecha actual → fecha nueva para cada ítem.
- Botón "Aplicar" o "Cancelar".
- Requiere confirmación explícita.
- Las fechas que se muestran al usuario deben formatearse como `dd/MM/yyyy`. Las fechas sugeridas deben resaltarse visualmente (por ejemplo con `<strong>`). El modal mostrará un resumen final con los archivos que fallaron y la causa.
- Botón "Iniciar" en el panel superior: deshabilitado hasta que se complete al menos un escaneo. Al pulsarlo, abre el modal de confirmación con la lista de cambios a aplicar.

### 4.8) Filtros (Fase 2)
- Los filtros por score, carpeta, rango de fechas, estado de revisión se implementan en Fase 2.

### 4.9) Chat conversacional (panel derecho)
- Panel de chat en el lado derecho, ancho 15% de la pantalla.
- Campo de texto para escribir comandos en lenguaje natural.
- Historial de mensajes visible en el panel (usuario y respuestas del sistema).
- Los mensajes se envían al backend, que usa un LLM local (Ollama) para interpretar el mensaje y ejecutar las MCP tools correspondientes.
- El LLM tiene acceso a las MCP tools como herramientas (tool calling) y responde en lenguaje natural con el resultado.
- Indicador visual de "escribiendo..." mientras el LLM procesa.
- Los resultados del chat pueden reflejarse en los otros paneles (ej: al escanear una carpeta, el grid se actualiza).

## 5) Requisitos del árbol de carpetas
- Árbol de carpetas del sistema de archivos con expand/colapsar por nodo.
- Al hacer clic en el nombre de una carpeta, se resalta visualmente como seleccionada (solo una a la vez).
- No hay checkboxes ni selección múltiple.
- Toggle "Incluir subcarpetas" para controlar recursividad del escaneo.
- En el árbol se muestra, para cada carpeta, ▶ + nombre.
- Al hacer clic en una carpeta proporciona la ruta al panel superior para escanear.
- La carga de hijos es bajo demanda vía API.

## 6) Requisitos de rendimiento en listados grandes
- El cliente nunca debe cargar toda la biblioteca en memoria de una sola vez.
- Paginación obligatoria (salvo opción "Todas", que usa lazy loading).
- Renderizado virtualizado para evitar bloqueos de UI en colecciones grandes.
- Consultas del backend optimizadas para filtros y ordenación sobre grandes volúmenes.

## 7) Requisitos de experiencia de usuario
- Los estados del sistema deben ser inequívocos (idle, scanning, cancelled, completed, error).
- Las acciones potencialmente destructivas deben requerir confirmación.
- Los mensajes de error deben indicar causa y siguiente acción recomendada, mostrados en el propio panel.
- La UI debe ser usable con bibliotecas grandes sin bloquear la interacción.
- La UI tiene tamaño fijo con scroll si la ventana es más pequeña de lo esperado.
- Sin atajos de teclado en MVP.
- Estados visuales contemplados: carga (spinner), vacío (mensaje informativo con icono), error (mensaje en panel).

## 8) Estrategia de tests de UI

La UI Blazor WASM se valida con dos niveles de test:

- **bUnit** (`tests/SnapTime.Client.Tests/`): tests unitarios de componentes. Verifican renderizado, eventos, estado visual y lógica de cada componente de forma aislada. Rápidos, sin navegador.
- **Playwright** (`tests/SnapTime.E2ETests/`): tests E2E con navegador real (Chromium). Validan flujos completos (selección de carpeta → grid → detalle → corrección) contra el servidor API real.

**Reglas:**
- Todo componente nuevo debe tener un test bUnit que cubra su renderizado básico y sus estados (carga, vacío, error, datos).
- Los flujos críticos (escaneo, revisión, aplicación de cambios, selección/detalle, navegación) deben tener un test Playwright.
- Los tests E2E deben ser **autónomos**: arrancan el servidor web antes de la ejecución y lo paran al finalizar, sin depender de un servidor externo ya funcionando.
- Los tests E2E se ejecutan contra un servidor real y una base de datos SQLite efímera (ver `SqliteDbFixture`).

**Tests E2E existentes:**
- `PhotoGridE2ETests.cs` (3 tests): selección de carpeta carga grid, doble click subcarpeta navega, breadcrumb navega arriba.
- `PhotoDetailE2ETests.cs` (5 tests, pendientes de ejecución con web): click thumbnail muestra detalle, muestra metadatos, cambio de foto actualiza detalle, subcarpeta + foto mantiene breadcrumb, breadcrumb limpia detalle.

## 9) Panel izquierdo: árbol del sistema de archivos

El panel izquierdo muestra un árbol completo y navegable del sistema de archivos, visible permanentemente (sin modal). Es la herramienta principal de navegación.

### 9.1) Funcionamiento general
- Al cargar la página, el panel izquierdo carga los directorios raíz del sistema y los muestra como un árbol expandible.
- Cada nodo tiene un ▶ que expande/colapsa los subdirectorios (carga bajo demanda vía API).
- Al hacer clic en el nombre de una carpeta:
  - La carpeta se resalta como seleccionada (solo una a la vez).
  - La ruta se usa como `rootPath` para el escaneo.
  - En el futuro, también filtra el grid central a esa carpeta.
- Un toggle "Incluir subcarpetas" (checkbox) controla si el escaneo recorre subdirectorios.
- El árbol es **siempre visible** mientras se interactúa con los otros paneles.

### 9.2) Comportamiento del árbol
- **Árbol recursivo** sin límite de profundidad: cada nodo puede expandirse y mostrar sus hijos como nuevos nodos.
- La expansión es **lazy**: solo se carga un nivel cuando el usuario hace clic en ▶.
- Los resultados se **cachean** por nodo (no se vuelve a consultar la API al colapsar/re-expandir).
- Cada nodo puede estar en uno de estos estados:
  - **Colapsado** (por defecto): solo se ve el nombre y ▶.
  - **Cargando**: ▶ se reemplaza por `⋯` (spinner textual).
  - **Expandido**: ▶ rotado 90°, hijos visibles debajo.
  - **Error**: mensaje en rojo debajo del nodo.
  - **Vacío**: texto "(vacía)" si el directorio no tiene subdirectorios.
- **Indentación progresiva**: cada nivel suma 24px de padding-left (n1=0, n2=24, n3=48, ...).

### 9.3) Componentes Blazor
- **`FolderTreePanel.razor`**: panel que carga las raíces del sistema al montarse y las renderiza como `FolderTreeItem`.
  - Inyecta `IFilesystemClient`.
  - Al montarse: llama a `GET /api/filesystem/directories` (sin path) para obtener las raíces.
  - Muestra cada raíz como un `FolderTreeItem` con indentación 0.
  - Estados: carga inicial ("Cargando..."), error, listado de raíces.
  - Mantiene `SelectedPath` (la ruta de la carpeta seleccionada) y un callback `OnFolderSelected`.
  - Incluye un toggle "Incluir subcarpetas" (checkbox) con valor por defecto `true`.

- **`FolderTreeItem.razor`**: componente recursivo que representa un nodo del árbol.
  - Parámetros: `Path` (ruta absoluta del nodo), `Name` (nombre a mostrar), `OnSelect(EventCallback<string>)`, `Indent` (padding-left en px), `SelectedPath` (ruta seleccionada actual).
  - Inyecta `IFilesystemClient` para cargar hijos bajo demanda con `CancellationToken`.
  - Render: ▶ + nombre. Al hacer clic en ▶, llama a `GET /api/filesystem/directories?path={Path}` y muestra hijos como más `FolderTreeItem`.
  - Al hacer clic en nombre, invoca `OnSelect` con la ruta absoluta del nodo.
  - Si `Path == SelectedPath`, se aplica la clase CSS `selected` para resaltar visualmente el nodo.
  - Estados internos: colapsado, cargando (`⋯`), expandido, error, vacío.
  - Sin límite de profundidad: se renderiza a sí mismo recursivamente.
  - Implementa `IDisposable` para cancelar peticiones HTTP en curso.

- **`IFilesystemClient` / `FilesystemClient`**: servicio HTTP.
  - Método: `Task<string[]> GetDirectoriesAsync(string? path = null, CancellationToken ct = default)`.
  - Path `null` → `GET /api/filesystem/directories` (raíces del sistema).
  - Path con valor → `GET /api/filesystem/directories?path={Uri.EscapeDataString(path)}`.

### 9.4) Endpoint API: GET /api/filesystem/directories

**Ruta:** `GET /api/filesystem/directories?path={path}`

**Comportamiento:**
- Sin `path`: devuelve los directorios raíz del sistema operativo.
  - macOS/Linux: lista los directorios directamente en `/` (ej: `["Users", "Applications", "Library", ...]`), **no** devuelve `["/"]`.
  - Windows: devuelve las unidades con `DriveInfo.GetDrives()` (ej: `["C:", "D:", ...]`).
- Con `path`: devuelve los nombres de subdirectorios (solo nombre, no ruta completa) de esa ruta.

**Formato respuesta:** `string[]` — array plano de nombres de directorio.

**Filtros de seguridad:**
- Omite directorios sin permisos de lectura (try/catch por directorio).
- Omite directorios del sistema por nombre y/o prefijo de ruta:
  - macOS/Linux: `System`, `proc`, `sys`, `dev`, `cores`, `Volumes` (por nombre); `/System`, `/proc`, `/sys`, `/dev`, `/private/var`, `/cores`, `/Volumes`, `/private/etc`, `/private/tmp` (por prefijo).
  - Windows: `WINDOWS`, `Program Files`, `Program Files (x86)`, `ProgramData`, `System Volume Information`, `$Recycle.Bin`, `Recovery`, `WindowsApps`, `Windows10Upgrade`, `WinSxS`.
- Omite directorios con atributos `Hidden` o `System`.
- No expone archivos (solo directorios).

**Errores:**
- 404 si el path no existe o no se encuentra.
- 403 si no hay permisos de lectura.
- 400 si el path es demasiado largo o contiene caracteres inválidos.

**Multiplataforma:**
- Usa `OperatingSystem.IsWindows()` para diferenciar comportamiento.
- En macOS/Linux: rutas con `/`.
- En Windows: rutas con `\` (usar `Path.Combine` y `Directory.EnumerateDirectories`).

### 9.5) Reglas de construcción de rutas

Todas las rutas deben ser **absolutas** y construirse sin doble slash:
- Raíz → directorios: `"/Users"` (desde `"/"` + `"Users"`)
- Segundo nivel: `"/Users/javiermontoro"` (desde `"/Users"` + `"javiermontoro"`)
- Implementación: `$"{basePath.TrimEnd('/')}/{child.TrimStart('/')}"`

El componente `FolderTreeItem` usa su propio método `Combine` para construir rutas de hijos, con soporte para `/` y `\`.

### 9.6) Tests requeridos

**Tests de integración (SnapTime.IntegrationTests):**
- `GET /api/filesystem/directories` sin path → 200, array con directorios raíz.
- `GET /api/filesystem/directories?path=/` → 200, subdirectorios de raíz.
- `GET /api/filesystem/directories?path=/ruta/inexistente` → 404.
- `GET /api/filesystem/directories?path=//Users` (doble slash) → mismo resultado que `/Users`.
- Directorios del sistema filtrados correctamente.
- `POST /api/jobs` con `{ rootPath, includeSubfolders }` → flag persistido correctamente.

**Tests bUnit (SnapTime.Client.Tests):**
- `FolderTreePanel`:
  - Se monta y carga directorios raíz automáticamente.
  - Muestra "Cargando..." inicial, luego lista de raíces (mock HTTP).
  - Muestra error si la API falla.
  - Al hacer clic en una carpeta → se resalta (clase CSS `selected`).
  - Al hacer clic en otra carpeta → la anterior se desresalta.
- `FolderTreeItem`:
  - Renderiza nombre + ▶.
  - Al hacer clic en ▶ → expande y muestra hijos (mock HTTP).
  - Al hacer clic en nombre → llama a `OnSelect` con la ruta.
  - Si `SelectedPath` coincide con `Path` → tiene clase `selected`.
  - Estados: colapsado, cargando (`⋯`), error, vacío ("(vacía)").
  - Al hacer clic en ▶ estando expandido → colapsa.
  - Al hacer clic en ▶ de un hijo → expande recursivamente.

**Tests E2E (SnapTime.E2ETests):**
- Cargar página → verificar que el árbol de raíces se muestra.

### 9.7) Consideraciones de rendimiento
- Cada expansión de nodo hace una llamada HTTP independiente.
- Los resultados se cachean por nodo en memoria durante la vida del panel.
- Para árboles muy profundos (>10 niveles), el renderizado recursivo puede ser lento; en MVP esto no es un problema porque los directorios rara vez superan 5-6 niveles.

## 10) Formato de fechas

### 10.1) Requisito general
Todas las fechas mostradas en la UI al usuario deben estar en formato **`dd/MM/yyyy`** (día/mes/año).

### 10.2) Fechas de corrección (sugerencias)
Las fechas que representen **sugerencias de corrección** (es decir, `SuggestedDate` en la respuesta del análisis) deben renderizarse en **negrita** (`<strong>`) para destacarlas visualmente respecto a las fechas actuales del archivo.

Ejemplo de evidencia:
```
"Filename suggests 10/06/2024, but metadata has 15/06/2024"
```
Ambas fechas están en negrita (`<strong>`) para resaltar la información crítica.

### 10.3) Fechas actuales y metadatos
Las fechas de metadatos actuales (EXIF, filesystem, etc.) se muestran en formato `dd/MM/yyyy` sin negrita en el panel de detalle.

### 10.4) Implementación
- **Backend (heurísticas):** todas las descripciones de evidencias generadas por las heurísticas utilizan `ToString("dd/MM/yyyy")` y envuelven las fechas en tags `<strong>`.
- **Frontend (Blazor):** las descripciones de evidencias se renderizam como `(MarkupString)description` para que los tags HTML se interpreten correctamente.

---

## 11) Decisiones de diseño

### Navegación del grid limpia selección de detalle
- Cuando el usuario navega (breadcrumb, flecha subir, doble click subcarpeta), `PhotoGrid` emite `OnNavigate` y `Home.razor` limpia `_selectedPhotoId` / `_selectedPhotoPath`, ocultando el detalle. Esto evita mostrar información obsoleta al cambiar de carpeta.

### Identificación de subcarpetas en grid
- Los items directorio tienen la clase CSS `is-directory` para permitir selección por selectores de Playwright. La clase se añade condicionalmente con `@(item.IsDirectory ? " is-directory" : "")`.

### Botones Aceptar/Rechazar sugerencia (individual)
- Son placeholders no funcionales en `PhotoDetail.razor`. Se implementarán en F7 (revisión en lote). Renderizados siempre pero sin efecto hasta entonces.
- En F7 pasan a ser funcionales. No aprueban/rechazan la foto, sino su **sugerencia de fecha** (`SuggestedDate`):
  - **Activos** solo si: (a) el proceso de escaneo no está en ejecución (idle, completed o cancelled), y (b) el archivo tiene `SuggestedDate` no nulo.
  - Si no se cumplen ambas condiciones, los botones se muestran **deshabilitados** (opacidad reducida, cursor por defecto, sin respuesta al clic).
- Al hacer clic en Aceptar → `SuggestionReviewStatus` pasa a `Approved`. Al hacer clic en Rechazar → pasa a `Rejected`. El cambio se persiste en SQLite.
- El círculo de estado en el grid no cambia (sigue mostrando `MediaStatus`). El estado de revisión se refleja en el panel de detalle y opcionalmente como indicador secundario en el grid (Fase 2).

### Botones Aceptar Todo / Rechazar Todo / Aceptar Total / Rechazar Total (lote)
- Se ubican en el panel superior (4.4).
- **Aceptar Todo / Rechazar Todo:** opera sobre todos los archivos visibles en la carpeta actual del grid, sin selección previa.
- **Aceptar Total / Rechazar Total:** opera sobre todos los archivos escaneados hasta la fecha.
- Se habilitan solo si el scan no está activo y hay al menos un archivo con recomendación en el ámbito correspondiente.
- Al pulsarlos, se abre un modal de confirmación con resumen: "Se aprobarán/rechazarán N archivos".
- No hay selección múltiple en el grid. No hay checkboxes por fila.

### Subpanel de metainformación de carpeta
- Ubicación definitiva: **zona inferior del panel central (grid)**, debajo de las miniaturas/iconos.
- Muestra: número de archivos de la carpeta seleccionada (desglosado por tipo: imagen/vídeo) y resumen rápido de confianza (altas / revisar / sugerir corrección).
- Ocupa el ancho completo del panel central.
