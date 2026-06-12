# SnapTime - Requisitos funcionales (FR)

## FR-01 Selección de biblioteca

El sistema debe permitir seleccionar una ruta raíz local para escanear archivos multimedia (imágenes y vídeos).

### Criterios de aceptación

- El usuario puede elegir carpeta raíz desde UI.
- El sistema valida acceso/lectura antes de iniciar.
- El sistema permite incluir subcarpetas.

## FR-02 Descubrimiento de archivos

El sistema debe descubrir archivos de imagen (JPG/JPEG) y vídeo (MP4, MOV, y otros extensiones configurables) y registrar inventario inicial.

### Criterios de aceptación

- Se contabilizan archivos candidatos y archivos descartados.
- Se informa motivo de descarte (extensión no soportada, archivo corrupto, etc.).
- Las extensiones soportadas son configurables en BD (tabla `Settings`, columnas `ImageExtensionsCsv`/`VideoExtensionsCsv`).

## FR-03 Extracción de metadatos de fecha

El sistema debe extraer metadatos de fecha relevantes por archivo.

### Criterios de aceptación

- Soporta al menos: `EXIF:DateTimeOriginal`, `EXIF:CreateDate`, `EXIF:ModifyDate` (fotos) y `QuickTime:CreationDate`, `QuickTime:CreateDate`, `QuickTime:MediaCreateDate` (vídeos).
- Registra fechas de sistema de archivos (`mtime`/`ctime`) como evidencia secundaria.
- Guarda origen de cada valor (tag exacto/fuente).
- Detecta automáticamente el tipo de archivo y aplica el extractor de metadatos correspondiente (EXIF para imágenes, QuickTime para vídeos).

## FR-04 Análisis de coherencia y scoring

El sistema debe calcular una confianza (0-100) sobre la fecha actual de cada archivo multimedia.

### Criterios de aceptación

- La confianza se calcula por reglas/señales configurables.
- Cada score incluye explicación legible y desglose por señal.
- El score es reproducible con la misma versión de reglas y datos.

## FR-05 Sugerencia de fecha alternativa

El sistema debe sugerir una fecha alternativa cuando la confianza esté por debajo del umbral.

### Criterios de aceptación

- El umbral es configurable por usuario.
- Cada sugerencia incluye confianza propia y evidencia.
- Si no hay evidencia suficiente, el sistema marca "sin sugerencia fiable".

## FR-06 Análisis contextual por carpeta

El sistema debe usar información contextual de carpeta para apoyar inferencias.

### Criterios de aceptación

- Calcula tendencia temporal dominante por carpeta/lote.
- Detecta outliers temporales respecto al grupo.
- Permite desactivar esta señal en configuración.

## FR-07 Parseo semántico de nombres de carpeta/archivo

El sistema debe detectar pistas temporales en nombres (por ejemplo, "cumpleaños_mayo_2021").

### Criterios de aceptación

- Extrae fechas/rangos de texto común en ES/EN básico.
- Marca nivel de confianza bajo/medio para esta evidencia.
- Nunca usa esta señal como única base para aplicar cambios automáticamente.

## FR-08 Panel de revisión en UI

La UI debe mostrar un grid navegable de miniaturas de archivos multimedia con resultados de análisis.

### Criterios de aceptación

