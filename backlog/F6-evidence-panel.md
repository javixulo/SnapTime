# F6 — Panel de detalle (foto seleccionada)

> Sección superior del panel derecho (20% ancho total). Al seleccionar una foto en el grid central, muestra metadatos EXIF, evidencias de heurísticas y barra de confianza.

**Referencias:** FR-08, docs/06-ui.md

**Dependencias:** F1 (metadatos + evidencias en SQLite), F5 (selección en grid)

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
- **Fotos sin escanear:** si el archivo no tiene ID en BD (Guid.Empty), se muestra la metadata básica desde el endpoint `from-file`. Sin evidencias ni sugerencias hasta que se escanee.
- **Botones Aceptar/Rechazar:** no forman parte de esta feature. Se implementarán en F7 (revisión en lote). Los botones actuales son placeholders no funcionales.
- **Preparación para F7:** el componente `PhotoDetail.razor` debe aceptar inyección de un servicio de estado (`IScanStateService` / `ScanStateService`) para que F7 US-003 pueda habilitar/deshabilitar los botones según el estado del scan. El placeholder debe renderizar los botones siempre (visibles pero sin efecto) y estar listo para recibir `ScanStateService` por DI. Los botones deben tener clase CSS `.btn-accept` y `.btn-reject` para selectores de test.
- La navegación por doble click en subcarpetas del grid debe ser independiente del árbol izquierdo. Un re-render del padre no debe resetear la carpeta de navegación interna del grid.

### Contrato API
- `GET /api/media-assets/{id}` → `MediaAssetDetailDto` con metadatos + evidencias (solo escaneados).
- `GET /api/media-assets/from-file?path={ruta}` → `FileMetadataDto` con metadatos leídos directamente del disco (EXIF + filesystem). Sin BD.

### Tests

#### Tests unitarios / bUnit
- **T-001** (✅ hecho) — bUnit PhotoDetail: render condicional (placeholder, loading, error, metadatos, evidencias, barra de confianza). Prueba de que al hacer click en foto escaneada se muestran evidencias. Prueba de que al hacer click en foto no escaneada se muestran metadatos desde `from-file`.
- **T-002** (✅ hecho) — bUnit PhotoGrid: la navegación interna (doble click subcarpeta) no se resetea al re-renderizar el padre.
- **T-003** (✅ hecho) — bUnit Home: al hacer click en una foto, `PhotoDetail` recibe `SelectedAssetId` y `SelectedAssetPath`.
- **T-004** (✅ hecho) — Client service: `PhotoClient.GetAssetDetailAsync` y `PhotoClient.GetFileMetadataAsync`.
- **T-005** (✅ hecho) — Integration: `GET /api/media-assets/{id}` devuelve detalle con evidencias. `GET /api/media-assets/from-file` devuelve metadatos desde archivo.

#### Tests E2E (Playwright) — pendientes

Diseño completo de casos E2E para `tests/SnapTime.E2ETests/Pages/PhotoDetailE2ETests.cs`:

1. **Click en foto escaneada → panel detalle visible**
   - Seleccionar carpeta
   - Escanear (para tener assets en BD)
   - Click en una miniatura
   - Assert: panel derecho muestra el nombre del archivo, metadatos, evidencias y barra de confianza

2. **Click en foto no escaneada → panel detalle con metadatos básicos**
   - Seleccionar carpeta (sin escanear)
   - Click en una miniatura
   - Assert: panel derecho muestra nombre del archivo, metadatos (fechas, tamaño). Sin evidencias ni barra de confianza

3. **Click en otra foto → detalle se actualiza**
   - Click en foto A → detalle visible
   - Click en foto B → detalle cambia al de B
   - Assert: nombre del archivo y metadatos corresponden a B

4. **Click en placeholder → se oculta el detalle**
   - Click en foto → detalle visible
   - Click en el área vacía del grid (o deseleccionar)
   - Assert: panel derecho muestra "Selecciona una foto"

5. **Doble click subcarpeta + click foto → se queda en subcarpeta y muestra detalle**
   - Seleccionar carpeta raíz
   - Doble click en subcarpeta del grid → navega dentro
   - Click en una foto de la subcarpeta
   - Assert: breadcrumb muestra la subcarpeta, panel derecho muestra detalle de la foto

6. **Click breadcrumb → detalle se limpia**
   - Click en foto → detalle visible
   - Click en breadcrumb para subir
   - Assert: panel derecho vuelve a "Selecciona una foto"

### Tareas propuestas
- **T-001** (✅ hecho) — Tests unitarios y de integración (Janus)
- **T-002** (✅ hecho) — Backend endpoints (Kip)
- **T-003** (✅ hecho) — Frontend `PhotoDetail.razor` (Karris)
- **T-004** (⏳ pendiente) — Tests E2E Playwright (6 casos)
