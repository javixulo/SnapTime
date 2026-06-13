# F6 — Panel de detalle (foto seleccionada)

> Sección superior del panel derecho (20% ancho total). Al seleccionar una foto en el grid central, muestra metadatos EXIF, evidencias de heurísticas y barra de confianza.

**Referencias:** FR-08, docs/06-ui.md

**Dependencias:** F1 (metadatos + evidencias en SQLite), F5 (selección en grid)

---

## F6-US-001 — Panel de detalle de foto ✅ COMPLETADO (parcial)

**Estado actual:** Componentes, endpoints y tests (bUnit + E2E) implementados completamente.

**Reglas base:**

### Panel derecho: estructura
- El panel derecho se divide en dos secciones con `display: flex; flex-direction: column`.
- La sección superior es el detalle de foto (F6). La inferior es el chat (F9).

### Detalle (sección superior)
- Al hacer clic en una miniatura del grid, la sección superior del panel derecho muestra el detalle.
- Muestra: ruta completa, tamaño, fechas EXIF (4 tags), fechas filesystem (ctime/mtime).
- Lista de evidencias generadas por heurísticas: id, nombre, peso, dirección (+/-), descripción.
- Barra de confianza visual (0-100) con color: verde ≥80, amarillo 50-79, rojo <50.
- Al hacer clic en otra miniatura, el detalle se actualiza.
- Si no hay foto seleccionada, la sección superior muestra un placeholder.
- **Fotos sin escanear:** si el archivo no tiene ID en BD (Guid.Empty), se muestra la metadata básica desde el endpoint `from-file`.
- **Botones Aceptar/Rechazar:** placeholders no funcionales. Se implementan en F7. Preparados para recibir `ScanStateService` por DI.

### Contrato API
- `GET /api/media-assets/{id}` → `MediaAssetDetailDto` con metadatos + evidencias (solo escaneados).
- `GET /api/media-assets/from-file?path={ruta}` → `FileMetadataDto` con metadatos leídos directamente del disco (EXIF + filesystem). Sin BD.

### Implementación
- **Frontend:** `PhotoDetail.razor` con estados placeholder, loading, error, metadatos (escaneado/no escaneado), evidencia, confidence bar. Botones Aceptar/Rechazar (placeholders, clase CSS `.btn-accept`/`.btn-reject`).
- **Backend:** `GET /api/media-assets/{id}`, `GET /api/media-assets/from-file`.

### Deselección (nuevo requisito)

- Al hacer clic en un directorio del grid (single click), la selección de foto debe limpiarse.
- `PhotoGrid.razor`: `SelectItem()` debe invocar `OnPhotoSelected.InvokeAsync(null)` cuando `item.IsDirectory == true`.
- `Home.razor`: `HandlePhotoSelected()` debe manejar `item == null` limpiando `_selectedPhotoId` y `_selectedPhotoPath`.
- CSS: No se requieren cambios visuales. La deselección muestra el placeholder `.photo-detail-empty`.

### Tests

#### Tests unitarios / bUnit (✅ completados)
- **T-001** — bUnit PhotoDetail: render condicional (placeholder, loading, error, metadatos, evidencias, barra de confianza). Prueba de que al hacer click en foto escaneada se muestran evidencias. Prueba de que al hacer click en foto no escaneada se muestran metadatos desde `from-file`. ✅
- **T-002** — bUnit PhotoGrid: la navegación interna (doble click subcarpeta) no se resetea al re-renderizar el padre. ✅
- **T-003** — bUnit Home: al hacer click en una foto, `PhotoDetail` recibe `SelectedAssetId` y `SelectedAssetPath`. ✅
- **T-004** — Client service: `PhotoClient.GetAssetDetailAsync` y `PhotoClient.GetFileMetadataAsync`. ✅
- **T-005** — Integration: `GET /api/media-assets/{id}` devuelve detalle con evidencias. `GET /api/media-assets/from-file` devuelve metadatos desde archivo. ✅

#### Tests E2E (✅ completados)

Archivo: `tests/SnapTime.E2ETests/Pages/PhotoDetailE2ETests.cs`

| # | Caso | Test | Estado |
|---|------|------|--------|
| 1 | Click foto escaneada → panel detalle visible | `PhotoDetail_ClickThumbnail_ShowsDetail` | ✅ |
| 2 | Click foto no escaneada → metadatos básicos | `PhotoDetail_ClickThumbnail_ShowsMetadata` | ✅ |
| 3 | Click otra foto → detalle se actualiza | `PhotoDetail_ClickDifferentPhoto_UpdatesDetail` | ✅ |
| 4 | Click directorio → deselecciona foto → "Selecciona una foto" | `PhotoDetail_Deselect_ClearsDetail` | ✅ |
| 5 | Subcarpeta + click foto → breadcrumb coherente | `PhotoDetail_NavigateSubfolder_ClickPhoto_StaysInSubfolder` | ✅ |
| 6 | Click breadcrumb → detalle se limpia | `PhotoDetail_ClickBreadcrumb_ClearsDetail` | ✅ |
| — | Extra: post-scan click sin error 404 | `PhotoDetail_AfterScanClickThumbnail_ShowsDetailWithoutError` | ✅ extra |

