# F6 — Subpanel de evidencia (detalle + scoring)

> Subpanel en la parte inferior del panel central. Al seleccionar un archivo en el grid, muestra detalle EXIF completo + lista de evidencias con pesos y directionalidad.

**Referencias:** FR-08, docs/06-ui.md

**Dependencias:** F1 (metadatos + evidencias en SQLite), F5 (selección en grid)

**Reglas base:**
- Al hacer clic en una fila del grid, el subpanel se expande
- Muestra: ruta completa, tamaño, fechas EXIF (4 tags), fechas filesystem (ctime/mtime)
- Lista de evidencias generadas por heurísticas: id, nombre, peso, dirección (+/-), descripción
- Barra de confianza visual (0-100) con color: verde ≥80, amarillo 50-79, rojo <50
- Botones: "Aceptar sugerencia" y "Rechazar" (conectan con F7)
- El subpanel se colapsa al hacer clic en otro archivo o al cerrar manualmente

**Contrato (pendiente de desglosar en US):**
- Componente Blazor `PhotoDetail.razor`
- Endpoint `GET /photos/{id}` (ya definido en 07-api-contracts)
- Tests de render condicional (con/sin evidencias)
