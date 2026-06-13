# SnapTime — Backlog

## Formato de IDs

- **Features**: `F0`, `F1`, ..., `F9`
- **User Stories**: `F0-US-001`, `F0-US-002`, ...
- **Comentarios en código**: `// [F0-US-001]` al inicio del bloque implementado

## Features

| ID | Nombre | Estado | Docs relacionadas |
|----|--------|--------|-------------------|
| F0 | Scaffolding del proyecto + UI testing infra | ✅ Completado | |
| F1 | Escaneo y extracción de metadatos (EXIF + QuickTime) | ✅ Completado | FR-01, FR-02, FR-03, FR-09 |
| F2 | SQLite real en tests de integración | ✅ Completado | |
| F3 | Motor de heurísticas (H-001 a H-006) | 🟡 En curso (H-006 + pipeline integrados, H-001..005 pendientes) | docs/05-heuristicas.md |
| F4 | Árbol de carpetas (panel izquierdo) | 🟡 En curso (componentes y API implementados, verificación pendiente) | FR-17 |
| F5 | Grid de fotos (panel central) | 🟡 En curso (componentes y API implementados, verificación pendiente) | FR-08, FR-18 |
| F6 | Panel detalle (foto seleccionada) | 🟡 En curso (componentes y API implementados, E2E tests pendientes) | FR-08 |
| F7 | Revisión en lote | 🟡 En curso (componentes, API y motor implementados) | FR-10 |
| F8 | Aplicación de cambios (escritura real, batch) | ⏳ Pendiente | FR-11 |
| F9 | Chat contextual con LLM (Ollama) | ⏳ Pendiente | FR-19 |

## Cómo trabajamos

1. Cada feature se desglosa en User Stories antes de empezar a codificar.
2. Las US se mueven de `🔴 Pendiente` → `🟡 En curso` → `🟢 Completada`.
3. Todo el código lleva comentario `// [ID]` apuntando a la US que lo origina.
4. No se salta de feature sin terminar la anterior (salvo decisión explícita).
