# SnapTime - Requisitos de UI

## 1) Decisión tecnológica
- La UI será una aplicación Blazor WebAssembly ejecutándose en localhost.
- Se comunica con el backend vía API REST.

## 2) Objetivo funcional de la UI
La UI debe permitir al usuario operar todo el flujo de análisis y revisión de forma clara, rápida y controlada.

## 3) Estructura visual del MVP
- **Pantalla única** con paneles (sin navegación entre páginas).
- **Panel superior**: estado del proceso, métricas básicas y botones de control. Ocupa todo el ancho.
- **Panel izquierdo** (25%): estructura de carpetas en árbol estilo Windows.
- **Panel central** (60%): grid de miniaturas con expandir inline para detalle. Subpanel informativo en zona inferior (número de archivos de la carpeta seleccionada).
- **Panel derecho** (15%): chat conversacional para ejecutar acciones vía MCP.
- **Ventana modal de configuración**, abierta desde un botón del panel superior.

## 4) Requisitos funcionales de UI

### 4.1) Selección de ruta raíz
- Input de texto para escribir o pegar la ruta.
- Botón "Examinar" junto al input (usando input file del SO).

### 4.2) Panel derecho: grid de miniaturas
- Cuadrícula de fotos con miniatura.
- Indicadores visuales sobre la miniatura (badge de score bajo/alto, icono de estado de revisión).
- Al hacer clic en una miniatura, se expande inline mostrando detalle con evidencias.
- **Solo visualización** en MVP. Sin acciones (aceptar/rechazar) desde el grid.

### 4.3) Vista de detalle
- Se abre expandiendo la miniatura inline dentro del grid.
- Muestra: ruta completa, fecha actual, score, sugerencia (si existe), desglose de evidencias (heurística aplicada, peso, dirección).

### 4.4) Panel superior
- Controles de ejecución: iniciar, pausar, reanudar y cancelar.
- Botón para abrir configuración (modal).
- Métricas: progreso de fotos procesadas + resumen de confianza (altas, revisar, sugerir corrección).
- Estados de proceso visibles: en curso, pausado, cancelado, finalizado, error.

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

### 4.7) Dry-run y aplicación
- Modal con lista de cambios: foto, fecha actual → fecha nueva para cada ítem.
- Botón "Aplicar" o "Cancelar".
- Requiere confirmación explícita.

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
- Árbol de carpetas con checkboxes en cascada (seleccionar/deseleccionar).
- Selección en cascada descendente y actualización consistente de estado en nodos padre.
- Los nodos padre usarán estado visual de tres valores: seleccionado, no seleccionado e indeterminado (parcial).
- En el árbol se muestra, para cada carpeta, icono de carpeta y nombre.
- Al hacer clic en una carpeta, el panel derecho muestra miniaturas de las fotos contenidas en esa carpeta y sus subcarpetas.
- La carga de miniaturas debe ser bajo demanda (lazy loading).

## 6) Requisitos de rendimiento en listados grandes
- El cliente nunca debe cargar toda la biblioteca en memoria de una sola vez.
- Paginación obligatoria (salvo opción "Todas", que usa lazy loading).
- Renderizado virtualizado para evitar bloqueos de UI en colecciones grandes.
- Consultas del backend optimizadas para filtros y ordenación sobre grandes volúmenes.

## 7) Requisitos de experiencia de usuario
- Los estados del sistema deben ser inequívocos (en curso, pausado, cancelado, finalizado, error).
- Las acciones potencialmente destructivas deben requerir confirmación.
- Los mensajes de error deben indicar causa y siguiente acción recomendada, mostrados en el propio panel.
- La UI debe ser usable con bibliotecas grandes sin bloquear la interacción.
- La UI tiene tamaño fijo con scroll si la ventana es más pequeña de lo esperado.
- Sin atajos de teclado en MVP.
- Estados visuales contemplados: carga (spinner), vacío (mensaje informativo con icono), error (mensaje en panel).

## 8) Decisiones de diseño

### Subpanel de metainformación de carpeta
- Ubicación definitiva: **zona inferior del panel central (grid)**, debajo de las miniaturas.
- Muestra: número de archivos de la carpeta seleccionada y resumen rápido de confianza (altas / revisar / sugerir corrección).
- Ocupa el ancho completo del panel central.
