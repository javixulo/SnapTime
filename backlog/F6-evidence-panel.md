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
- Botones: "Aceptar sugerencia" y "Rechazar" (conectan con F7).
- Al hacer clic en otra miniatura, el detalle se actualiza.
- Si no hay foto seleccionada, la sección superior muestra un placeholder.

### Contrato API
- `GET /api/media-assets/{id}` → `MediaAssetDetailDto` con evidencias.

### Tareas propuestas
- **T-001** — Tests (Janus): bUnit PhotoDetail (render condicional, con/sin evidencias). E2E (click miniatura → detalle visible).
- **T-002** — Backend (Kip): Endpoint `GET /api/media-assets/{id}` con evidencias.
- **T-003** — Frontend (Karris): `PhotoDetail.razor` en sección superior del panel derecho, conectado a `OnPhotoSelected` del grid.
