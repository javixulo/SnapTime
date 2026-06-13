# F5 — Grid de fotos (panel central)

> Panel central (60% ancho) con grid de miniaturas del contenido de la carpeta seleccionada en el árbol izquierdo. Navegación por doble clic en subcarpetas, independiente del árbol.

**Referencias:** FR-08, FR-18

**Dependencias:** F0 (API), F1 (fotos en SQLite), F4 (árbol de carpetas + selección)

---

## F5-US-001 — Grid de fotos con navegación ✅ COMPLETADO

**Estado actual:** Componentes y API implementados. Pendiente verificar que los tests (bUnit + E2E) pasan.

**Reglas base:**

### Disposición
- Grid CSS con `auto-fill` y columnas de 180px de ancho mínimo.
- Cada celda: contenedor cuadrado de **180×180px**.
- La imagen se escala con `object-fit: contain` (proporcional, sin recortar), centrada.
- Debajo de cada miniatura: **nombre del archivo** (truncado si muy largo, max 20 chars).
- **Tooltip** con el nombre completo al hacer hover sobre la miniatura.
- Los vídeos se muestran con su primer frame como thumbnail mediante `<video preload="metadata" muted playsinline>`. En la **esquina inferior derecha**, badge ▶️.

### Subcarpetas
- Las subcarpetas aparecen **primero** en el grid, con icono de carpeta.
- **Doble clic** en una subcarpeta → navega dentro (cambia el `currentPath` del grid).
- La navegación es **independiente del árbol izquierdo** — el árbol mantiene su selección.
- El grid expone una **miga de pan (breadcrumb)** para navegar hacia arriba.

### Círculo de estado (MediaStatus)
- Esquina superior derecha de cada miniatura, círculo pequeño (16px diámetro).
- **Gris** (`Pending`), **Verde** (`Correct`), **Rojo** (`Error`), **Amarillo** (`NoSuggestion`), **Azul** (`HasSuggestion`).

### Indicador de recuento
- "N imágenes, M vídeos" alineado a la derecha en la barra de breadcrumb.

### Carga de datos
- Carga manual en `OnParametersSetAsync`: pide todos los items (`pageSize = int.MaxValue`) al cambiar de carpeta.
- `CancellationTokenSource` cancela peticiones previas al navegar rápido.
- Sin `Virtualize`. Scroll nativo en `.photo-grid-items` (`overflow-y: auto`).
- Si no hay carpeta seleccionada en el árbol, el grid se muestra vacío.

### Interacción
- **Click en miniatura** → F6 muestra detalle en subpanel derecho.
- **Sin checkboxes** (no selección múltiple por ahora).
- **Sin filtros** por ahora (mejora futura, fuera de MVP).

### Contrato API
- `GET /api/photos?path={ruta}&page={n}&pageSize={n}` → `PhotoGridResponse`
- `GET /api/thumbnails/{assetId}` → binario del archivo
- `GET /api/thumbnails/from-file?path={ruta}` → sirve cualquier archivo sin BD
- `GET /api/thumbnails/placeholder` → placeholder gris
- `GET /api/video/stream?path={ruta}` → stream de vídeo con Content-Type correcto

### Implementación
- **Frontend:** `PhotoGrid.razor` con CSS Grid, breadcrumb, subcarpetas, status circles, video badge, tooltip, file count, navegación interna independiente.
- **Backend:** `GET /api/photos` con paginación, cross-reference con BD para assets escaneados, orden directorios + alfabético.

### Tareas pendientes
- Verificar que los tests bUnit (PhotoGridTests.cs) pasan.
- Verificar que los tests E2E (PhotoGridE2ETests.cs) pasan.
