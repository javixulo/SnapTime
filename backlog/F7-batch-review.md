# F7 — Escaneo y revisión en lote

> Proceso completo: escanear una carpeta con control de ejecución (progreso + cancelación + reescaneo), agregar confianza y sugerencia de fecha, y aprobar/rechazar archivos de forma individual o por lote (carpeta actual o total escaneado).

**Referencias:** FR-03, FR-04, FR-08, FR-09, FR-10, docs/06-requisitos-ui.md, docs/07-api-contracts.md

**Dependencias:** F4 (selección de carpeta + toggle subcarpetas), F5 (grid de fotos), F6 (panel de detalle), F1 (datos de scan en SQLite)

---

## US-001 — Escaneo con progreso, cancelación y reescaneo ✅ COMPLETADO

> El ScanPanel se ubica en el panel superior (4.4). El botón "Escanear" lanza un job de análisis asíncrono. Muestra progreso en tiempo real, permite cancelar, y al re-escanear una carpeta ya escaneada se fuerza el reescaneo completo de todos sus archivos.

**Estado actual:** Implementado. Componentes, endpoints y lógica de reescaneo completados.

### Reglas base (implementadas)
- El ScanPanel se ubica en el **panel superior** (4.4), no en el panel izquierdo.
- Usa `{ rootPath: carpetaSeleccionada, includeSubfolders: bool }` desde F4.
- El botón "Escanear" se **deshabilita** mientras el scan está activo.
- Durante el scan, aparece un botón "Cancelar".
- Al finalizar o cancelar, "Escanear" se **rehabilita** y "Cancelar" desaparece.
- Progreso: "Procesando N de M archivos".
- Estados visibles: `idle`, `scanning`, `cancelled`, `completed`, `error`.
- **Reescaneo:** si la carpeta ya fue escaneada, al pulsar "Escanear" de nuevo se eliminan los datos previos y se recogen desde cero.
- El grid de fotos (F5) se actualiza automáticamente al finalizar.

### Contrato API
- `POST /api/jobs` → crea y encola job
- `GET /api/jobs/{id}` → estado y conteo
- `POST /api/jobs/{id}/cancel` → cancelación cooperativa

### Componentes
- `ScanPanel.razor` + `ScanPanel.razor.cs` — implementados con estados idle/scanning/cancelled/completed/error.

---

## US-002 — Motor de agregación de confianza ✅ COMPLETADO

> Las evidencias recolectadas durante el escaneo se sintetizan en un `ConfidenceScore` (0-100), un `MediaStatus` y una sugerencia (`SuggestedDate` + `SuggestedByHeuristic` + `SuggestionReviewStatus`) por archivo.

**Estado actual:** `HeuristicEngine` implementado y ejecutado en el pipeline de `ScanJobService.ProcessSingleFileAsync`.

### Reglas base (implementadas)
- Se ejecuta como **paso final del pipeline de escaneo**, tras la extracción de metadatos y heurísticas.
- Procesa las `EvidenceEntry` de cada `MediaAsset` y calcula `MediaStatus`, `ConfidenceScore`, `SuggestedDate`, `SuggestedByHeuristic`, `SuggestionReviewStatus`.
- El umbral "suficiente peso" se vincula al `confidenceThreshold` configurable (defecto: 70). Una evidencia de corrección genera sugerencia si `Weight * 100 >= threshold`.
- Al reescanear una carpeta, se regeneran scores, status y sugerencias desde cero.
- **Fix re-escaneo:** `PersistProgressCheckpointAsync` ahora actualiza también `Status`, `SuggestedDate`, `ConfidenceScore`, `SuggestedByHeuristic` y `SuggestionStatus` en assets existentes (no solo `MetadataEntries` y `EvidenceEntries`).

### Contrato (servicio interno)
- `IHeuristicEngine` / `HeuristicEngine` — registra y ejecuta la agregación.
- El pipeline de `ScanJobService` invoca al engine tras la recolección por archivo.

---

## US-003 — Servicio de coordinación de estado de escaneo ✅ COMPLETADO

> Un servicio singleton (`ScanStateService`) expone el estado actual del escaneo y notifica a los componentes que necesitan habilitar/deshabilitar sus botones.

**Estado actual:** `ScanStateService` implementado en el cliente Blazor.

### Reglas base (implementadas)
- `ScanStateService` (singleton) expone:
  - `bool IsScanning`
  - `bool HasCompletedScan` (se resetea al cambiar de carpeta)
  - `event Action<ScanState> StateChanged`
- `PhotoDetail.razor` se suscribe a `StateChanged` para habilitar/deshabilitar Aceptar/Rechazar.
- `BatchActions.razor` se suscribe para habilitar/deshabilitar botones de lote.
- El `ScanPanel` notifica a `ScanStateService` cuando inicia/cancela/completa un scan.

### Componentes
- `Services/ScanStateService.cs` — implementado
- `ScanPanel.razor.cs` — notifica cambios
- `PhotoDetail.razor` — suscrito (placeholders)
- `BatchActions.razor` — implementado

---

## US-004 — Revisión y aprobación/rechazo de sugerencias ✅ COMPLETADO

> Una vez escaneada una carpeta y calculadas las sugerencias, el usuario puede aprobar o rechazar las sugerencias de fecha de cada archivo, tanto de forma individual como por lote.

**Estado actual:** Endpoints y componentes implementados.

### Reglas base (implementadas)
- **Sin selección múltiple en grid.**
- **Aceptar/Rechazar individual:** botones funcionales en `PhotoDetail.razor`. Habilitados solo si scan completado + archivo tiene `SuggestedDate`.
- **Aceptar Carpeta / Rechazar Carpeta:** opera sobre carpeta actual del grid.
- **Aceptar Total / Rechazar Total:** opera sobre todos los archivos escaneados.
- Persistencia inmediata en SQLite.
- Modal de confirmación para operaciones por lote.

### Contrato API
- `POST /api/reviews/single` → single review
- `POST /api/reviews/batch` → batch review (scope "folder" o "total")

### Componentes
- `PhotoDetail.razor` — botones Aceptar/Rechazar funcionales
- `BatchActions.razor` — botones en panel superior
- Modal de confirmación
- Ambos se suscriben a `ScanStateService`

#### Tests E2E (✅ completados)

Archivo: `tests/SnapTime.E2ETests/Pages/ReviewE2ETests.cs`

| # | Caso | Test | Estado |
|---|------|------|--------|
| 1 | Aceptar sugerencia individual → status Approved | `Review_AcceptSuggestion_ChangesStatusToApproved` | ✅ |
| 2 | Rechazar sugerencia individual → status Rejected | `Review_RejectSuggestion_ChangesStatusToRejected` | ✅ |
| 3 | Aceptar Todo (carpeta) → todas las sugerencias Approved | `Review_AcceptAllInFolder_AllSuggestionsApproved` | ✅ |
| 4 | Rechazar Todo (carpeta) → todas las sugerencias Rejected | `Review_RejectAllInFolder_AllSuggestionsRejected` | ✅ |
| 5 | Aceptar Total (multi-carpeta) → sugerencias de todas las carpetas Approved | `Review_AcceptTotal_AffectsMultipleScannedFolders` | ✅ |
