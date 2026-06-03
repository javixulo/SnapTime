# SnapTime - Blueprint (flujo, módulos y fases)

## 1) Arquitectura lógica de módulos
- UI web (Blazor WASM en localhost): tres paneles (árbol carpetas 25%, grid miniaturas 60%, chat MCP 15%) para explorar resultados, ejecutar acciones y chatear.
- API Backend (C#/.NET): interfaz principal para la UI y control de jobs.
- MCP Server (C#/.NET): interfaz para agentes y para el chat conversacional, compartiendo core de negocio.
- Chat Backend (C#/.NET): endpoint que recibe mensajes del chat, los envía a un LLM local (Ollama) con tool calling sobre las MCP tools, y devuelve la respuesta.
- Core de Dominio: heurísticas, scoring, sugerencias y reglas.
- Infraestructura: adaptadores de EXIF, filesystem, DbContext de EF Core y SQLite.
- Worker de procesamiento: escaneo/análisis asíncrono con cancelación y checkpoints.

## 1.1) Restricción tecnológica de backend
- Todo el backend debe implementarse en C#/.NET.
- Objetivo de versión: última versión estable disponible en el momento de implementación.
- Preferencia actual: .NET 10 si está estable y soportado por el stack elegido.
- Fallback permitido: .NET 8 LTS cuando exista impedimento técnico justificado.

## 2) Flujo operativo end-to-end
1. Usuario selecciona ruta y parámetros (umbral, concurrencia, filtros).
2. Se crea un job de escaneo y se indexan archivos candidatos.
3. Se extraen metadatos y se normalizan fechas.
4. El motor de heurísticas calcula score y, si procede, sugerencia.
5. Resultados se persisten en SQLite con evidencia.
6. La UI muestra lista/filtros/detalle y permite revisión.
7. Usuario aprueba/rechaza cambios.
8. Sistema ejecuta dry-run o aplicación real y registra auditoría.

## 3) Contratos iniciales (sin implementación)

### API (para UI)
- POST /jobs: crear job de análisis.
- POST /jobs/{id}/pause: pausar job.
- POST /jobs/{id}/resume: reanudar job.
- POST /jobs/{id}/cancel: cancelar job.
- GET /jobs/{id}: estado y progreso.
- GET /folders/tree: árbol de carpetas con estado de selección.
- POST /folders/selection: actualizar selección en cascada.
- GET /photos: listado paginado con filtros.
- GET /photos/{id}: detalle con evidencia.
- GET /thumbnails/{photoId}: miniatura bajo demanda.
- POST /reviews/batch: aprobar/rechazar en lote.
- POST /apply: ejecutar dry-run o aplicación real.

### MCP tools (para agentes)
- scan_library(root_path, options)
- list_low_confidence(threshold, limit, filters)
- get_photo_evidence(photo_id)
- suggest_date(photo_id)
- apply_fix(photo_id, mode=dry_run|commit, confirm_token)

## 4) Reglas de decisión iniciales (baseline)
- `EXIF:DateTimeOriginal` (o `SubSecDateTimeOriginal`) es el campo canónico de fecha de captura. Es siempre la fuente de verdad para lecturas, comparaciones y escrituras. Ver doc 00 §8.
- Penalizar inconsistencias severas entre fecha principal y fechas secundarias.
- Comparar contra tendencia temporal de carpeta/lote.
- Tratar pistas de nombre de carpeta/archivo como evidencia blanda.
- Penalizar paradojas temporales obvias (ej. mtime mucho menor que fecha propuesta).

## 5) Modelo de confianza propuesto
- Score [0-100] para "fecha actual correcta".
- Estados sugeridos:
  - >= 80: alta confianza.
  - 50-79: revisar.
  - < 50: sugerir corrección.
- Cada decisión debe incluir señales principales a favor/en contra.

## 6) Plan por fases

### Fase 0 - Requisitos y diseño (actual)
- Cerrar FR/NFR y criterios de aceptación.
- Definir diccionario de datos y ADRs iniciales.
- Definir estrategia de pruebas y dataset de evaluación.
- Definir política de desarrollo TDD (solo unit tests) y reglas de ejecución.
- Confirmar versión objetivo de .NET para el arranque del desarrollo.

### Fase 1 - MVP de análisis (solo lectura)
- Escaneo + extracción de metadatos + scoring baseline.
- Persistencia SQLite.
- UI mínima de listado y detalle con evidencia.
- Sin escritura de metadatos en esta fase.

### Fase 2 - Revisión y sugerencias avanzadas
- Filtros avanzados, revisión por lotes, exportes.
- Mejoras de heurísticas de contexto.
- API/MCP estabilizados y versionados.

### Fase 3 - Aplicación controlada de cambios
- Dry-run formal.
- Aplicación real con confirmaciones fuertes.
- Auditoría completa y reportes post-operación.

## 7) Riesgos y mitigaciones
- Falsos positivos en sugerencias -> umbrales conservadores + revisión humana.
- Jobs largos en bibliotecas enormes -> checkpoints + control de concurrencia.
- Reescrituras peligrosas -> modo dry-run por defecto + confirmación explícita.
- Complejidad creciente -> ADRs y versionado de reglas.

## 8) Entregables de la siguiente iteración (sin código)
- Documento de casos de uso (priorizados).
- Especificación de datos (tablas SQLite + índices + estados).
- Matriz de tests de aceptación FR/NFR.
- Backlog inicial (epics -> historias -> tareas técnicas).
