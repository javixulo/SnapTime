# F6 — Revisión en lote

> Aprobar o rechazar sugerencias en masa desde el grid, con selección múltiple y resumen previo a la acción.

**Referencias:** FR-10, docs/07-api-contracts.md

**Dependencias:** F4 (selección múltiple en grid), F5 (aceptar/rechazar individual)

**Reglas base:**
- Checkbox por fila en el grid (F4)
- Barra de acciones: "Aprobar selección (N)" / "Rechazar selección (N)"
- Al hacer clic en aprobar/rechazar → cambia estado de las fotos a `Approved` / `Rejected`
- No hay dry-run en esta fase (es solo cambio de estado interno, no escritura a archivo)
- El estado se refleja inmediatamente en el grid (badge de color)
- Persistencia del cambio en SQLite

**Contrato (pendiente de desglosar en US):**
- Componente Blazor `BatchActions.razor`
- Endpoint `POST /reviews/batch` con `BatchReviewRequest`
- Servicio `IReviewService` en Server
- Tests: aprobar/rechazar lote, mezcla de estados, IDs inválidos
