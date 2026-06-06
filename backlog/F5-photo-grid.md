# F5 — Grid de fotos (panel central)

> Panel central (60% ancho) con grid paginado de miniaturas, columnas con indicadores visuales, filtros y ordenación.

**Referencias:** FR-08, FR-18, docs/07-api-contracts.md

**Dependencias:** F0 (API), F1 (fotos en SQLite), F4 (filtro por carpeta)

**Reglas base:**
- Grid con columnas: miniatura, nombre, fecha EXIF, fecha sugerida, confianza (barra), estado (badge)
- Paginación: 20/50/100/Todas (lazy loading con Virtualize)
- Filtros: por carpeta (desde F4), por estado, por rango de confianza
- Ordenación por defecto: nombre. Cambiable por cualquier columna.
- Al hacer clic en una fila → F6 muestra detalle en subpanel
- Checkbox por fila para selección múltiple (para F7)

**Contrato (pendiente de desglosar en US):**
- Componente Blazor `PhotoGrid.razor`
- Endpoint `GET /photos` con paginación y filtros
- Endpoint `GET /thumbnails/{photoId}` con caché en disco
- Servicio `IPhotoService` en Server
- Tests del grid con datos mock
