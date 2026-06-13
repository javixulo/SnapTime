# F6 — Panel de detalle (foto seleccionada)

> Sección superior del panel derecho (20% ancho total). Al seleccionar una foto en el grid central, muestra metadatos EXIF, evidencias de heurísticas y barra de confianza.

**Referencias:** FR-08, docs/06-ui.md

**Dependencias:** F1 (metadatos + evidencias en SQLite), F5 (selección en grid)

---

## F6-US-001 — Panel de detalle de foto ✅ COMPLETADO (parcial)

**Estado actual:** Componentes, endpoints y tests unitarios/bUnit implementados. Tests E2E pendientes.

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

### Tests

#### Tests unitarios / bUnit (✅ completados)
- **T-001** — bUnit PhotoDetail: render condicional (placeholder, loading, error, metadatos, evidencias, barra de confianza). Prueba de que al hacer click en foto escaneada se muestran evidencias. Prueba de que al hacer click en foto no escaneada se muestran metadatos desde `from-file`.
- **T-002** — bUnit PhotoGrid: la navegación interna (doble click subcarpeta) no se resetea al re-renderizar el padre.
- **T-003** — bUnit Home: al hacer click en una foto, `PhotoDetail` recibe `SelectedAssetId` y `SelectedAssetPath`.
- **T-004** — Client service: `PhotoClient.GetAssetDetailAsync` y `PhotoClient.GetFileMetadataAsync`.
- **T-005** — Integration: `GET /api/media-assets/{id}` devuelve detalle con evidencias. `GET /api/media-assets/from-file` devuelve metadatos desde archivo.

#### Tests E2E (⏳ pendientes)

Diseño completo de casos E2E para `tests/SnapTime.E2ETests/Pages/PhotoDetailE2ETests.cs`:

1. **Click en foto escaneada → panel detalle visible**
2. **Click en foto no escaneada → panel detalle con metadatos básicos**
3. **Click en otra foto → detalle se actualiza**
4. **Click en placeholder → se oculta el detalle**
5. **Doble click subcarpeta + click foto → se queda en subcarpeta y muestra detalle**
6. **Click breadcrumb → detalle se limpia**
