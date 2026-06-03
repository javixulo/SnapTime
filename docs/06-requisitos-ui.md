# SnapTime - Requisitos de UI

## 1) Decisión tecnológica
La UI será una aplicación Blazor WebAssembly ejecutándose en localhost.
Se comunica con el backend vía API REST.

## 2) Objetivo funcional de la UI
La UI debe permitir al usuario operar todo el flujo de análisis y revisión de forma clara, rápida y controlada.

## 3) Estructura visual obligatoria
- Panel izquierdo con estructura de carpetas en árbol estilo Windows.
- Panel derecho para MVP en modo grid de miniaturas.
- Panel superior con estado del proceso, métricas básicas y botones de control.
- Ventana flotante de configuración, abierta desde un botón del panel superior.
- Subpanel informativo en el panel derecho (zona inferior) con metainformación de la carpeta seleccionada.

## 4) Requisitos funcionales de UI
- Selección de ruta raíz de biblioteca.
- Controles de ejecución: iniciar, pausar, reanudar y cancelar.
- Botón para abrir configuración.
- Vista de progreso del job con estado y métricas básicas.
- Listado de fotos con filtros, ordenación y paginación.
- Tamaño de página configurable por usuario (número de imágenes por página).
- Valores permitidos de paginación en MVP: 20, 50, 100 y Todas.
- Orden por defecto en MVP: nombre.
- Cambio de orden habilitado por usuario.
- Pantalla de configuración para umbral, pesos y activación/desactivación de heurísticas.
- Aplicación de cambios en modo simulación y modo real con confirmación explícita.

## 5) Requisitos del árbol de carpetas
- Árbol de carpetas con checkboxes en cascada (seleccionar/deseleccionar).
- Selección en cascada descendente y actualización consistente de estado en nodos padre.
- Los nodos padre usarán estado visual de tres valores: seleccionado, no seleccionado e indeterminado (parcial).
- En el árbol se muestra, para cada carpeta, icono de carpeta y nombre.
- Al hacer clic en una carpeta, el panel derecho muestra miniaturas de las fotos contenidas en esa carpeta y sus subcarpetas.
- La carga de miniaturas debe ser bajo demanda (lazy loading).

## 6) Requisitos de rendimiento en listados grandes
- El cliente nunca debe cargar toda la biblioteca en memoria de una sola vez.
- Paginación obligatoria en el panel de miniaturas/listado.
- Renderizado virtualizado para evitar bloqueos de UI en colecciones grandes.
- Consultas del backend optimizadas para filtros y ordenación sobre grandes volúmenes.

## 7) Requisitos de experiencia de usuario
- Los estados del sistema deben ser inequívocos (en curso, pausado, cancelado, finalizado, error).
- Las acciones potencialmente destructivas deben requerir confirmación.
- Los mensajes de error deben indicar causa y siguiente acción recomendada.
- La UI debe ser usable con bibliotecas grandes sin bloquear la interacción.
- En MVP, el panel derecho es solo de visualización (sin edición ni acciones masivas desde miniaturas).

## 8) Temas abiertos para discusión
- Definir nivel de detalle visual inicial (MVP vs panel avanzado).
- Priorizar pantallas para primera entrega.
- Ubicación final del subpanel de metainformación de carpeta si cambia respecto a la zona inferior del panel derecho.
