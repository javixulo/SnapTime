# SnapTime — Contratos API REST

## 1) Convenciones generales

- **Base URL**: `/api`
- **Formato**: JSON (System.Text.Json, camelCase)
- **Paginación**: query params `?page=1&pageSize=50`. PageSize valores: 20, 50, 100. `pageSize=0` = todas (lazy loading).
- **Errores**: `{ "error": { "code": "string", "message": "string", "details": {}? } }`
- **Códigos**: 200 OK, 201 Created, 400 Bad Request, 404 Not Found, 409 Conflict, 500 Internal
- **Autenticación**: No aplica (local-first, solo localhost)
- **Health**: `GET /health` → 200 `"OK"`, 503 `"Degraded"` (sin DTO)

## 2) DTOs compartidos

```csharp
// SnapTime.Client/Models/ (y SnapTime.Server/Models/)

public enum MediaType { Image, Video }

public enum SelectionState { Selected, None, Partial }

public enum MediaStatus { Pending, Correct, Error, NoSuggestion, HasSuggestion }

public enum JobStatus { Running, Paused, Completed, Cancelled, Error }

public record MediaAssetDto(
    Guid Id,
    string FilePath,
    string FileName,
    MediaType MediaType,
    DateTime? DateTimeOriginal,
    DateTime? SuggestedDate,
    int ConfidenceScore,
    string? SuggestedByHeuristic,
    MediaStatus Status
);

public record MediaAssetDetailDto(
    Guid Id,
    string FilePath,
    string FileName,
    MediaType MediaType,
    long FileSize,
    DateTime? DateTimeOriginal,
    string? SubSecDateTimeOriginal,
    DateTime? CreateDate,
    DateTime? ModifyDate,
    DateTime? FileCreatedAt,    // ctime del sistema de archivos
    DateTime? FileModifiedAt,   // mtime del sistema de archivos
    int ConfidenceScore,
    DateTime? SuggestedDate,
    string? SuggestedByHeuristic,
    MediaStatus Status,
    List<EvidenceDto> Evidence
);

public record FileMetadataDto(
    string FilePath,
    string FileName,
    long FileSize,
    DateTime? DateTimeOriginal,
    string? SubSecDateTimeOriginal,
    DateTime? CreateDate,
    DateTime? ModifyDate,
    DateTime? FileCreatedAt,
    DateTime? FileModifiedAt
);

public record EvidenceDto(
    string HeuristicId,
    string HeuristicName,
    double Weight,
    string Direction,  // "positive" | "negative"
    string Description
);

public record JobDto(
    Guid Id,
    JobStatus Status,
    string RootPath,
    int TotalFiles,
    int ProcessedFiles,
    int ErrorCount,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

public record FolderTreeNodeDto(
    string Path,
    string Name,
    bool HasSubfolders,
    int? FileCount,
    SelectionState Selection
);

public record PaginatedResponse<T>(
    List<T> Items,
    int Total,
    int Page,
    int PageSize
);

public record BatchReviewRequest(
    List<Guid> MediaAssetIds,
    string Action  // "approve" | "reject"
);

public record ApplyChangesRequest(
    List<Guid> MediaAssetIds,
    bool DryRun  // true = simulación, false = escritura real
);

public record ApplyChangesResponse(
    List<ApplyResult> Results
);

public record ApplyResult(
    Guid MediaAssetId,
    bool Success,
    string? Error
);

public record ChatRequest(
    string Message
);

public record ChatResponse(
    string Reply
);

public record ConfigUpdateRequest(
    int? ConfidenceThreshold,
    List<HeuristicConfigDto>? Heuristics
);

public record HeuristicConfigDto(
    string Id,
    bool? Enabled,
    double? Weight
);
```

> `SnapTimeConfig` (response de `GET/PUT /config`) está definido en [`docs/08-configuracion.md`](08-configuracion.md).

## 3) Endpoints

### Jobs

