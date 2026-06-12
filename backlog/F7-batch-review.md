# F7 — Escaneo y revisión en lote

> Proceso completo: escanear una carpeta con control de ejecución (progreso + cancelación + reescaneo), agregar confianza y sugerencia de fecha, y aprobar/rechazar archivos de forma individual o por lote (carpeta actual o total escaneado).

**Referencias:** FR-03, FR-04, FR-08, FR-09, FR-10, docs/06-requisitos-ui.md, docs/07-api-contracts.md

**Dependencias:** F4 (selección de carpeta + toggle subcarpetas), F5 (grid de fotos), F6 (panel de detalle), F1 (datos de scan en SQLite)

---

## US-001 — Escaneo con progreso, cancelación y reescaneo

> El ScanPanel se traslada al panel superior (4.4). El botón "Escanear" lanza un job de análisis asíncrono. Muestra progreso en tiempo real, permite cancelar, y al re-escanear una carpeta ya escaneada se fuerza el reescaneo completo de todos sus archivos.

**Reglas base:**
- El ScanPanel se ubica en el **panel superior** (4.4), no en el panel izquierdo.
- Usa `{ rootPath: carpetaSeleccionada, includeSubfolders: bool }` desde F4.
- El botón "Escanear" se **deshabilita** mientras el scan está activo.
- Durante el scan, aparece un botón "Cancelar".
- Al finalizar o cancelar, "Escanear" se **rehabilita** y "Cancelar" desaparece.
- Progreso: "Procesando N de M archivos".
- Estados visibles: `idle`, `scanning`, `cancelled`, `completed`, `error`.
- **Reescaneo:** si la carpeta ya fue escaneada, al pulsar "Escanear" de nuevo se eliminan los datos previos (`MediaAsset`, `MetadataEntry`, `EvidenceEntry` de esa carpeta) y se vuelven a recoger desde cero. Esto cubre el caso de archivos modificados entre escaneos.
- El grid de fotos (F5) se actualiza automáticamente al finalizar.

### Contrato API
- `POST /api/jobs` → crea y encola job (respuesta: `ScanJob { id, rootPath, status, ... }`)
- `GET /api/jobs/{id}` → estado y conteo (útil para polling del cliente)
- `POST /api/jobs/{id}/cancel` → cancelación cooperativa del job

### Componentes
- `ScanPanel.razor` + `ScanPanel.razor.cs` — se mueven al panel superior, se actualizan con estado disabled/enabled y reescaneo

### Tests

**bUnit:**
- ScanPanel: botón "Escanear" visible y habilitado en estado idle.
- ScanPanel: al hacer clic → "Escanear" se deshabilita, aparece "Cancelar", muestra progreso (mock HTTP).
- ScanPanel: al hacer clic en "Cancelar" → "Escanear" se rehabilita, "Cancelar" desaparece, estado "Cancelled".
- ScanPanel: al hacer clic en "Escanear" sobre carpeta ya escaneada → llama a POST /api/jobs (reescaneo forzado).
- ScanPanel: error de API → muestra mensaje de error.

**E2E** (arranque autónomo vía `WebApplicationFactory<Program>` + SQLite efímera):
- Seleccionar carpeta, click "Escanear" → job se crea y progreso avanza.
- Click "Cancelar" durante scan → job se cancela (estado "Cancelled").
- Scan completado → grid muestra archivos escaneados.
- Escanear misma carpeta dos veces → el segundo scan reemplaza los datos anteriores.

---

## US-002 — Motor de agregación de confianza

> Las evidencias recolectadas durante el escaneo se sintetizan en un `ConfidenceScore` (0-100), un `MediaStatus` y una sugerencia (`SuggestedDate` + `SuggestedByHeuristic` + `SuggestionReviewStatus`) por archivo. Sin esto, los botones Aceptar/Rechazar nunca se activan.

**Reglas base:**
- Se ejecuta como **paso final del pipeline de escaneo**, tras la extracción de metadatos y heurísticas.
- Procesa las `EvidenceEntry` de cada `MediaAsset` y calcula:
  - `MediaStatus` (Pending, Correct, Error, NoSuggestion, HasSuggestion) según las evidencias:
    - Sin escanear → `Pending`.
    - Sin evidencias o todas positivas con confianza ≥ umbral → `Correct`.
    - Error en el procesamiento → `Error`.
    - Evidencias Correction sin suficiente peso → `NoSuggestion`.
    - Evidencias Correction con peso ≥ `confidenceThreshold` → `HasSuggestion`.
  - `ConfidenceScore`: ponderación de evidencias según dirección (Positive, Negative, Correction) y peso de cada heurística. 0-100.
  - `SuggestedDate`: si existe una evidencia Correction dominante con peso ≥ `confidenceThreshold`, se asigna la fecha alternativa.
  - `SuggestedByHeuristic`: id de la heurística que produjo la sugerencia.
  - `SuggestionReviewStatus`: `Unreviewed` si hay sugerencia, no aplica si no.
