# SnapTime — Backlog

## Formato de IDs

- **Features**: `F0`, `F1`, ..., `F8`
- **User Stories**: `F0-US-001`, `F0-US-002`, ...
- **Comentarios en código**: `// [F0-US-001]` al inicio del bloque implementado

## Features

| ID | Nombre | Estado | Docs relacionadas |
|----|--------|--------|-------------------|
| F0 | Scaffolding del proyecto | 🏗️ Backlog definido | |
| F1 | Escaneo y extracción de metadatos (EXIF + QuickTime) | 🏗️ Backlog definido | FR-01, FR-02, FR-03, FR-09 |
| F2 | Heurística H-006 (fecha desde filename) | ⏳ Pendiente | docs/05-heuristicas.md |
| F3 | Árbol de carpetas (panel izquierdo) | ⏳ Pendiente | FR-17 |
| F4 | Grid de fotos (panel central) | ⏳ Pendiente | FR-08, FR-18 |
| F5 | Subpanel de evidencia (detalle + scoring) | ⏳ Pendiente | FR-08 |
| F6 | Revisión en lote | ⏳ Pendiente | FR-10 |
| F7 | Aplicación de cambios (dry-run + escritura) | ⏳ Pendiente | FR-11 |
| F8 | Chat contextual con LLM (Ollama) | ⏳ Pendiente | FR-19 |

## Cómo trabajamos

1. Cada feature se desglosa en User Stories antes de empezar a codificar.
2. Las US se mueven de `🔴 Pendiente` → `🟡 En curso` → `🟢 Completada`.
3. Todo el código lleva comentario `// [ID]` apuntando a la US que lo origina.
4. No se salta de feature sin terminar la anterior (salvo decisión explícita).
