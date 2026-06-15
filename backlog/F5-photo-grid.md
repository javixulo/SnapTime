# F5 — Grid de fotos (panel central)

> Panel central (60% ancho) con grid de miniaturas del contenido de la carpeta seleccionada en el árbol izquierdo. Navegación por doble clic en subcarpetas, independiente del árbol.

**Referencias:** FR-08, FR-18

**Dependencias:** F0 (API), F1 (fotos en SQLite), F4 (árbol de carpetas + selección)

---

## F5-US-001 — Grid de fotos con navegación ✅ COMPLETADO

**Estado actual:** Componentes, API y todos los tests (bUnit + E2E) implementados y verificados.

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

### Círculo de estado
- Esquina superior derecha de cada miniatura, círculo pequeño (16px diámetro).
- **Gris** (`Pending`), **Verde** (`Correct`), **Rojo** (`Error`), **Amarillo** (`NoSuggestion`), **Azul claro** (`HasSuggestion`).
- Cuando una sugerencia se acepta (`SuggestionStatus = Approved`), el círculo cambia a **azul oscuro (#1565C0)** independientemente del MediaStatus.

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

---

## F5-US-002 — Círculo de estado refleja sugerencias aceptadas 🔴 Pendiente

> Cuando el usuario acepta una sugerencia (`SuggestionStatus = Approved`), el círculo de estado en el grid debe cambiar a azul oscuro (#1565C0), independientemente del `MediaStatus` del asset.

**Estado actual:** Pendiente de implementar.

### Cambios necesarios

#### Backend (Kip)
1. Añadir campo `SuggestionStatus` al record `PhotoGridItem` en `src/SnapTime.Server/Models/PhotoGridItem.cs`.
2. En el handler `GET /api/photos` de `Program.cs`, poblar `SuggestionStatus` desde `asset.SuggestionStatus` al crear `PhotoGridItem` para assets en BD.

#### Frontend (Karris)
1. Añadir propiedad `SuggestionStatus` al DTO `PhotoGridItem` en `src/SnapTime.Client/Models/PhotoGridItem.cs`.
2. Actualizar `GetStatusClass` en `PhotoGrid.razor` para que, si `SuggestionStatus` es `"approved"`, devuelva `"status-approved"` (azul oscuro), independientemente del `MediaStatus`.
3. Añadir CSS `.status-approved { background: #1565C0; }` en `PhotoGrid.razor`.

#### Tests (Janus)
1. Actualizar tests bUnit existentes para incluir `SuggestionStatus` en los mocks de `PhotoGridItem`.
2. Añadir test `photoGrid_statusCircle_darkBlue_whenApproved` que verifique que el círculo tiene clase `status-approved` cuando `SuggestionStatus = "approved"`.

### Criterios de aceptación
- [ ] Backend: `PhotoGridItem` incluye `SuggestionStatus` poblado desde la BD.
- [ ] Frontend: círculo del grid cambia a azul oscuro (#1565C0) cuando `SuggestionStatus` es Approved.
- [ ] Frontend: el resto de colores se mantienen igual (gris, verde, rojo, amarillo, azul claro).
- [ ] Tests bUnit: nuevo test para estado approved + tests existentes siguen verdes.
- [ ] Compilación sin errores.

