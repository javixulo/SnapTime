# SnapTime - Arquitectura del sistema

## 1) Arquitectura lógica de módulos

```
┌─────────────────────────────────────────────────────────────┐
│                     Blazor WASM (UI)                         │
│  ┌──────────┐  ┌──────────────┐  ┌──────────────────────┐   │
│  │ Árbol     │  │ Grid         │  │ Chat MCP             │   │
│  │ Carpetas  │  │ Miniaturas   │  │ (Ollama + tools)     │   │
│  │ (25%)     │  │ (60%)        │  │ (15%)                │   │
│  └──────────┘  └──────────────┘  └──────────────────────┘   │
└─────────────────────────┬───────────────────────────────────┘
                          │ HTTP REST
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                    API Backend (C#/.NET)                     │
│  ┌──────────┐  ┌───────────┐  ┌──────────────────────────┐  │
│  │ MCP      │  │ Chat      │  │ Control de jobs          │  │
│  │ Server   │  │ Backend   │  │ (crear, pausar, cancelar)│  │
│  └────┬─────┘  └─────┬─────┘  └──────────────┬───────────┘  │
│       └──────────────┼───────────────────────┘              │
│                      ▼                                      │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              Core de Dominio                          │   │
│  │  • Heurísticas (H-001 a H-006)                       │   │
│  │  • Motor de scoring y sugerencias                    │   │
│  │  • Reglas de decisión                                │   │
│  └──────────────────────────┬───────────────────────────┘   │
│                             │                                │
│  ┌──────────────────────────▼───────────────────────────┐   │
│  │              Infraestructura                          │   │
│  │  • Adaptador EXIF (lectura/escritura metadatos)      │   │
│  │  • Filesystem (escaneo, thumbnails)                  │   │
│  │  • EF Core DbContext + SQLite                        │   │
│  │  • ConfigService (JSON)                              │   │
│  │  • Serilog (logging estructurado)                    │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         Worker de procesamiento                       │   │
│  │  • Escaneo/análisis asíncrono                         │   │
│  │  • Cancelación cooperativa + checkpoints              │   │
│  │  • Pipeline batch por lotes                           │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

### 1.1) Descripción de módulos

| Módulo | Tecnología | Propósito |
|--------|-----------|-----------|
| UI | Blazor WebAssembly | Interfaz de usuario en 3 paneles. Sin tooling JS. |
| API Backend | C#/.NET | Endpoints REST para UI y control de jobs. |
| MCP Server | C#/.NET | Tools para agentes IA y chat conversacional. Comparte core con API. |
| Chat Backend | C#/.NET | Recibe mensajes del chat, consulta Ollama con tool calling, devuelve respuesta. |
| Core de Dominio | C#/.NET | Heurísticas, scoring, sugerencias, reglas de decisión. Framework-agnóstico. |
| Infraestructura | C#/.NET | Adaptadores EXIF, filesystem, EF Core + SQLite, ConfigService, Serilog. |
| Worker | C#/.NET | Procesamiento asíncrono con checkpoints y cancelación. |

### 1.2) Decisiones tecnológicas por módulo

#### Backend
- Lenguaje obligatorio: C#. Plataforma: .NET.
- Política de versión: última estable disponible en cada iteración mayor.
- Preferencia actual: .NET 10. Fallback: .NET 8 LTS si hay impedimento técnico.

#### UI
- Framework: Blazor WebAssembly. Sin tooling JS (npm, webpack).
- Alternativas descartadas: React + API REST (duplica toolchain), Vue (misma desventaja), Angular (excesivo).
- Consecuencias: mismo lenguaje en frontend y backend; modelos y DTOs compartibles vía proyecto shared; mayor tamaño de descarga inicial.

#### Persistencia
- Motor: SQLite. ORM: EF Core con enfoque code-first (POCO classes en C#).
- Las POCOs en `SnapTime.Domain` son la fuente de verdad del esquema. EF Core genera migrations automáticamente.
- Durante el desarrollo se permite modificar POCOs libremente. No se mantienen archivos SQL manuales.
- Alternativas descartadas: Dapper + SQL manual (bloqueante), ADO.NET raw (sin migraciones), LiteDB (NoSQL, fuera del estándar).

#### Chat conversacional
- LLM local: Ollama. Sin envío de datos a internet.
- Modelo configurable en `snaptime.config.json` (default: `llama3.2`).
- El chat backend envía el mensaje al LLM con tool calling sobre las MCP tools.
- El LLM interpreta el mensaje, ejecuta la tool correspondiente y devuelve la respuesta formateada.

#### Configuración
- Archivo JSON: `snaptime.config.json` (ver `docs/08-configuracion.md`).
- ConfigService singleton con FileSystemWatcher para detección de cambios externos.
- Validación de valores antes de aplicar. Cada cambio se registra en auditoría.

#### Logging
- Librería: Serilog. Integración con `Microsoft.Extensions.Logging`.
- Logging estructurado como estándar. Nivel configurable en `snaptime.config.json`.

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

### API REST (para UI)
| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/jobs` | Crear job de análisis |
| POST | `/jobs/{id}/pause` | Pausar job |
| POST | `/jobs/{id}/resume` | Reanudar job |
| POST | `/jobs/{id}/cancel` | Cancelar job |
| GET | `/jobs/{id}` | Estado y progreso |
| GET | `/folders/tree` | Árbol de carpetas con estado de selección |
| POST | `/folders/selection` | Actualizar selección en cascada |
| GET | `/photos` | Listado paginado con filtros |
| GET | `/photos/{id}` | Detalle con evidencia |
| GET | `/thumbnails/{photoId}` | Miniatura bajo demanda |
| POST | `/reviews/batch` | Aprobar/rechazar en lote |
| POST | `/apply` | Ejecutar dry-run o aplicación real |

