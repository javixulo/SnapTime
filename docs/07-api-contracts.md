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

public enum SelectionState { Selected, None, Partial }

public enum PhotoStatus { Pending, Review, Approved, Rejected }

public enum JobStatus { Running, Paused, Completed, Cancelled, Error }

public record PhotoDto(
    Guid Id,
    string FilePath,
    string FileName,
    DateTime? DateTimeOriginal,
    DateTime? SuggestedDate,
    int ConfidenceScore,
    string? SuggestedByHeuristic,
    PhotoStatus Status
);

public record PhotoDetailDto(
    Guid Id,
    string FilePath,
    string FileName,
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
    List<EvidenceDto> Evidence
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
    List<Guid> PhotoIds,
    string Action  // "approve" | "reject"
);

public record ApplyChangesRequest(
    List<Guid> PhotoIds,
    bool DryRun  // true = simulación, false = escritura real
);

public record ApplyChangesResponse(
    List<ApplyResult> Results
);

public record ApplyResult(
    Guid PhotoId,
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

### Fotos

| Método | Ruta | Request Query | Response | Descripción |
|--------|------|---------------|----------|-------------|
| GET | `/photos` | `folderPath?`, `minConfidence?`, `maxConfidence?`, `status?`, `sortBy?`, `sortDir?`, `page`, `pageSize` | `PaginatedResponse<PhotoDto>` | Listado paginado con filtros |
| GET | `/photos/{id}` | — | `PhotoDetailDto` | Detalle con evidencia |
| GET | `/thumbnails/{photoId}` | `maxDimension?` (default 300) | `FileStream` (image/jpeg) | Miniatura bajo demanda |

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
- **Miniaturas**: el servidor las genera bajo demanda y las cachea en disco (`thumbnails/`). No se sirven desde SQLite.
- **Lazy loading**: cuando `pageSize=0`, el backend devuelve todas las fotos en una sola respuesta. El frontend usa `Virtualize` para el render.
- **MCP tools** tienen contratos separados definidos en `docs/03-blueprint.md §3`.
