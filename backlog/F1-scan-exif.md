# F1 — Escaneo y extracción EXIF

> Escanear directorios seleccionados, extraer metadatos EXIF + filesystem y persistir en SQLite. Job engine con progreso, pause/resume/cancel.

---

## F1-US-001 — Scanner de directorios

Walk recursivo del sistema de archivos desde una ruta raíz.

**Interfaz:**
```csharp
// [F1-US-001]
public interface IDirectoryWalker {
    IAsyncEnumerable<FileInfo> WalkAsync(
        string rootPath,
        string[] allowedExtensions,
        CancellationToken ct);
}
```

**Reglas:**
- Solo archivos con extensión en `allowedExtensions` (por defecto `.jpg`, `.jpeg`)
- Omite directorios sin permiso de lectura (log + sigue)
- Reporta archivos descartados con motivo (extensión, permisos, ausente)
- Devuelve `FileInfo` con ruta completa y tamaño

**Criterios de aceptación:**
- WalkAsync recorre subdirectorios recursivamente.
- Salta archivos no .jpg/.jpeg.
- No lanza excepción si un subdirectorio no es accesible (log + skip).
- Cancellation interrumpe antes del próximo archivo.

---

## F1-US-002 — Extracción EXIF

Extraer metadatos de fecha EXIF de cada archivo.

**Interfaz:**
```csharp
// [F1-US-002]
public interface IExifExtractor {
    Task<List<MetadataEntry>> ExtractAsync(string filePath, CancellationToken ct);
}
```

**Tags a extraer:**
| Tag EXIF | Propiedad |
|----------|-----------|
| `EXIF:DateTimeOriginal` | Fecha de captura original |
| `EXIF:SubSecDateTimeOriginal` | Fecha con subsegundos |
| `EXIF:CreateDate` | Fecha de creación digital |
| `EXIF:ModifyDate` | Fecha de modificación digital |

**Reglas:**
- Usar MetadataExtractor (librería .NET)
- Parsear a `DateTime?`. Si el tag no existe o no se puede parsear → `null`
- El tag `MetadataEntry.Tag` se compone como `"{DirectoryName}:{TagName}"` (ej: `"Exif SubIFD:DateTimeOriginal"`)
- Devolver `List<MetadataEntry>` con `Source = "exif"`

**Mapeo de nombres MetadataExtractor:**

| Tag lógico | Directory de MetadataExtractor | Tag Name |
|------------|-------------------------------|----------|
| DateTimeOriginal | Exif SubIFD | DateTime Original |
| SubSecDateTimeOriginal | Exif SubIFD | Sub Sec Time Original |
| CreateDate | Exif IFD0 | Date/Time Digitized |
| ModifyDate | Exif IFD0 | Date/Time |

**Criterios de aceptación:**
- Extrae correctamente los 4 tags de una imagen con metadatos completos.
- Devuelve lista vacía si la imagen no tiene metadatos (no lanza).
- Soporta JPEG estándar.

---

## F1-US-003 — Metadatos del filesystem

Extraer fechas del sistema de archivos como evidencia secundaria.

```csharp
// [F1-US-003]
public interface IFileSystemMetadataExtractor {
    List<MetadataEntry> ExtractFileSystemDates(string filePath);
}
```

**Tags:**
| Tag | Descripción |
|-----|-------------|
| `Filesystem:ctime` | Creation time |
| `Filesystem:mtime` | Last write (modify) time |

**Criterios de aceptación:**
- Devuelve ambas fechas en `MetadataEntry` con `Source = "filesystem"`.
- Usa `File.GetCreationTime` / `File.GetLastWriteTime`.

---

## F1-US-004 — Motor de jobs (worker pipeline)

Pipeline asíncrono que orquesta scan → EXIF → persistencia.

**Background runner:** un `IHostedService` + `Channel<ScanJob>` desencola jobs y los ejecuta en segundo plano. `CreateJobAsync` encola el job y devuelve inmediatamente; el runner procesa en background.

```csharp
// [F1-US-004]
public interface IScanJobService {
    Task<ScanJob> CreateJobAsync(string rootPath);
    Task<ScanJob> GetJobAsync(Guid jobId);
    Task<List<ScanJob>> GetAllJobsAsync();
    Task PauseJobAsync(Guid jobId);
    Task ResumeJobAsync(Guid jobId);
    Task CancelJobAsync(Guid jobId);
}

public interface IBackgroundJobRunner : IHostedService {
    // Procesa jobs del Channel<ScanJob>, actualiza progreso en memoria
}
```

**Estados:**
```
Running ←→ Paused
   ↓
Completed | Cancelled | Error
```

**Pipeline por archivo:**
1. Walk → obtiene `FileInfo`
2. EXIF → `List<MetadataEntry>`
3. Filesystem → `List<MetadataEntry>`
4. Crear `Photo` + `MetadataEntries` → persistir en SQLite
5. Incrementar `ProcessedFiles`

