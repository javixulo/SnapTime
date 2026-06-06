# F1 — Escaneo y extracción EXIF ✅ COMPLETED

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

**Estado actual:** `GET /api/jobs/{id}` devuelve `ProcessedFiles/TotalFiles/ErrorCount`. Tests de API verifican valores estáticos y de unidad verifican conteos finales. Endpoint ya implementado en F1-US-005.

**Brecha:** No existe ningún test que verifique que el progreso avanza incrementalmente durante la ejecución. Todos los tests chequean estado final o valores fijos mockeados.

**Requisitos precisos:**
- [ ] Test que lanza un job con N archivos (N ≥ 5), cada uno con procesamiento de 1ms, y hace 3+ consultas a `GET /api/jobs/{id}` durante ejecución para verificar que `ProcessedFiles` incrementa progresivamente
- [ ] Test que verifica que `ErrorCount` se refleja en el endpoint durante un job donde algunos archivos fallan
- [ ] Los tests deben acceder al progreso via el endpoint real o via `GetJobAsync`

**Criterios de aceptación:**
- [ ] `ProcessedFiles` en la respuesta de `GET /api/jobs/{id}` avanza de 0 a N durante la ejecución (no solo al final)
- [ ] `ErrorCount` se incrementa y es visible via endpoint mientras el job aún está en `Running`
- [ ] Los tests no dependen de sleeps fijos; usan señales para sincronizar

---

## F1-US-007 — Persistencia de resultados

**Estado actual:** El pipeline guarda en batch de 50 con checkpoints. Errores de persistencia se loguean y el job continúa. Los assets se crean con `MediaAsset.Add()`.

**Brecha:** No existe lógica de upsert por `FilePath` normalizado (`Path.GetFullPath`). `PersistProgressCheckpointAsync` siempre inserta, nunca actualiza. El batch size (50) está hardcodeado.

**Requisitos precisos:**
- [ ] Implementar upsert en `PersistProgressCheckpointAsync`: buscar `MediaAsset` existente por `FilePath` normalizado. Si existe, actualizar `ScanJobId` y reemplazar `MetadataEntries`; si no, insertar nuevo. `MediaType` no debe cambiar en upsert
- [ ] Hacer configurable el batch size: parámetro `batchSize` en constructor de `ScanJobService` con default 50
- [ ] Test: primer scan de carpeta con 3 archivos → se insertan 3 `MediaAsset`
- [ ] Test: segundo scan de misma carpeta → upsert: siguen siendo 3 `MediaAsset`, `ScanJobId` actualizado al nuevo job, metadatos reemplazados
- [ ] Test: error de persistencia simulado incrementa `ErrorCount` y el job continúa (no se detiene)

**Criterios de aceptación:**
- [ ] Dos scans de la misma ruta: la tabla `MediaAssets` tiene exactamente tantas filas como archivos únicos (sin duplicados por `FilePath`)
- [ ] `ScanJobId` se actualiza al último job en cada upsert
- [ ] `MediaType` no cambia en upsert aunque el archivo haya sido reclasificado
- [ ] Batch size configurable desde constructor
- [ ] Fallo en `SaveChangesAsync` no detiene el job; incrementa `ErrorCount` y loguea

---

## F1-US-008 — Tests del scanner ✅ DONE

Completamente implementado en `IDirectoryWalkerTests.cs` (`tests/SnapTime.Tests/Scanner/`). No requiere cambios.

**Casos cubiertos en tests existentes:**
- Directorio con 10 archivos .jpg → 10 archivos devueltos (todos Image)
- Directorio con mezcla .jpg / .mp4 / .mov → 10 archivos (3 Image, 7 Video)
- Directorio con mezcla .jpg / .png / .txt → solo .jpg/.jpeg
- Directorio sin permisos → skip + log, no exception
- Subdirectorios anidados → recorrido completo
- CancellationToken lanzado → interrupción limpia
- Null/empty args → exception

**Mock:** `InMemoryDirectoryWalker` de test.

---

## F1-US-009 — Tests de extracción de metadatos (integración con archivos reales)

