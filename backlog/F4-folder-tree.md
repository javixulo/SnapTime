# F4 — Árbol de carpetas (panel izquierdo)

> Panel izquierdo (25% ancho) con treeview del sistema de archivos completo, expandible/colapsable, selección de una carpeta y opción "Incluir subcarpetas".

**Referencias:** FR-17, docs/06-ui.md §9

**Dependencias:** F0 (API base + UI testing infra), F1 (datos de scan en SQLite)

**Reglas base:**
- Treeview con expand/colapsar por carpeta
- Indentado por nivel: 6px (código) / 24px (docs)
- Muestra el sistema de archivos completo desde las raíces (endpoint `GET /api/filesystem/directories`)
- Al hacer clic en el nombre de una carpeta → se resalta como seleccionada (solo una a la vez)
- Toggle "Incluir subcarpetas" que indica si el escaneo debe recorrer también las subcarpetas
- El botón "Escanear" se implementa en **F7** y usa la carpeta seleccionada + el flag de subcarpetas
- Si no hay carpeta seleccionada, se usa `sample/` por defecto

---

## F4-US-001 — Seleccionar carpeta del árbol ✅ COMPLETADO

> Al hacer clic en una carpeta del árbol del sistema de archivos, se resalta como seleccionada. Un toggle "Incluir subcarpetas" controla si el escaneo debe ser recursivo. El botón "Escanear" se implementa en F7 y consume estos valores.

**Estado actual:** Componentes, API y todos los tests (bUnit + E2E) implementados y verificados.

**Dependencias:** Árbol del sistema de archivos ya funcionando (FolderTreePanel + FolderTreeItem)

**Referencias:** docs/06-requisitos-ui.md §4.1, §9

### Comportamiento

1. **Selección única por click:** el usuario hace clic en el nombre de cualquier carpeta del árbol → esa carpeta se resalta visualmente (background highlight). Solo una carpeta seleccionada a la vez. Si se hace clic en otra carpeta, la selección anterior se desmarca.

2. **Toggle "Incluir subcarpetas":** checkbox debajo del árbol que indica si el scan debe ser recursivo.
   - `true` (por defecto): se escanea la carpeta seleccionada y todas sus subcarpetas.
   - `false`: solo los archivos directamente dentro de la carpeta seleccionada.

3. **Sin input de ruta:** la selección se hace exclusivamente desde el árbol. No hay campo de texto para escribir rutas manualmente.

### Implementación

- **Frontend:** `FolderTreePanel.razor` con `SelectedPath`, `OnFolderSelected`, toggle "Incluir subcarpetas". `FolderTreeItem.razor` recursivo con expand/colapsar lazy, clase CSS `selected` cuando `Path == SelectedPath`.
- **Backend:** `GET /api/filesystem/directories?path=` retorna subdirectorios. `GET /api/filesystem/directories` (sin path) retorna raíces del sistema. Filtros de seguridad (ocultos, sistema, sin permisos). Soporta Windows (unidades) y macOS/Linux (raíz /).
- **ScanJob:** incluye `IncludeSubfolders` persistido en BD.
- **DirectoryWalker:** respeta flag `includeSubfolders` (recursivo o no).

