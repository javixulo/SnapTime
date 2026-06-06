# SnapTime — Backlog

## Formato de IDs

- **Features**: `F0`, `F1`, ..., `F9`
- **User Stories**: `F0-US-001`, `F0-US-002`, ...
- **Comentarios en código**: `// [F0-US-001]` al inicio del bloque implementado

## Features

| ID | Nombre | Estado | Docs relacionadas |
|----|--------|--------|-------------------|
| F0 | Scaffolding del proyecto + UI testing infra | ✅ Completado (F0-US-010 pendiente) | |
| F1 | Escaneo y extracción de metadatos (EXIF + QuickTime) | ✅ Completado | FR-01, FR-02, FR-03, FR-09 |
| F2 | SQLite real en tests de integración | ✅ Completado | |
| F3 | Motor de heurísticas (H-001 a H-006) | 🟡 En curso (H-006 implementado, H-001..005 pendientes) | docs/05-heuristicas.md |
| F4 | Árbol de carpetas (panel izquierdo) | ⏳ Pendiente (bloqueado por F0-US-010) | FR-17 |
| F5 | Grid de fotos (panel central) | ⏳ Pendiente | FR-08, FR-18 |
| F6 | Subpanel de evidencia (detalle + scoring) | ⏳ Pendiente | FR-08 |
| F7 | Revisión en lote | ⏳ Pendiente | FR-10 |
| F8 | Aplicación de cambios (dry-run + escritura) | ⏳ Pendiente | FR-11 |
| F9 | Chat contextual con LLM (Ollama) | ⏳ Pendiente | FR-19 |

## Cómo trabajamos

1. Cada feature se desglosa en User Stories antes de empezar a codificar.
2. Las US se mueven de `🔴 Pendiente` → `🟡 En curso` → `🟢 Completada`.
3. Todo el código lleva comentario `// [ID]` apuntando a la US que lo origina.
4. No se salta de feature sin terminar la anterior (salvo decisión explícita).
