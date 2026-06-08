# F4 — Árbol de carpetas (panel izquierdo)

> Panel izquierdo (25% ancho) con treeview del sistema de archivos completo, expandible/colapsable, selección de una carpeta y opción "Incluir subcarpetas".

**Referencias:** FR-17, docs/06-ui.md §9

**Dependencias:** F0 (API base + F0-US-010 UI testing infra), F1 (datos de scan en SQLite)

**Reglas base:**
- Treeview con expand/colapsar por carpeta
- Muestra el sistema de archivos completo desde las raíces (endpoint `GET /api/filesystem/directories`)
- Al hacer clic en el nombre de una carpeta → se resalta como seleccionada (solo una a la vez)
- Toggle "Incluir subcarpetas" que indica si el escaneo debe recorrer también las subcarpetas
- El botón "Escanear" usa la carpeta seleccionada + el flag de subcarpetas
- Si no hay carpeta seleccionada, se usa `sample/` por defecto

---

## F4-US-001 — Seleccionar carpeta del árbol + escanear

> Al hacer clic en una carpeta del árbol del sistema de archivos, se resalta como seleccionada. Un toggle "Incluir subcarpetas" controla si el escaneo es recursivo. El botón "Escanear" usa estos valores.

**Dependencias:** Árbol del sistema de archivos ya funcionando (FolderTreePanel + FolderTreeItem), F4-US-000 (botón Escanear)

**Referencias:** docs/06-requisitos-ui.md §4.1, §9

### Comportamiento

1. **Selección única por click:** el usuario hace clic en el nombre de cualquier carpeta del árbol → esa carpeta se resalta visualmente (background highlight). Solo una carpeta seleccionada a la vez. Si se hace clic en otra carpeta, la selección anterior se desmarca.

2. **Toggle "Incluir subcarpetas":** checkbox o switch debajo del árbol (o junto al botón Escanear) que indica si el scan debe ser recursivo.
   - `true` (por defecto): se escanea la carpeta seleccionada y todas sus subcarpetas.
   - `false`: solo los archivos directamente dentro de la carpeta seleccionada.

3. **Escanear:** el botón "Escanear" usa `{ rootPath: rutaSeleccionada, includeSubfolders: bool }`. Si no hay carpeta seleccionada, se usa `sample/` por defecto.

4. **Sin input de ruta:** la selección se hace exclusivamente desde el árbol. No hay campo de texto para escribir rutas manualmente.

### API

El endpoint `POST /api/jobs` se modifica para aceptar `includeSubfolders`:

```json
{
  "rootPath": "/Users/foo/fotos",
  "includeSubfolders": true
}
```

El servidor lo usará para decidir si recorrer solo el directorio raíz o también subdirectorios.

### Tareas

- **🔴 T-001** — Tests (Janus):
  - bUnit FolderTreePanel: hacer clic en nombre de carpeta → se resalta (clase CSS `selected`).
  - bUnit FolderTreePanel: hacer clic en otra carpeta → la anterior se desresalta.
  - bUnit ScanPanel: sin carpeta seleccionada → usa `sample/`.
  - bUnit ScanPanel: con carpeta seleccionada + includeSubfolders=true → POST con rootPath + true.
  - bUnit ScanPanel: con carpeta seleccionada + includeSubfolders=false → POST con rootPath + false.
  - Integration: `POST /api/jobs` con `{ rootPath, includeSubfolders }` → flag persistido correctamente.
  - E2E: seleccionar carpeta en el árbol → Escanear → job se crea con esa ruta.

- **🟢 T-002** — Backend (Kip):
  - Modificar `CreateJobRequest` para incluir `IncludeSubfolders`.
  - Modificar `ScanJob` domain entity para persistir el flag.
  - Modificar `IScanJobService.CreateJobAsync` para aceptar y almacenar el flag.
  - Modificar `DirectoryWalker` para respetar el flag (recursivo o no).
  - Generar migración EF si cambia el esquema.

- **🟢 T-003** — Frontend (Karris):
  - FolderTreePanel: añadir `SelectedPath` y callback `OnFolderSelected(string path)`.
  - FolderTreeItem: añadir parámetro `SelectedPath` y clase CSS `selected` cuando coincide.
  - FolderTreePanel: incluir toggle "Incluir subcarpetas" (checkbox, default true).
  - ScanPanel: eliminar input de ruta.
  - ScanPanel: suscribirse a FolderTreePanel.OnFolderSelected para conocer la ruta.
  - ScanPanel: leer el toggle de subcarpetas al hacer clic en Escanear.

- **🔵 T-004** — Refactor (Kip/Karris)
- **👁 T-005** — Review (Gavin)
