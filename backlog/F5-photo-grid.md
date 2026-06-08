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
- Los vídeos se muestran con su primer frame como thumbnail mediante `<video preload="metadata" muted playsinline>` — el navegador lo renderiza sin instalar ffmpeg ni librerías externas. En la **esquina inferior derecha**, un círculo pequeño con icono **▶️** para indicar que es vídeo.

### Subcarpetas
- Las subcarpetas aparecen **primero** en el grid, con icono de carpeta.
- **Doble clic** en una subcarpeta → navega dentro (cambia el `currentPath` del grid).
- La navegación es **independiente del árbol izquierdo** — el árbol mantiene su selección.
- El grid expone una **miga de pan (breadcrumb)** para navegar hacia arriba.

### Círculo de estado
- Esquina superior derecha de cada miniatura, círculo pequeño (16px diámetro).
- **Gris** — no escaneado / pendiente.
- **Verde** — escaneado y fecha correcta.
- **Rojo** — escaneado con error.
- **Amarillo** — escaneado, sin sugerencia de mejora.
- **Azul** — escaneado, hay sugerencia de mejor fecha.

### Indicador de recuento
- Muestra el número de archivos de la carpeta seleccionada, desglosado por tipo: imagen y vídeo.
- Se sitúa en la barra de breadcrumb, alineado a la derecha (ej: "23 imágenes, 16 vídeos").

### Carga de datos
- Carga manual en `OnParametersSetAsync`: pide todos los items (`pageSize = int.MaxValue`) al cambiar de carpeta.
- `CancellationTokenSource` cancela peticiones previas al navegar rápido.
- Sin `Virtualize` (causaba parpadeos con CSS Grid). Scroll nativo en `.photo-grid-items` (`overflow-y: auto`).
- Si no hay carpeta seleccionada en el árbol, el grid se muestra vacío.

### Interacción
- **Click en miniatura** → F6 muestra detalle en subpanel derecho.
- **Sin checkboxes** (no selección múltiple por ahora).
- **Sin filtros** por ahora (mejora futura, fuera de MVP).

### Contrato API
- `GET /api/photos?path={ruta}&page={n}&pageSize={n}` → `{ items: PhotoGridItem[], totalCount: int, page: int }`
- `PhotoGridItem { name, path, isDirectory, thumbnailUrl?, mediaStatus, hasSuggestion }`
- `GET /api/thumbnails/{assetId}` → binario de la miniatura (sirve el archivo del disco desde un asset escaneado).
- `GET /api/thumbnails/from-file?path={ruta}` → sirve cualquier archivo del disco sin dependencia de BD.
- `GET /api/thumbnails/placeholder` → placeholder gris para directorios o formatos no soportados.
- `GET /api/video/stream?path={ruta}` → sirve el vídeo con `Content-Type` correcto (`video/mp4`, `video/quicktime`, etc.) para que el `<video>` del navegador muestre su primer frame como thumbnail.
- Las miniaturas NO dependen de la base de datos. Todos los archivos (escaneados o no) reciben un `thumbnailUrl`. Los vídeos usan `<video>` en vez de `<img>`.

### Tareas propuestas
- **🔴 T-001** — Tests (Janus): bUnit PhotoGrid (subcarpetas primero, doble clic navega, breadcrumb, círculo estado, click miniatura). E2E Playwright (seleccionar carpeta → grid poblado, doble clic subcarpeta, breadcrumb).
- **🟢 T-002** — Backend (Kip): Endpoint `GET /api/photos` con paginación + `GET /api/thumbnails/{assetId}`. Endpoint `GET /api/photos/directory` para listar contenido del sistema de archivos (subcarpetas + archivos). Servicio `IPhotoService`.
- **🟢 T-003** — Frontend (Karris): `PhotoGrid.razor` con CSS Grid, breadcrumb, subcarpetas click, círculo estado, overlay vídeo. Carga manual (sin Virtualize para evitar flickering con CSS Grid). Foto vacía/loading/error states.
- **🔵 T-004** — Refactor
- **👁 T-005** — Review (Gavin)