### MCP tools (para agentes)
| Tool | Descripción |
|------|-------------|
| `scan_library(root_path, options)` | Iniciar análisis de biblioteca |
| `list_low_confidence(threshold, limit, filters)` | Listar fotos con baja confianza |
| `get_photo_evidence(photo_id)` | Obtener evidencias de una foto |
| `suggest_date(photo_id)` | Pedir sugerencia de fecha |
| `apply_fix(photo_id, mode, confirm_token)` | Aplicar cambio (dry_run o commit) |

## 4) Reglas de decisión iniciales (baseline)

### 4.1) Campo canónico de fecha de captura
- `SubSecDateTimeOriginal` > `DateTimeOriginal` es el campo canónico. Siempre es la fuente de verdad para lecturas, comparaciones y escrituras.
- Al escribir, se fija hora 5:00 AM en todas las correcciones automáticas.
- Referencia: `docs/00-vision-y-alcance.md §8`.

### 4.2) Reglas baseline
- Penalizar inconsistencias severas entre fecha principal y fechas secundarias.
- Comparar contra tendencia temporal de carpeta/lote.
- Tratar pistas de nombre de carpeta/archivo como evidencia blanda.
- Penalizar paradojas temporales obvias (ej: `mtime` mucho menor que fecha propuesta).

## 5) Modelo de confianza
- Score [0-100] para "fecha actual correcta".
- Estados:
  - >= 80: alta confianza.
  - 50-79: revisar.
  - < 50: sugerir corrección.
- Cada decisión incluye desglose de señales a favor/en contra.

## 6) Plan por fases

### Fase 0 - Requisitos y diseño (actual)
- Cerrar FR/NFR y criterios de aceptación.
- Definir documentación de arquitectura.
- Definir estrategia de pruebas y dataset de evaluación.

### Fase 1 - MVP de análisis (solo lectura)
- Escaneo + extracción de metadatos + scoring baseline.
- Persistencia SQLite.
- UI mínima: árbol + grid miniaturas + detalle inline + chat MCP.

### Fase 2 - Revisión y sugerencias avanzadas
- Filtros avanzados, revisión por lotes, exportes.
- Mejoras de heurísticas de contexto.
- API/MCP estabilizados y versionados.

### Fase 3 - Aplicación controlada de cambios
- Dry-run formal.
- Aplicación real con confirmaciones fuertes.
- Auditoría completa y reportes post-operación.

## 7) Riesgos y mitigaciones
| Riesgo | Mitigación |
|--------|-----------|
| Falsos positivos en sugerencias | Umbrales conservadores + revisión humana |
| Jobs largos en bibliotecas enormes | Checkpoints + control de concurrencia |
| Reescrituras peligrosas | Dry-run por defecto + confirmación explícita |
| Complejidad creciente | Documentación como fuente de verdad + versionado de reglas |

## 8) Entregables de la siguiente iteración (sin código)
- Documento de casos de uso (priorizados).
- Especificación de datos (tablas SQLite + índices + estados).
- Matriz de tests de aceptación FR/NFR.
- Backlog inicial (epics → historias → tareas técnicas).