| Método | Ruta | Request | Response | Descripción |
|--------|------|---------|----------|-------------|
| POST | `/jobs` | `{ rootPath: string }` | `JobDto` (201) | Crear y arrancar job de análisis |
| GET | `/jobs` | — | `List<JobDto>` | Listar jobs |
| GET | `/jobs/{id}` | — | `JobDto` | Estado y progreso |
| POST | `/jobs/{id}/pause` | — | `JobDto` | Pausar |
| POST | `/jobs/{id}/resume` | — | `JobDto` | Reanudar |
| POST | `/jobs/{id}/cancel` | — | `JobDto` | Cancelar |

### Carpetas

| Método | Ruta | Request | Response | Descripción |
|--------|------|---------|----------|-------------|
| GET | `/folders/tree` | — | `List<FolderTreeNodeDto>` | Árbol completo con estado |
| POST | `/folders/selection` | `{ path: string, selected: bool }` | `List<FolderTreeNodeDto>` | Cambiar selección (cascada) |

### Archivos multimedia

| Método | Ruta | Request Query | Response | Descripción |
|--------|------|---------------|----------|-------------|
| GET | `/media-assets` | `folderPath?`, `mediaType?`, `minConfidence?`, `maxConfidence?`, `status?`, `sortBy?`, `sortDir?`, `page`, `pageSize` | `PaginatedResponse<MediaAssetDto>` | Listado paginado con filtros |
| GET | `/media-assets/{id}` | — | `MediaAssetDetailDto` | Detalle con evidencia (solo escaneados). |
| GET | `/media-assets/from-file` | `path` (string, required) | `FileMetadataDto` | Metadatos del archivo leídos directamente del disco (EXIF + filesystem). Sin BD. Funciona para cualquier archivo, escaneado o no. |
| GET | `/thumbnails/{assetId}` | — | `FileStream` | Miniatura desde un asset escaneado. Busca el asset en BD y sirve el archivo del disco. |
| GET | `/thumbnails/from-file` | `path` (string, required) | `FileStream` | Miniatura de cualquier archivo. Sin dependencia de BD. Lee el archivo directamente del sistema de archivos. |
| GET | `/thumbnails/placeholder` | — | `image/png` | Placeholder gris para directorios o archivos no soportados. |
| GET | `/video/stream` | `path` (string, required) | `FileStream` | Stream de vídeo con `Content-Type` correcto (`video/mp4`, `video/quicktime`, etc.). Usado por `<video>` del frontend para mostrar primer frame como thumbnail. Sin BD. |

### Revisión y aplicación

| Método | Ruta | Request | Response | Descripción |
|--------|------|---------|----------|-------------|
| POST | `/reviews/batch` | `BatchReviewRequest` | `List<Guid>` (200) | Aprobar/rechazar en lote. Devuelve los IDs actualizados. |
| POST | `/apply` | `ApplyChangesRequest` | `ApplyChangesResponse` | Dry-run o aplicación real |

### Chat

| Método | Ruta | Request | Response | Descripción |
|--------|------|---------|----------|-------------|
| POST | `/chat` | `ChatRequest` | `ChatResponse` | Mensaje al LLM con tool calling |

### Configuración

| Método | Ruta | Request | Response | Descripción |
|--------|------|---------|----------|-------------|
| GET | `/config` | — | `SnapTimeConfig` | Config actual |
| PUT | `/config` | `ConfigUpdateRequest` | `SnapTimeConfig` | Actualizar en runtime |

## 4) Notas técnicas

- **Progreso de jobs**: el frontend puede hacer polling a `GET /jobs/{id}`. Para Fase 2 considerar SSE si el polling es insuficiente.
- **Miniaturas**: el servidor sirve el archivo original directamente del sistema de archivos. No hay dependencia de base de datos ni generación de thumbnails redimensionados por ahora. Para vídeos no hay thumbnail (solo el placeholder gris). En una fase futura se podrá añadir generación y caché en disco.
- **Lazy loading**: cuando `pageSize=0`, el backend devuelve todos los archivos en una sola respuesta. El frontend usa `Virtualize` para el render.
- **MCP tools** tienen contratos separados definidos en `docs/03-blueprint.md §3`.
