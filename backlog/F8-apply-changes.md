# F8 — Aplicación de cambios (escritura real)

> Escribir la fecha aceptada en los metadatos EXIF (fotos) o QuickTime (vídeos) del archivo real. Proceso real, best-effort, con resumen final de errores por archivo.

**Referencias:** FR-11, docs/07-api-contracts.md, docs/00-vision-y-alcance.md §8

**Dependencias:** F7 (assets aprobados y SuggestedDate poblada en BD)

---

## Visión general

Reglas base:

- Solo se aplican cambios a assets con `SuggestionStatus = Approved` (fotos y vídeos).
- La fecha a escribir se toma exclusivamente de `MediaAsset.SuggestedDate` (campo poblado por el proceso de scan).
- Zona horaria: siempre 05:00 AM (canonical) — el valor en la BD ya debe estar normalizado a este criterio.
- Campo a escribir:
  - Fotos: `EXIF:DateTimeOriginal`.
  - Vídeos: `QuickTime:CreateDate`.
- Aplicación real: el servicio intentará escribir cada archivo (best-effort). Para cada archivo se registra el resultado (ok/error/motivo). No hay rollback en el MVP.
- Modal de confirmación antes de aplicar: lista de cambios (archivo, fecha actual → fecha nueva). El modal mostrará las fechas al usuario en formato `dd/MM/yyyy` y la fecha sugerida deberá ir resaltada (por ejemplo con `<strong>`).
- Si falla la escritura en un archivo (p.ej. archivo readonly) se captura la excepción y se añade al resumen de errores; no detiene la ejecución del lote.
- Si un asset no tiene `SuggestedDate` se considera error y se incluye en el resumen final.

Contrato (alto nivel):

- Servicio `IApplyService` en Server (métodos para ejecución batch, best-effort).
- `IExifWriter` en Infrastructure (escribir tag EXIF/QuickTime).
- Endpoint `POST /apply` con `ApplyChangesRequest / ApplyChangesResponse` (respuesta incluye resultado por archivo y listado de errores).
- Componente Blazor `ApplyModal.razor` (modal de confirmación con lista y resumen final).
- Tests: escritura real, archivo readonly, mezcla de éxitos/errores, auditoría registrada, y bloqueo si hay scan en curso.

---

## F8-US-001 — DTOs y contrato API

### Tareas

**🔴 T-001 — Definir DTOs (Janus/Kip)**
- `ApplyChangesRequest`: `List<Guid> MediaAssetIds`.
- `ApplyChangesResponse`: `List<ApplyResult> Results, int AppliedCount, int FailedCount, DateTime Timestamp`.
- `ApplyResult`: `Guid MediaAssetId, string FileName, bool Success, string? Error`.

Acceptance: DTOs añadidos en `SnapTime.Server/Models` y `SnapTime.Client/Models`, la API compila y coincide con `docs/07-api-contracts.md`.

**🟢 T-002 — Contrato de endpoint (Kip)**
- Implementar `POST /apply` en controlador `ApplyController` que acepte `ApplyChangesRequest` y devuelva `ApplyChangesResponse`.
- Validaciones: si la petición contiene ids no existentes, reportar en `ApplyResult` como `Error` con mensaje "NotFound".

Acceptance: endpoint responde conforme al contrato y tiene tests unitarios básicos.

---

## F8-US-002 — IExifWriter (Infraestructura)

### Tareas

**🔴 T-001 — Tests de comportamiento esperable (Janus)**
- Tests unitarios que simulen escritura sobre archivos temporales (mock FS) y cubran: éxito, archivo readonly, formato no soportado.

**🟢 T-002 — Implementación IExifWriter (Kip)**
- Interfaz propuesta:
  ```csharp
  public interface IExifWriter
  {
      Task<ExifWriteResult> WriteAsync(string filePath, MediaType mediaType, DateTime newDate, CancellationToken ct = default);
  }
  public record ExifWriteResult(bool Success, string? ErrorMessage);
  ```
- Intentar usar API .NET nativa (p. ej. System.Formats) sin dependencias externas. Si no es viable para escritura o ciertos formatos, proponer `ExifLibrary` (MIT) como fallback y documentar la elección.

Acceptance: implementation writes expected EXIF/QuickTime tags on sample files in integration tests.

**🔵 T-003 — Refactor y review (Gavin)**

---

## F8-US-003 — IApplyService (Domain/Server)

### Tareas