**Reglas:**
- Cooperative cancellation via `CancellationToken`
- Checkpoint cada 50 archivos (guardar estado del job en DB)
- Progreso en memoria (`ConcurrentDictionary<Guid, JobProgress>`) para lecturas rápidas sin consultar DB
- Persistir progreso a DB solo en checkpoints y al completar/cancelar
- Todos los cambios de estado se registran como `AuditEntry`
- Al pausar: termina el archivo actual, no arranca el siguiente

**Criterios de aceptación:**
- Ciclo completo: create → running → completed.
- Pause → resume continúa desde donde iba.
- Cancel interrumpe en el próximo checkpoint.
- Errores individuales no detienen el job (log + errorCount++).

---

## F1-US-005 — API endpoints de jobs

Endpoints REST para control de jobs.

| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/api/jobs` | Body `{ rootPath: string }` → crea y arranca |
| GET | `/api/jobs` | Lista todos los jobs |
| GET | `/api/jobs/{id}` | Estado y progreso |
| POST | `/api/jobs/{id}/pause` | Pausar |
| POST | `/api/jobs/{id}/resume` | Reanudar |
| POST | `/api/jobs/{id}/cancel` | Cancelar |

**DTOs definidos en** `docs/07-api-contracts.md`.

**Validaciones:**
- `rootPath` debe existir y ser un directorio accesible → 400 si no
- `rootPath` vacío o nulo → 400
- Si ya hay un job running para la misma ruta → 409 Conflict

**Criterios de aceptación:**
- POST `/api/jobs` devuelve 201 con `JobDto`.
- POST `/api/jobs` con ruta inválida devuelve 400.
- GET `/api/jobs/{id}` devuelve progreso actualizado.
- POST pause/resume/cancel devuelven 200 con el job actualizado.
- Job inexistente → 404.

---

## F1-US-006 — Progreso en tiempo real

`GET /api/jobs/{id}` expone `ProcessedFiles/TotalFiles/ErrorCount`.

**Incluye:**
- Endpoint ya implementado en F1-US-005
- Tests que verifican que el progreso avanza durante un scan real con mock de IExifExtractor
- Desde UI se hará polling cada 2s (contrato, implementación en F3/F4)

**Criterios de aceptación:**
- Después de procesar 5 fotos, `processedFiles == 5`.
- `errorCount` se incrementa cuando un archivo falla.

---

## F1-US-007 — Persistencia de resultados

Cada foto escaneada + sus metadatos se guardan en SQLite.

```csharp
// [F1-US-007] — dentro del pipeline
await _dbContext.Photos.AddAsync(photo, ct);
await _dbContext.MetadataEntries.AddRangeAsync(photo.MetadataEntries, ct);
await _dbContext.SaveChangesAsync(ct);
```

**Reglas:**
- Guardar foto y metadatos en la misma transacción.
- Si falla el guardado → errorCount++ y se registra el error, no se detiene el job.
- Batch de 50 fotos antes de SaveChanges para evitar transacciones largas (configurable).
- Upsert por `FilePath` normalizado ( `Path.GetFullPath` ): misma ruta absoluta → misma entidad. Si la foto ya existe, se actualizan metadatos y se incrementa `ScanJobId`.

**Criterios de aceptación:**
- Las fotos aparecen en SQLite después del scan.
- Los metadatos están linkeados a la foto correcta.
- Dos scans de la misma carpeta: upsert por `FilePath` (no duplicados).

---

## F1-US-008 — Tests del scanner

```csharp
// [F1-US-008]
```

**Casos:**
- Directorio con 10 fotos .jpg → 10 archivos devueltos
- Directorio con mezcla .jpg / .png / .txt → solo .jpg/.jpeg
- Directorio sin permisos → skip + log, no exception
- Subdirectorios anidados → recorrido completo
- CancellationToken lanzado → interrupción limpia

**Mock:** `IDirectoryWalker` con un `InMemoryDirectoryWalker` de test que devuelve lista en memoria.

---

## F1-US-009 — Tests de extracción EXIF

```csharp
// [F1-US-009]
```

**Casos:**
- Imagen con todos los tags → 4 MetadataEntry
- Imagen sin metadatos → lista vacía
- Archivo corrupto → lista vacía, log de error
- DateTimeOriginal con formato no estándar → null (no crash)

**Mock:** `IExifExtractor` con imágenes de muestra embedidas.

---

## F1-US-010 — Tests del motor de jobs

```csharp
// [F1-US-010]
```

**Casos:**
- Crear job → estado Running
- Job completo → estado Completed, processedFiles == totalFiles
- Pause → estado Paused, reanudar → estado Running
- Cancel → estado Cancelled, pipeline no procesa más archivos
- Error en un archivo → errorCount++, no se detiene el job
- rootPath inválido → estado Error con mensaje

**Mock:** `IDirectoryWalker` + `IExifExtractor` + `IFileSystemMetadataExtractor` inyectados como mocks.
