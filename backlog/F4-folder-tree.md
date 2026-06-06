# F4 — Árbol de carpetas (panel izquierdo)

> Panel izquierdo (25% ancho) con treeview de carpetas, checkboxes con selección en cascada, y recuento de fotos por nodo.

**Referencias:** FR-17, docs/06-ui.md

**Dependencias:** F0 (API base + F0-US-010 UI testing infra), F1 (datos de scan en SQLite)

**Reglas base:**
- Treeview con expand/colapsar por carpeta
- Checkbox por nodo con 3 estados: selected / none / partial (hijos mixtos)
- Seleccionar padre → selecciona todos los hijos
- Deseleccionar padre → deselecciona todos los hijos
- Seleccionar un hijo → padre pasa a partial (si no todos seleccionados)
- Muestra recuento de fotos junto al nombre: `Fotos (42)`
- Al hacer clic en una carpeta → filtra el grid (F5) a esa carpeta + subcarpetas
- Endpoint `GET /folders/tree` y `POST /folders/selection`

**Contrato (pendiente de desglosar en US):**
- Componente Blazor `FolderTree.razor`
- Servicio `IFolderService` en Server
- FolderRepository en Infrastructure
- Tests del árbol con datos mock
