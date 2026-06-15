# F8 — Aplicación de cambios (escritura real)

> Escribir la fecha aceptada en los metadatos EXIF (fotos) o QuickTime (vídeos) del archivo real. Proceso real, best-effort, con resumen final de errores por archivo.

**Referencias:** FR-11, docs/07-api-contracts.md, docs/03-blueprint-flujo-modulos-y-fases.md §4.1, docs/00-vision-y-alcance.md §8

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
  - Además, se anota el valor original y las heurísticas responsables en `EXIF UserComment` (fotos) o `QuickTime ©cmt` (vídeos).
- Formato de la anotación: `SnapTime;original=YYYY-MM-DDTHH:mm:ss;heuristics=H-XXX,H-YYY`.
- La anotación se escribe en la misma operación que la fecha (misma transacción de escritura).
- Aplicación real: el servicio intentará escribir cada archivo (best-effort). Para cada archivo se registra el resultado (ok/error/motivo). No hay rollback en el MVP.
- Modal de confirmación antes de aplicar: lista de cambios (archivo, fecha actual → fecha nueva). El modal mostrará las fechas al usuario en formato `dd/MM/yyyy` y la fecha sugerida deberá ir resaltada (por ejemplo con `<strong>`).
- Si falla la escritura en un archivo (p.ej. archivo readonly) se captura la excepción y se añade al resumen de errores; no detiene la ejecución del lote.
- Si un asset no tiene `SuggestedDate` se considera error y se incluye en el resumen final.

Contrato (alto nivel):

- Servicio `IApplyService` en Server (métodos para ejecución batch, best-effort).
- `IExifWriter` en Infrastructure (escribir tag EXIF/QuickTime + UserComment/©cmt).
- Endpoint `POST /apply` con `ApplyChangesRequest / ApplyChangesResponse` (respuesta incluye resultado por archivo y listado de errores).
- Componente Blazor `ApplyModal.razor` (modal de confirmación con lista y resumen final).
- Tests: escritura real, archivo readonly, mezcla de éxitos/errores, auditoría registrada, y bloqueo si hay scan en curso.
- Botón "Iniciar" en el panel superior: deshabilitado hasta que haya al menos un escaneo completado. Al pulsarlo se abre el `ApplyModal`.
- Al escribir la fecha, se anota en los metadatos el valor original y los IDs de heurística (`EXIF UserComment` en fotos, `QuickTime ©cmt` en vídeos).

---

## F8-US-001 — DTOs y contrato API ✅ COMPLETADO

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

**Criterios cumplidos:**
- [x] DTOs creados en Server/Models y Client/Models
- [x] POST /api/apply responde con ApplyChangesResponse
- [x] IDs no existentes → success: false, error: "NotFound"
- [x] Lista vacía → appliedCount: 0, failedCount: 0
- [x] Null MediaAssetIds → 400 BadRequest
- [x] Tests de integración: 4/4 pasan
- [x] Tags [F8-US-001] en todos los archivos

---

## F8-US-002 — IExifWriter (Infraestructura) ✅ COMPLETADO

### Tareas

**🔴 T-001 — Tests de comportamiento esperable (Janus)**
- Tests unitarios que simulen escritura sobre archivos temporales (mock FS) y cubran: éxito, archivo readonly, formato no soportado.

**🟢 T-002 — Implementación IExifWriter (Kip)**
- Interfaz propuesta:
  ```csharp
  public interface IExifWriter
  {
      Task<ExifWriteResult> WriteAsync(
          string filePath,
          MediaType mediaType,
          DateTime newDate,
          DateTime? originalDate,
          IReadOnlyList<string> heuristicIds,
          CancellationToken ct = default);
  }
  public record ExifWriteResult(bool Success, string? ErrorMessage);
  ```
- Intentar usar API .NET nativa (p. ej. System.Formats) sin dependencias externas. Si no es viable para escritura o ciertos formatos, proponer `ExifLibrary` (MIT) como fallback y documentar la elección.
- La implementación debe:
  1. Escribir la nueva fecha en `EXIF:DateTimeOriginal` (fotos) o `QuickTime:CreateDate` (vídeos).
  2. Escribir la anotación en `EXIF UserComment` (0x9286) para fotos o `QuickTime ©cmt` para vídeos con formato `SnapTime;original=YYYY-MM-DDTHH:mm:ss;heuristics=H-XXX,H-YYY`.
  3. Si `originalDate` es null, escribir `original=unknown`.
  4. Si `heuristicIds` está vacío, escribir `heuristics=none`.
  5. Ambas escrituras en la misma transacción/operación (éxito completo o fallo completo a nivel de archivo).

Acceptance: implementation writes expected EXIF/QuickTime tags + UserComment/©cmt on sample files in integration tests.

**🔵 T-003 — Refactor y review (Gavin)**

**🔴 T-004 — Tests de anotación UserComment/©cmt (Janus)**
- Tests unitarios que verifiquen:
  - Al escribir la fecha, el campo UserComment/©cmt contiene el formato esperado.
  - `originalDate` null produce `original=unknown`.
  - `heuristicIds` vacío produce `heuristics=none`.
  - Múltiples heuristics IDs separados por coma.
- Tests de integración con archivos reales JPEG y MOV/MP4 para verificar que el tag se escribe y se puede leer de vuelta.

Acceptance: tests confirman que la anotación se escribe y parsea correctamente.