- Grid de miniaturas con indicadores visuales de score y estado (círculo de 16px: gris pendiente, verde correcto, rojo error, #ffc107 sin sugerencia, azul con sugerencia).
- Al hacer clic en una miniatura se abre el detalle en el panel derecho (no inline): ruta, tamaño, fechas EXIF + filesystem, sugerencia, evidencias, barra de confianza.
- Al hacer clic en otra miniatura, el detalle se actualiza. Al navegar (breadcrumb, subcarpeta), la selección se limpia.
- Las miniaturas se sirven desde disco (`GET /api/thumbnails/from-file?path=`), no desde BD.
- Doble click en subcarpeta navega dentro del grid independientemente del árbol izquierdo.
- Sin acciones de aprobación/rechazo en F6 (los botones son placeholders hasta F7).
- Filtros por score, carpeta, rango de fechas, estado de revisión (Fase 2).

## FR-09 Control de ejecución

La UI debe permitir iniciar y cancelar procesos de escaneo/análisis.

### Criterios de aceptación

- Cambios de estado visibles en tiempo real (idle, scanning, cancelled, completed, error).
- Cancelación cooperativa con cierre consistente del job.
- Progreso visible: archivos procesados / total.
- El botón de escaneo se deshabilita durante la ejecución y se rehabilita al finalizar o cancelar.
- Al escanear una carpeta ya escaneada previamente, se fuerza el reescaneo: los datos previos se eliminan y se recogen de nuevo desde cero.

## FR-10 Flujo de aprobación de cambios

El sistema debe permitir aceptar/rechazar sugerencias por ítem o por lote.

### Criterios de aceptación

- Soporta aprobación/rechazo individual desde el panel de detalle.
- Soporta aprobación/rechazo por lote sobre la carpeta actual visible en el grid.
- Soporta aprobación/rechazo por lote sobre el total de archivos escaneados.
 - Muestra resumen previo en modal con lista de cambios: archivo, fecha actual → fecha nueva. (La ejecución es real only; el modal sirve para confirmar.)
- Requiere confirmación explícita para aplicar (botón "Aplicar" / "Cancelar" en el modal).

## FR-11 Aplicación de cambios de metadatos

El sistema debe escribir la fecha aceptada en metadatos de forma controlada.

### Criterios de aceptación

- Registra resultado por archivo (ok/error/motivo).
- Nunca modifica sin consentimiento explícita del usuario (modal de confirmación).
- Proceso best-effort: aplica todo lo posible y devuelve listado de errores sin rollback.
- Al modificar la fecha, anota en los metadatos del archivo el valor original y la heurística responsable: campo `EXIF UserComment` (0x9286) en fotos, `QuickTime ©cmt` (UserData Comment) en vídeos.

## FR-12 Auditoría y trazabilidad

El sistema debe registrar eventos relevantes de análisis y cambios.

### Criterios de aceptación

- Guarda quién/cuándo/qué regla/versión produjo score/sugerencia.
- Conserva historial de cambios aplicados.
- Permite exportar reporte de auditoría.

## FR-13 Integración MCP

El sistema debe exponer capacidades clave vía MCP para uso por agentes.

### Criterios de aceptación

- Tools mínimas: escanear, listar baja confianza, obtener evidencia, proponer, aplicar con confirmación.
- MCP reutiliza el mismo core de negocio que la API/UI.

## FR-14 API de aplicación

El sistema debe ofrecer API para consumo de UI y procesos internos.

### Criterios de aceptación

- Endpoints para jobs, consulta de resultados, revisión y aplicación.
- Contratos estables versionados.

## FR-15 Configuración de heurísticas

El sistema debe permitir ajustar pesos, umbrales y activación/desactivación de heurísticas sin recompilar.

### Criterios de aceptación

- Config editable desde UI de administración (persistido en BD, no en archivo).
- Cada heurística puede activarse o desactivarse de forma independiente.
- Permite ajustar pesos de heurísticas habilitadas.
- Los cambios de configuración deben aplicarse en runtime sin reiniciar la aplicación.
- Los cambios de configuración quedan versionados en auditoría.

## FR-16 Pantalla de configuración de usuario

El sistema debe ofrecer una pantalla de configuración para gestionar parámetros de análisis.

### Criterios de aceptación

- Permite modificar umbral global de confianza.
- Permite activar/desactivar heurísticas individualmente.
- Permite ajustar pesos de heurísticas habilitadas.
- Muestra validaciones y errores de configuración de forma clara.
- Aplica los cambios en runtime.

## FR-17 Navegación de carpetas en árbol

La UI debe incluir un panel de carpetas con estructura en árbol estilo explorador de Windows.

### Criterios de aceptación

- Muestra la jerarquía completa de carpetas de la ruta seleccionada.
- Permite seleccionar y deseleccionar carpetas con checkboxes en cascada.
- La selección de una carpeta actualiza consistentemente nodos descendientes y estado del nodo padre.
- El nodo padre soporta estado indeterminado (parcial) cuando hay selección mixta en descendientes.
- En el árbol se muestra icono de carpeta y nombre de carpeta.
- Al hacer clic en una carpeta, el panel derecho muestra los archivos asociados de esa carpeta y sus subcarpetas.

## FR-18 Paginación configurable de imágenes

La UI debe paginar el contenido de archivos mostrado en el panel derecho.

### Criterios de aceptación

- El número de imágenes por página es configurable por usuario.
- Valores permitidos en MVP: 20, 50, 100 y Todas.
- La opción "Todas" desactiva la paginación numérica; las miniaturas se cargan bajo demanda (lazy loading) conforme entran en viewport.
- El sistema conserva el filtro/orden actual al cambiar de página.
- La UI solicita al backend únicamente la página activa (o batch de lazy loading).
- Debe soportar bibliotecas grandes sin cargar todas las imágenes en memoria de una sola vez.
- Orden por defecto en MVP: nombre.
- El usuario puede cambiar el criterio de ordenación.

## FR-19 Chat conversacional con MCP

La UI debe incluir un panel de chat conversacional que permita ejecutar acciones del sistema mediante lenguaje natural.

### Criterios de aceptación

- Panel de chat en el lado derecho de la interfaz (15% ancho).
- El usuario escribe mensajes en lenguaje natural y el sistema responde.
- El backend utiliza un LLM local (Ollama) para interpretar los mensajes. Backend code tasks MUST use model `qwen2.5-coder:14b`.
- El LLM tiene acceso a las MCP tools como herramientas (tool calling).
- El chat refleja el resultado de las operaciones en lenguaje natural.
- Las operaciones ejecutadas desde el chat tienen efecto en los demás paneles de la UI.
- Indicador visual de "escribiendo..." mientras el LLM procesa.
- Historial de conversación visible en el panel.

## FR-20 Formato de presentación de fechas

El sistema debe presentar todas las fechas a los usuarios en formato `dd/MM/yyyy` de forma consistente en toda la interfaz.

### Criterios de aceptación

- Todas las fechas mostradas en la UI (metadatos, evidencias, sugerencias) están en formato `dd/MM/yyyy`.
- Las fechas de corrección (sugerencias) se renderizam en **negrita** (`<strong>`) en el panel de detalle para destacarlas.
- Las heurísticas generan evidencias con fechas formateadas como `dd/MM/yyyy` envueltas en tags HTML `<strong>`.
- El frontend Blazor renderiza estas descripciones como `MarkupString` para mostrar correctamente las etiquetas HTML.
- La presentación es consistente en toda la aplicación (grid, detalle, listados).
