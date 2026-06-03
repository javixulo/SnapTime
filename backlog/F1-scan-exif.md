# F1 — Escaneo y extracción EXIF

> Escanear directorios seleccionados, extraer metadatos EXIF + filesystem y persistir en SQLite. Job engine con progreso, pause/resume/cancel.

---

## F1-US-001 — Scanner de directorios

Walk recursivo del sistema de archivos desde una ruta raíz, soportando imágenes y vídeos.

**Interfaz:**
```csharp
// [F1-US-001]
public interface IDirectoryWalker {
    IAsyncEnumerable<FileInfo> WalkAsync(
        string rootPath,
        string[] imageExtensions,
        string[] videoExtensions,
        CancellationToken ct);
}
```

**Reglas:**
- Archivos de imagen: extensiones en `imageExtensions` (por defecto `.jpg`, `.jpeg`)
- Archivos de vídeo: extensiones en `videoExtensions` (por defecto `.mp4`, `.mov`, `.avi`, `.mkv`, `.webm`, `.m4v`)
- Omite directorios sin permiso de lectura (log + sigue)
- Reporta archivos descartados con motivo (extensión no soportada, permisos, ausente)
- Devuelve `FileInfo` con ruta completa, tamaño y extensión (para determinar `MediaType`)

**Criterios de aceptación:**
- WalkAsync recorre subdirectorios recursivamente.
- Salta archivos no .jpg/.jpeg.
- No lanza excepción si un subdirectorio no es accesible (log + skip).
- Cancellation interrumpe antes del próximo archivo.

---

## F1-US-002 — Extracción de metadatos (EXIF + QuickTime)

Extraer metadatos de fecha de cada archivo. Usa EXIF para imágenes y QuickTime para vídeos.

**Interfaz:**
```csharp
// [F1-US-002]
public interface IMetadataExtractor {
    Task<List<MetadataEntry>> ExtractAsync(string filePath, MediaType mediaType, CancellationToken ct);
}
```

**Tags a extraer por tipo:**

| Tipo | Directorio MetadataExtractor | Tags |
|------|------------------------------|------|
| Image | `ExifSubIfdDirectory` | DateTime Original, Sub Sec Time Original |
| Image | `ExifIfd0Directory` | Date/Time Digitized, Date/Time |
| Video | `QuickTimeMovieHeaderDirectory` | Create Date, Modify Date |
| Video | `QuickTimeMetadataDirectory` | Creation Date |
| Video | `QuickTimeTrackHeaderDirectory` | Track Create Date, Track Modify Date |

**Mapeo de nombres MetadataExtractor — Imágenes:**

| Tag lógico | Directory | Tag Name |
|------------|-----------|----------|
| DateTimeOriginal | Exif SubIFD | DateTime Original |
| SubSecDateTimeOriginal | Exif SubIFD | Sub Sec Time Original |
| CreateDate | Exif IFD0 | Date/Time Digitized |
| ModifyDate | Exif IFD0 | Date/Time |

**Mapeo de nombres MetadataExtractor — Vídeos:**

| Tag lógico | Directory | Tag Name |
|------------|-----------|----------|
| QuickTime:CreateDate | QuickTime Movie Header | Create Date |
| QuickTime:ModifyDate | QuickTime Movie Header | Modify Date |
| QuickTime:CreationDate | QuickTime Metadata | Creation Date |
| QuickTime:MediaCreateDate | QuickTime Media Header | Media Create Date |
| QuickTime:TrackCreateDate | QuickTime Track Header | Track Create Date |

**Reglas:**
- Usar MetadataExtractor con el directorio adecuado según `MediaType`
- Parsear a `DateTime?`. Si el tag no existe o no se puede parsear → `null`
- El tag `MetadataEntry.Tag` se compone como `"{DirectoryName}:{TagName}"` (ej: `"Exif SubIFD:DateTime Original"`, `"QuickTime Movie Header:Create Date"`)
- Devolver `List<MetadataEntry>` con `Source = "exif"` para imágenes y `Source = "quicktime"` para vídeos

**Criterios de aceptación:**
- Extrae correctamente los 4 tags EXIF de una imagen con metadatos completos.
- Extrae correctamente los 5 tags QuickTime de un vídeo con metadatos completos.
- Devuelve lista vacía si el archivo (imagen o vídeo) no tiene metadatos (no lanza).
- Soporta JPEG estándar y contenedores MOV/MP4.

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
2. Determinar `MediaType` por extensión: si está en `imageExtensions` → `Image`, si está en `videoExtensions` → `Video`
3. Metadatos → `List<MetadataEntry>` (EXIF o QuickTime según MediaType)
4. Filesystem → `List<MetadataEntry>`
5. Crear `MediaAsset` + `MetadataEntries` → persistir en SQLite
6. Incrementar `ProcessedFiles`

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

Cada archivo escaneado + sus metadatos se guardan en SQLite.

```csharp
// [F1-US-007] — dentro del pipeline
await _dbContext.MediaAssets.AddAsync(mediaAsset, ct);
await _dbContext.MetadataEntries.AddRangeAsync(mediaAsset.MetadataEntries, ct);
await _dbContext.SaveChangesAsync(ct);
```

**Reglas:**
- Guardar `MediaAsset` y metadatos en la misma transacción.
- Si falla el guardado → errorCount++ y se registra el error, no se detiene el job.
- Batch de 50 archivos antes de SaveChanges para evitar transacciones largas (configurable).
- Upsert por `FilePath` normalizado (`Path.GetFullPath`): misma ruta absoluta → misma entidad. Si el archivo ya existe, se actualizan metadatos y `ScanJobId`.
- `MediaType` se determina por extensión al insertar y no cambia en upserts.

**Criterios de aceptación:**
- Los archivos aparecen en SQLite después del scan.
- Los metadatos están linkeados al archivo correcto.
- Dos scans de la misma carpeta: upsert por `FilePath` (no duplicados).

---

## F1-US-008 — Tests del scanner

```csharp
// [F1-US-008]
```

**Casos:**
- Directorio con 10 archivos .jpg → 10 archivos devueltos (todos Image)
- Directorio con mezcla .jpg / .mp4 / .mov → 10 archivos (3 Image, 7 Video)
- Directorio con mezcla .jpg / .png / .txt → solo .jpg/.jpeg
- Directorio sin permisos → skip + log, no exception
- Subdirectorios anidados → recorrido completo
- CancellationToken lanzado → interrupción limpia

**Mock:** `IDirectoryWalker` con un `InMemoryDirectoryWalker` de test que devuelve lista en memoria.

---

## F1-US-009 — Tests de extracción de metadatos

```csharp
// [F1-US-009]
```

**Casos:**
- Imagen con todos los tags EXIF → 4 MetadataEntry con Source="exif"
- Imagen sin metadatos → lista vacía
- Archivo de imagen corrupto → lista vacía, log de error
- DateTimeOriginal con formato no estándar → null (no crash)
- Vídeo MOV con metadatos QuickTime completos → 5 MetadataEntry con Source="quicktime"
- Vídeo MP4 sin metadatos → lista vacía
- Archivo de vídeo corrupto → lista vacía, log de error

**Mock:** `IMetadataExtractor` con archivos de muestra embedidos (JPEG y MOV/MP4).

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

**Mock:** `IDirectoryWalker` + `IMetadataExtractor` + `IFileSystemMetadataExtractor` inyectados como mocks.