**Criterios cumplidos:**
- [x] IExifWriter interfaz creada en Domain/Interfaces con record ExifWriteResult
- [x] ExifWriter implementado en Infrastructure/Services (manipulación binaria EXIF, sin deps externas)
- [x] Escribe EXIF:DateTimeOriginal (tag 0x9003) en JPEG
- [x] Escribe EXIF UserComment (tag 0x9286) con formato SnapTime;original=...;heuristics=...
- [x] originalDate null → "original=unknown"
- [x] heuristicIds vacío → "heuristics=none"
- [x] Múltiples heuristicIds separados por coma
- [x] Archivo inexistente → error "File not found"
- [x] Archivo readonly → error "File is read-only"
- [x] JPEG inválido → error
- [x] Vídeo MP4 → error "not yet implemented" (pendiente QuickTime)
- [x] 10 tests unitarios, todos verdes
- [x] Registro DI: AddScoped<IExifWriter, ExifWriter>
- [x] Tags [F8-US-002] en todos los archivos

---

## F8-US-003 — IApplyService (Domain/Server) 🟢 Implementado (pendiente validación manual)

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
  - Recuperar assets por id desde BD (incluye `SuggestedByHeuristic` y fecha original desde `MetadataEntry` o método análogo).
  - Validar `SuggestionStatus == Approved`. Si no, añadir `ApplyResult` con `Success = false, Error = "NotApproved"`.
  - Para cada asset aprobado:
    - Tomar `SuggestedDate` desde BD. Si nulo → `Error = "MissingSuggestedDate"`.
    - Obtener fecha original: priorizar `EXIF:DateTimeOriginal`/`QuickTime:CreateDate` de `MetadataEntries`, o si no, null.
    - Obtener heuristic IDs desde `MediaAsset.SuggestedByHeuristic` (formato string separado por comas, parsear a lista).
    - Llamar `IExifWriter.WriteAsync(filePath, mediaType, suggestedDate, originalDate, heuristicIds)`.
    - Si éxito: actualizar `MediaAsset.Status` a `Completed` y persistir cambios.
    - Si error: capturar excepción y añadir `ApplyResult` con `Error = ex.Message`.
  - Crear un `AuditEntry` con el resumen (applied/failed/results) y persistir.
  - Antes de iniciar: verificar si existe un ScanJob con estado `Running`; si sí, rechazar la ejecución con 409 Conflict.

Acceptance: Servicio pasa tests de integración y crea AuditEntry. Writer recibe originalDate y heuristicIds correctamente.

**🔵 T-003 — Refactor/optimización (Kip)**

**🔴 T-004 — Tests de integración con anotación (Janus)**
- Tests que verifiquen que `IApplyService` pasa correctamente la fecha original y los heuristic IDs al writer.
- Tests con asset que tiene `SuggestedByHeuristic = "H-001,H-003"` → writer recibe `["H-001", "H-003"]`.
- Tests con asset sin fecha original → writer recibe `originalDate = null`.

---

## F8-US-004 — Endpoint POST /apply (Server) 🟢 Implementado (pendiente validación manual)

### Tareas

**🔴 T-001 — Tests de contrato (Janus)**
- Test que haga POST /apply con una mezcla de ids y verifique el ApplyChangesResponse contiene los `ApplyResult` esperados.

**🟢 T-002 — Implementación Controller (Kip)**
- Implementar `ApplyController` con `POST /apply` que delegue en `IApplyService`.

Acceptance: Integration test con WebApplicationFactory confirma flujo end-to-end.

---

## F8-US-005 — Frontend: ApplyModal.razor (Karris) 🟢 Implementado (pendiente validación manual)

### Tareas

**🔴 T-001 — bUnit Tests (Janus/Karris)**
- Tests para estados: empty, confirmation list, applying, success, errors.

**🟢 T-002 — Implementación (Karris)**
- Modal presenta lista: `FileName`, `Fecha actual` (leer de `MediaAsset.DateTimeOriginal` o `FileMetadata`), `Fecha sugerida` (`SuggestedDate`). Formato `dd/MM/yyyy`. Fecha sugerida en `<strong>`.
- Botón "Aplicar" llama `POST /api/apply` y muestra el resumen final con errores.
- Botón deshabilitado si existe un ScanJob en ejecución (UI obtiene estado jobs para decidir).

Acceptance: bUnit + Playwright E2E valida el flujo.

---

## F8-US-006 — Tests E2E (Janus) 🟢 Implementado (pendiente validación manual)

### Tareas

**🔴 T-001 — E2E exitoso**
- Playwright test que simula assets aprobados y verifica que tras aplicar las fechas se actualizan y modal muestra summary.

**🔴 T-002 — E2E mezcla éxito/error**
- Al menos un asset readonly o sin SuggestedDate; verificar que aparecen en resumen final con motivo.

**🔴 T-003 — E2E bloqueo por scan activo**
- Intentar aplicar mientras hay scan Running: backend responde 409 y UI muestra mensaje.

Acceptance: E2E tests pasan en la suite autónoma (WebApplicationFactory + SQLite efímera).

---

## F8-US-007 — DB / EF migration (Kip) 🟢 Implementado (pendiente validación manual)

### Tareas

**🟢 T-001 — MediaStatus.Completed (opcional)**
- Añadir `Completed` a `MediaStatus` enum si se decide distinguir; si implica cambios en EF, crear migration `F8_AddMediaStatusCompleted`.

Acceptance: migration generada y test de smoke.

---

## F8-US-008 — QA / Code review (Gavin) 🟢 Implementado (pendiente validación manual)

### Tareas

**👁 T-001 — Review completo**
- Revisar código, seguridad, concurrencia, manejo de archivos grandes y bloqueo por scan.

Acceptance: Gavin aprueba y lista comentarios a corregir (si hay).


## Notas finales

- Evitar añadir dependencias sin autorización previa; si la solución nativa no cubre escritura de EXIF/QuickTime, proponer `ExifLibrary` y solicitar validación.
- No ejecutar commits/push sin permiso del usuario (regla reforzada en AGENTS.md).
