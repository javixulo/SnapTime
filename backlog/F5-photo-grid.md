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

### Círculo de estado (MediaStatus)
- Esquina superior derecha de cada miniatura, círculo pequeño (16px diámetro). Representa el `MediaStatus` del archivo.
- **Gris** (`Pending`) — no escaneado / pendiente.
- **Verde** (`Correct`) — escaneado y fecha correcta.
- **Rojo** (`Error`) — escaneado con error.
- **Amarillo** (`NoSuggestion`) — escaneado, sin sugerencia de mejora.
- **Azul** (`HasSuggestion`) — escaneado, hay sugerencia de mejor fecha.
- El estado de revisión de la sugerencia (`SuggestionReviewStatus: Unreviewed/Approved/Rejected`) no se refleja en el círculo del grid en MVP. Se muestra en el panel de detalle.

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
- **🔴 T-001** — Tests (Janus): bUnit PhotoGrid (subcarpetas primero, doble clic navega, breadcrumb, círculo estado, click miniatura). E2E Playwright — los 3 tests existentes en `PhotoGridE2ETests.cs` más los siguientes casos adicionales:

  1. (✅ existente) Seleccionar carpeta → grid carga items (`PhotoGrid_SelectFolderInTree_LoadsGridWithItems`).
  2. (✅ existente) Doble click subcarpeta → navega dentro (`PhotoGrid_DoubleClickSubfolder_NavigatesInside`).
  3. (✅ existente) Click breadcrumb → navega arriba (`PhotoGrid_Breadcrumb_ClickNavigatesUp`).
  4. **Carpeta vacía → mensaje "Esta carpeta no contiene fotos".**
      - Assert: `.photo-grid-empty` visible con texto informativo.
  5. **Círculo de estado visible en miniaturas.**
      - Assert: `.photo-grid-status-circle` presente en cada item.
  6. **Vídeo muestra badge ▶.**
      - Assert: si hay vídeos, `.photo-grid-play-badge` visible en ellos.
  7. **Click thumbnail → emite selección (F6).**
      - Assert: al clickar miniatura, se abre detalle en panel derecho (`.photo-detail-name` visible).
- **🟢 T-002** — Backend (Kip): Endpoint `GET /api/photos` con paginación + `GET /api/thumbnails/{assetId}`. Endpoint `GET /api/photos/directory` para listar contenido del sistema de archivos (subcarpetas + archivos). Servicio `IPhotoService`.
- **🟢 T-003** — Frontend (Karris): `PhotoGrid.razor` con CSS Grid, breadcrumb, subcarpetas click, círculo estado, overlay vídeo. Carga manual (sin Virtualize para evitar flickering con CSS Grid). Foto vacía/loading/error states.
- **🔵 T-004** — Refactor
- **👁 T-005** — Review (Gavin)