- Sin evidencias o sin peso suficiente → `SuggestedDate = null`, `ConfidenceScore = 0`, `MediaStatus` según corresponda.
- El umbral "suficiente peso" se vincula al `confidenceThreshold` configurable en BD (tabla `Settings`, columna `ConfidenceThreshold`; defecto: 80). Ver `docs/08-configuracion.md`.
- Al reescanear una carpeta (US-001), se regeneran scores, status y sugerencias desde cero.

### Contrato API (servicio interno, sin endpoint nuevo)
- `IHeuristicEngine` / `HeuristicEngine` — registra todas las heurísticas disponibles y ejecuta la agregación
- El pipeline de `ScanJobService` invoca al engine tras la recolección por archivo

### Tests

**Unit (SnapTime.Domain.Tests):**
- HeuristicEngine con evidencias Positive y peso alto → MediaStatus = Correct, score alto.
- HeuristicEngine con evidencias mixtas → score medio, MediaStatus = NoSuggestion.
- HeuristicEngine sin evidencias → score 0, MediaStatus = Correct.
- HeuristicEngine con evidencia Correction dominante → SuggestedDate asignado, SuggestionReviewStatus = Unreviewed, MediaStatus = HasSuggestion.
- Re-scaneo: scores, status y sugerencias anteriores se descartan y se recalculan.

---

## US-003 — Servicio de coordinación de estado de escaneo

> Un servicio singleton (`ScanStateService`) expone el estado actual del escaneo y notifica a los componentes que necesitan habilitar/deshabilitar sus botones (detalle y botones de lote).

**Reglas base:**
- `ScanStateService` (singleton en el cliente Blazor) expone:
  - `bool IsScanning` — true mientras hay un job en ejecución.
  - `bool HasCompletedScan` — true si se completó al menos un scan. Se **resetea a false** al cambiar de carpeta (el usuario debe escanear la nueva carpeta para que los botones de lote se habiliten).
  - `event Action<ScanState> StateChanged` — evento para que los componentes se suscriban.
- `PhotoDetail.razor` se suscribe a `StateChanged` para habilitar/deshabilitar Aceptar/Rechazar según:
  1. `ScanState.IsScanning == false`
  2. El archivo tiene `SuggestedDate` no nulo.
- `BatchActions.razor` se suscribe para habilitar/deshabilitar los botones de lote:
  1. `ScanState.IsScanning == false`
  2. Hay al menos un archivo con recomendación en el ámbito (carpeta o total).
- El `ScanPanel` notifica a `ScanStateService` cuando inicia/cancela/completa un scan.

### Componentes nuevos
- `Services/ScanStateService.cs` — estado singleton + evento
- Modificar `ScanPanel.razor.cs` para notificar cambios de estado
- Modificar `PhotoDetail.razor` para suscribirse y reflejar disabled/enabled
- Modificar `BatchActions.razor` para suscribirse

### Tests

**bUnit:**
- ScanStateService: estado inicial `IsScanning = false`, `HasCompletedScan = false`.
- ScanStateService: al notificar scan start → `IsScanning = true`.
- ScanStateService: al notificar scan complete → `IsScanning = false`, `HasCompletedScan = true`.
- PhotoDetail: con scan activo → botones deshabilitados (clase CSS `disabled`).
- PhotoDetail: scan completado + archivo sin SuggestedDate → botones deshabilitados.
- PhotoDetail: scan completado + archivo con SuggestedDate → botones habilitados.
- BatchActions: scan activo → botones de lote deshabilitados.
- BatchActions: scan completado + sin archivos con recomendación → botones deshabilitados.
- BatchActions: scan completado + hay archivos con recomendación → botones habilitados.

**E2E:**
- Scan completado → seleccionar miniatura con sugerencia → Aceptar habilitado.
- Scan completado → seleccionar miniatura sin sugerencia → Aceptar deshabilitado.
- Durante scan → miniatura no permite Aceptar/Rechazar.

---

## US-004 — Revisión y aprobación/rechazo de sugerencias