**Estado actual:** Existen tests unitarios mock-based (`InMemoryMetadataExtractor`) para: imagen con EXIF, imagen sin metadatos, corrupta, vídeo con QuickTime, vídeo sin metadatos, archivo inexistente, cancellation, null check.

**Brecha:** No existen tests de integración contra el `MetadataExtractorService` real con archivos de muestra reales embedidos. Las US actuales exigen archivos embedidos (JPEG, MOV/MP4) — no existen.

**Requisitos precisos:**
- [ ] Agregar tests que usen el `MetadataExtractorService` real (no mock) con archivos de muestra embedidos como recursos del proyecto de tests:
  - JPEG con EXIF completo (DateTimeOriginal, SubSecTimeOriginal, etc.)
  - JPEG sin metadatos EXIF de fecha
  - MOV o MP4 con metadatos QuickTime completos
  - Archivo corrupto (bytes aleatorios, sin estructura válida)
- [ ] Generar archivos mínimos JPEG/MOV inyectando bytes EXIF/QuickTime conocidos si no es posible embeder archivos reales
- [ ] Test: `DateTimeOriginal` con formato no estándar en archivo real → se parsea como `null` (no crash)
- [ ] Test: archivo de vídeo corrupto → `ExtractAsync` devuelve lista vacía (no crash)

**Criterios de aceptación:**
- [ ] Test: JPEG con EXIF completo devuelve exactamente 4 `MetadataEntry` con `Source="exif"`
- [ ] Test: JPEG sin metadatos devuelve lista vacía
- [ ] Test: MOV/MP4 con QuickTime devuelve hasta 5 `MetadataEntry` con `Source="quicktime"`
- [ ] Formato de fecha no estándar → entry con `Value` presente pero `null` en el campo parseado (no crash)
- [ ] Archivo corrupto (imagen y vídeo) → lista vacía (no exception)
- [ ] Tests usan `MetadataExtractorService` real; tests existentes con `InMemoryMetadataExtractor` se mantienen como unitarios

---

## F1-US-010 — Tests del motor de jobs (integración con mocks reales + InMemory DB)

**Estado actual:** `InMemoryScanJobService` + `IScanJobServiceTests.cs` cubren 7 casos. `BackgroundJobRunner` nunca se testea. Las 3 dependencias se inyectan por separado en el constructor de `InMemoryScanJobService`.

**Brecha:** `InMemoryScanJobService` es un test double que duplica la lógica del pipeline (271 líneas) en vez de probar el `ScanJobService` real con NSubstitute mocks. El `BackgroundJobRunner` (`Channel<ScanJob>` + `IHostedService`) nunca se testea. No hay tests que inyecten mocks de las 3 dependencias en el `ScanJobService` real con InMemory DbContext.

**Requisitos precisos:**
- [ ] NO eliminar tests existentes de `InMemoryScanJobService` (son válidos para verificar el contrato de `IScanJobService`)
- [ ] Agregar test de `BackgroundJobRunner`: encolar un job via `Channel` y verificar que el runner lo procesa (con mock de `IScanJobService` y `ILogger`)
- [ ] Agregar test de `ScanJobService` real usando mocks de `IDirectoryWalker`, `IMetadataExtractor`, `IFileSystemMetadataExtractor` + un `SnapTimeDbContext` en memoria (EF Core InMemory)
- [ ] El test con `ScanJobService` real debe verificar pipeline completo (walk → extract → persist → completed)

**Criterios de aceptación:**
- [ ] `BackgroundJobRunner.StartAsync` procesa un job encolado y llama a `ProcessJobAsync` en `IScanJobService`
- [ ] `ScanJobService` real con mocks completa un job de 3 archivos: estado `Completed`, 3 `MediaAsset` en DB, `ProcessedFiles == 3`
- [ ] `ScanJobService` real con mocks: error en 1 de 3 archivos → `Completed`, `ErrorCount == 1`, `ProcessedFiles == 3`
- [ ] `ScanJobService` real con mocks: cancelación → `Cancelled`, no todos los archivos procesados
- [ ] DbContext InMemory se crea nuevo por test (sin estado compartido entre tests)
