# F7 — Aplicación de cambios (dry-run + escritura)

> Escribir la fecha aceptada en los metadatos EXIF del archivo real. Incluye modo simulación (dry-run) y aplicación real con confirmación.

**Referencias:** FR-11, docs/07-api-contracts.md, docs/00-vision-y-alcance.md §8

**Dependencias:** F6 (fotos aprobadas)

**Reglas base:**
- Solo se aplican cambios a fotos con estado `Approved`
- La fecha a escribir es `SuggestedDate` (si existe)
- Zona horaria: siempre 5:00 AM (canonical)
- Campo a escribir: `DateTimeOriginal` (EXIF tag)
- Dry-run: simula la escritura, devuelve el resultado sin modificar el archivo
- Aplicación real: escribe en el archivo, registra en auditoría, marca el archivo como `Completed`
- Modal de confirmación antes de aplicar: lista de cambios (archivo, fecha actual → fecha nueva)
- Si falla la escritura → se registra error, no se detiene el lote

**Contrato (pendiente de desglosar en US):**
- Servicio `IApplyService` en Server
- `IExifWriter` en Infrastructure (escribir tag EXIF)
- Endpoint `POST /apply` con `ApplyChangesRequest / ApplyChangesResponse`
- Componente Blazor `ApplyModal.razor`
- Tests: dry-run, escritura real, error en archivo readonly, rollback