> Una vez escaneada una carpeta y calculadas las sugerencias, el usuario puede aprobar o rechazar las **sugerencias de fecha** (`SuggestedDate`) de cada archivo, tanto de forma individual (panel de detalle) como por lote (carpeta actual visible o total escaneado).

**Reglas base:**
- **Sin selección múltiple en grid.** No hay checkboxes por fila.
- El círculo de color en el grid (F5) muestra el **`MediaStatus`** del archivo (Correct, Error, NoSuggestion, HasSuggestion), no el estado de revisión de la sugerencia.
- El estado de revisión de la sugerencia (`SuggestionReviewStatus`) se muestra en el panel de detalle y opcionalmente como indicador secundario en el grid (Fase 2).
- **Aceptar/Rechazar individual:** botones funcionales en el panel de detalle (F6, antes placeholders). Habilitados solo si `ScanState.IsScanning == false` y el archivo tiene `SuggestedDate` no nulo (gestionado por US-003).
  - Al pulsar **Aceptar**, la sugerencia del archivo pasa a `SuggestionReviewStatus.Approved`.
  - Al pulsar **Rechazar**, pasa a `SuggestionReviewStatus.Rejected`.
  - El cambio se persiste en SQLite y se refleja en el detalle.
- **Aceptar Todo / Rechazar Todo:** opera sobre las sugerencias de todos los archivos visibles en la carpeta actual del grid que tengan `SuggestedDate` no nulo y estén `Unreviewed`. Muestra resumen "Se aprobarán/rechazarán N sugerencias" con confirmación.
- **Aceptar Total / Rechazar Total:** opera sobre las sugerencias de todos los archivos escaneados (todas las carpetas) con `SuggestedDate` no nulo y `Unreviewed`. Modal de confirmación con resumen.
- Botones de lote habilitados solo si `ScanState.IsScanning == false` y hay al menos un archivo con sugerencia no revisada (`Unreviewed`) en el ámbito.
- Persistencia inmediata en SQLite.
- Solo los archivos con `SuggestionReviewStatus.Approved` serán procesados por F8 (aplicación de cambios).

### Contrato API
- `POST /api/reviews/single` → `SingleReviewRequest { assetId, status }` → `MediaAssetDto`
- `POST /api/reviews/batch` → `BatchReviewRequest { scope, status, rootPath? }` → `List<Guid>`
- `GET /api/media-assets?folderPath=&page=&pageSize=` → cada `MediaAssetDto` incluye `MediaStatus` y `SuggestionReviewStatus`

### Componentes
- `PhotoDetail.razor` — botones Aceptar/Rechazar funcionales (antes placeholders)
- `BatchActions.razor` — botones en el **panel superior (4.4)**: Aceptar Todo / Rechazar Todo (carpeta) + Aceptar Total / Rechazar Total
- Modal de confirmación para operaciones por lote (resumen con contador simple, sin lista detallada por archivo — la lista detallada se evaluará en F8)
- Ambos componentes se suscriben a `ScanStateService` (US-003) para su estado disabled/enabled

### Tests

**bUnit:**
- PhotoDetail: botones Aceptar/Rechazar habilitados (scan completado + SuggestedDate presente).
- PhotoDetail: al hacer clic en Aceptar → llama a API y badge cambia a verde.
- PhotoDetail: al hacer clic en Rechazar → llama a API y badge cambia a rojo.
- BatchActions: botones Aceptar Todo y Rechazar Todo visibles y habilitados.
- BatchActions: al hacer clic en Aceptar Todo → modal de confirmación aparece.
- BatchActions: confirmar Aceptar Todo → llama a API con scope "folder".
- BatchActions: Aceptar Total → llama a API con scope "total".

**E2E** (todos con arranque autónomo vía `WebApplicationFactory<Program>` + SQLite efímera, según §8 de 06-requisitos-ui.md y F0-US-010):
- Escanear carpeta → click miniatura con sugerencia → click Aceptar → `SuggestionReviewStatus` cambia a Approved.
- Escanear carpeta → click miniatura con sugerencia → click Rechazar → `SuggestionReviewStatus` cambia a Rejected.
- Escanear carpeta → click Aceptar Todo → confirmar → todas las sugerencias de la carpeta cambian a Approved.
- Escanear carpeta → click Rechazar Todo → confirmar → todas las sugerencias de la carpeta cambian a Rejected.
- Aceptar Total desde una carpeta → afecta también a sugerencias de otras carpetas escaneadas.
