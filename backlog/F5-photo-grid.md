# F5 — Grid de fotos (panel central)

> Panel central (60% ancho) con grid de miniaturas del contenido de la carpeta seleccionada en el árbol izquierdo. Navegación por doble clic en subcarpetas, independiente del árbol.

**Referencias:** FR-08, FR-18

**Dependencias:** F0 (API), F1 (fotos en SQLite), F4 (árbol de carpetas + selección)

**Reglas base:**

### Disposición
- Grid CSS con `auto-fill` y columnas de 180px de ancho mínimo.
- Cada celda: contenedor cuadrado de **180×180px**.
- La imagen se escala con `object-fit: contain` (proporcional, sin recortar), centrada.
- Debajo de cada miniatura: **nombre del archivo** (truncado si muy largo).
- **Tooltip** con el nombre completo al hacer hover sobre la miniatura.
- Los vídeos se muestran con su thumbnail. Sin overlay central. En la **esquina inferior derecha**, un círculo pequeño con icono **▶️** para indicar que es vídeo.

### Subcarpetas
- Las subcarpetas aparecen **primero** en el grid, con icono de carpeta.
- **Doble clic** en una subcarpeta → navega dentro (cambia el `currentPath` del grid).
- La navegación es **independiente del árbol izquierdo** — el árbol mantiene su selección.
- El grid expone una **miga de pan (breadcrumb)** para navegar hacia arriba.

### Círculo de estado
- Esquina superior derecha de cada miniatura, círculo pequeño (8px diámetro aprox).
- **Gris** — no escaneado / pendiente.
- **Verde** — escaneado y fecha correcta.
- **Rojo** — escaneado con error.
- **Amarillo** — escaneado, sin sugerencia de mejora.
- **Azul** — escaneado, hay sugerencia de mejor fecha.

### Carga de datos
- `Virtualize` con `ItemsProvider` que pide lotes de 50 vía `GET /api/photos`.
- Parámetros: `path`, `page`, `pageSize`.
- Sin paginación visible — scroll infinito gestionado por Virtualize.
- Si no hay carpeta seleccionada en el árbol, el grid se muestra vacío.

### Interacción
- **Click en miniatura** → F6 muestra detalle en subpanel derecho.
- **Sin checkboxes** (no selección múltiple por ahora).
- **Sin filtros** por ahora (mejora futura, fuera de MVP).

### Contrato API
- `GET /api/photos?path={ruta}&page={n}&pageSize={n}` → `{ items: PhotoGridItem[], totalCount: int, page: int }`
- `PhotoGridItem { name, path, isDirectory, thumbnailUrl?, mediaStatus, hasSuggestion, dateStatus }`
- `GET /api/thumbnails/{assetId}` → binario de la miniatura (caché en disco).

### Tareas propuestas
- **🔴 T-001** — Tests (Janus): bUnit PhotoGrid (subcarpetas primero, doble clic navega, Virtualize carga lotes, breadcrumb, círculo estado, click miniatura). E2E Playwright (seleccionar carpeta → grid poblado, doble clic subcarpeta, breadcrumb).
- **🟢 T-002** — Backend (Kip): Endpoint `GET /api/photos` con paginación + `GET /api/thumbnails/{assetId}`. Endpoint `GET /api/photos/directory` para listar contenido del sistema de archivos (subcarpetas + archivos). Servicio `IPhotoService`.
- **🟢 T-003** — Frontend (Karris): `PhotoGrid.razor` con Virtualize, breadcrumb, subcarpetas click, círculo estado, overlay vídeo. Foto vacía/loading/error states.
- **🔵 T-004** — Refactor
- **👁 T-005** — Review (Gavin)
