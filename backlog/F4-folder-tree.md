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

**Datos de prueba:** Existe `sample/` en la raíz del proyecto con fotos y vídeos de prueba (ficheros mínimos con extensión real). Si el usuario no ha elegido una carpeta, `sample/` se usa como raíz por defecto.

---

## F4-US-000 — Panel superior + escaneo de carpeta

> Input de ruta, botón "Escanear", indicador de progreso. Sin esto no hay datos que mostrar en el árbol.

**Tareas:**

- **🔴 T-001** — Tests (Janus):
  - bUnit: `ScanPanel.razor` renderiza input + botón + no empieza scan sin ruta válida
  - Playwright: flujo escribir ruta + click Escanear → llama a POST /api/jobs

- **🟢 T-002** — Implementación (Kip):
  - Componente `ScanPanel.razor` en la zona superior del layout
  - Input de texto + botón "Escanear"
  - Al hacer clic: POST `/api/jobs` con `{ rootPath }` y muestra JobId + estado (polling GET `/api/jobs/{id}`)
  - Si el input está vacío, usa `sample/` como ruta por defecto

- **🔵 T-003** — Refactor (Kip)
- **👁 T-004** — Review (Gavin)

**Datos de prueba:**
- Carpeta `sample/` en raíz del proyecto con 25+ ficheros variados

---

## F4-US-001 — API GET /folders/tree

> Endpoint que devuelve la estructura de carpetas de la ruta escaneada con recuento de archivos por nodo.

**Tareas:**

- **🔴 T-001** — Tests (Janus):
  - Integration test con SQLite real: insertar MediaAssets con varias rutas, llamar a `GET /folders/tree?scanJobId=...`, verificar árbol devuelto

- **🟢 T-002** — Implementación (Kip):
  - `IFolderService` en `Domain/Interfaces/`
  - `FolderService` en `Infrastructure/Services/` que agrupa MediaAssets por directorio padre y cuenta archivos
  - DTO `FolderTreeNode` con `Name, Path, FileCount, Children`
  - Endpoint `GET /folders/tree?scanJobId={id}` en Server
  - Si no se pasa `scanJobId`, devuelve la estructura de la última ejecución completada

  **Formato DTO:**
  ```json
  {
    "name": "sample",
    "path": "/abs/path/to/sample",
    "fileCount": 9,
    "children": [
      { "name": "vacation", "path": "...", "fileCount": 5, "children": [] },
      { "name": "family", "path": "...", "fileCount": 4, "children": [] }
    ]
  }
  ```

- **🔵 T-003** — Refactor (Kip)
- **👁 T-004** — Review (Gavin)

---

## F4-US-002 — FolderTree.razor básico

> Componente Blazor que renderiza el árbol expandible/colapsable.

**Tareas:**

- **🔴 T-001** — Tests (Janus):
  - bUnit: renderizar FolderTree con datos mock, verificar nodos visibles
  - bUnit: hacer clic en nodo → expande hijos
  - bUnit: hacer clic en nodo expandido → colapsa hijos

- **🟢 T-002** — Implementación (Kip):
  - `FolderTree.razor` componente recursivo
  - Cada nodo: icono carpeta + nombre + recuento
  - Click en nodo → toggle expand/colapsar
  - Props: `FolderTreeNode Data`, `bool Expanded`
  - Callback: `EventCallback<FolderTreeNode> OnFolderSelected`

- **🔵 T-003** — Refactor (Kip)
- **👁 T-004** — Review (Gavin)

---

## F4-US-003 — Checkbox en cascada

> Checkbox por nodo con 3 estados y sincronía padre↔hijo.

**Tareas:**

- **🔴 T-001** — Tests (Janus):
  - bUnit: checkbox padre → todos los hijos se checkean
  - bUnit: descheckear padre → todos los hijos se descheckean
  - bUnit: checkear algunos hijos → padre pasa a partial
  - bUnit: checkear todos los hijos → padre pasa a selected

- **🟢 T-002** — Implementación (Kip):
  - Estado `CheckState` por nodo: `Selected | None | Partial`
  - Click en checkbox padre: si está Selected → todos a None; si está None o Partial → todos a Selected
  - Cambio en hijo: recalcular estado del padre (si todos selected → Selected, si ninguno → None, si mixto → Partial)
  - POST `/folders/selection` envía paths seleccionados al backend (opcional, para F5)

- **🔵 T-003** — Refactor (Kip)
- **👁 T-004** — Review (Gavin)

---

## F4-US-004 — Integración FolderTree + API real

> Conectar FolderTree al endpoint real y usar `sample/` por defecto.

**Tareas:**

- **🔴 T-001** — Tests (Janus):
  - bUnit: FolderTree recibe datos desde un HttpClient mock
  - Playwright: escanear sample/, verificar que el árbol se puebla con las subcarpetas

- **🟢 T-002** — Implementación (Kip):
  - `IFolderClient` en el cliente WASM (servicio HTTP)
  - `FolderTree.razor` obtiene datos de `GET /folders/tree` al cargar
  - Si hay un scan en curso, polling hasta que termine, luego cargar árbol
  - Ruta por defecto: `sample/` (path absoluto resuelto desde el servidor)

- **🔵 T-003** — Refactor (Kip)
- **👁 T-004** — Review (Gavin)