**🔴 T-001 — Tests (Janus)**
- Tests unitarios e integración (SQLite efímera) para los casos:
  - Asset Approved + SuggestedDate presente → escritura OK → status actualizado
  - Asset Approved + SuggestedDate ausente → error reportado
  - Asset Approved + archivo readonly → error reportado
  - Asset no Approved en request → marcado como Skipped en resultado
  - Mezcla de assets (éxitos/errores) → best-effort, aplicados los posibles

**🟢 T-002 — Implementación (Kip)**
- Lógica propuesta:
  - Recuperar assets por id desde BD.
  - Validar `SuggestionStatus == Approved`. Si no, añadir `ApplyResult` con `Success = false, Error = "NotApproved"`.
  - Para cada asset aprobado:
    - Tomar `SuggestedDate` desde BD. Si nulo → `Error = "MissingSuggestedDate"`.
    - Llamar `IExifWriter.WriteAsync(filePath, mediaType, suggestedDate)`.
    - Si éxito: actualizar `MediaAsset.Status` a `Completed` y persistir cambios.
    - Si error: capturar excepción y añadir `ApplyResult` con `Error = ex.Message`.
  - Crear un `AuditEntry` con el resumen (applied/failed/results) y persistir.
  - Antes de iniciar: verificar si existe un ScanJob con estado `Running`; si sí, rechazar la ejecución con 409 Conflict.

Acceptance: Servicio pasa tests de integración y crea AuditEntry.

**🔵 T-003 — Refactor/optimización (Kip)**

---

## F8-US-004 — Endpoint POST /apply (Server)

### Tareas

**🔴 T-001 — Tests de contrato (Janus)**
- Test que haga POST /apply con una mezcla de ids y verifique el ApplyChangesResponse contiene los `ApplyResult` esperados.

**🟢 T-002 — Implementación Controller (Kip)**
- Implementar `ApplyController` con `POST /apply` que delegue en `IApplyService`.

Acceptance: Integration test con WebApplicationFactory confirma flujo end-to-end.

---

## F8-US-005 — Frontend: ApplyModal.razor (Karris)

### Tareas

**🔴 T-001 — bUnit Tests (Janus/Karris)**
- Tests para estados: empty, confirmation list, applying, success, errors.

**🟢 T-002 — Implementación (Karris)**
- Modal presenta lista: `FileName`, `Fecha actual` (leer de `MediaAsset.DateTimeOriginal` o `FileMetadata`), `Fecha sugerida` (`SuggestedDate`). Formato `dd/MM/yyyy`. Fecha sugerida en `<strong>`.
- Botón "Aplicar" llama `POST /api/apply` y muestra el resumen final con errores.
- Botón deshabilitado si existe un ScanJob en ejecución (UI obtiene estado jobs para decidir).

Acceptance: bUnit + Playwright E2E valida el flujo.

---

## F8-US-006 — Tests E2E (Janus)

### Tareas

**🔴 T-001 — E2E exitoso**
- Playwright test que simula assets aprobados y verifica que tras aplicar las fechas se actualizan y modal muestra summary.

**🔴 T-002 — E2E mezcla éxito/error**
- Al menos un asset readonly o sin SuggestedDate; verificar que aparecen en resumen final con motivo.

**🔴 T-003 — E2E bloqueo por scan activo**
- Intentar aplicar mientras hay scan Running: backend responde 409 y UI muestra mensaje.

Acceptance: E2E tests pasan en la suite autónoma (WebApplicationFactory + SQLite efímera).

---

## F8-US-007 — DB / EF migration (Kip)

### Tareas

**🟢 T-001 — MediaStatus.Completed (opcional)**
- Añadir `Completed` a `MediaStatus` enum si se decide distinguir; si implica cambios en EF, crear migration `F8_AddMediaStatusCompleted`.

Acceptance: migration generada y test de smoke.

---

## F8-US-008 — QA / Code review (Gavin)

### Tareas

**👁 T-001 — Review completo**
- Revisar código, seguridad, concurrencia, manejo de archivos grandes y bloqueo por scan.

Acceptance: Gavin aprueba y lista comentarios a corregir (si hay).

---

## Notas finales

- Evitar añadir dependencias sin autorización previa; si la solución nativa no cubre escritura de EXIF/QuickTime, proponer `ExifLibrary` y solicitar validación.
- No ejecutar commits/push sin permiso del usuario (regla reforzada en AGENTS.md).
