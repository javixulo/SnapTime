# F0 — Scaffolding del proyecto

> Crear la estructura base del proyecto .NET, entidades del dominio, persistencia, API base, logging y tooling.

---

## F0-US-001 — Solución y proyectos

Crear la solución SnapTime y los proyectos de la arquitectura limpia.

**Subtareas:**
- Crear carpeta `src/` para proyectos de aplicación y `tests/` para tests
- `dotnet new sln -n SnapTime`
- `dotnet new classlib -n SnapTime.Domain -o src/SnapTime.Domain`
- `dotnet new classlib -n SnapTime.Infrastructure -o src/SnapTime.Infrastructure`
- `dotnet new webapi -n SnapTime.Server -o src/SnapTime.Server`
- `dotnet new blazorwasm -n SnapTime.Client -o src/SnapTime.Client`
- `dotnet new xunit -n SnapTime.Tests -o tests/SnapTime.Tests`
- Añadir todos a la solución: `dotnet sln add src/*/ tests/*/`

**Referencias entre proyectos:**

```
SnapTime.Infrastructure ──→ SnapTime.Domain
SnapTime.Server          ──→ SnapTime.Infrastructure, SnapTime.Domain
SnapTime.Client          ──→ (solo HTTP al Server, sin referencia directa)
SnapTime.Tests           ──→ SnapTime.Server, SnapTime.Infrastructure, SnapTime.Domain
```

**Criterios de aceptación:**
- `dotnet build` compila sin errores.
- La solución tiene exactamente 5 proyectos en las carpetas `src/` y `tests/`.

---

## F0-US-002 — Paquetes NuGet base

Agregar paquetes a cada proyecto.

| Proyecto | Paquete |
|----------|---------|
| Infrastructure | `Microsoft.EntityFrameworkCore.Sqlite`, `MetadataExtractor` |
| Infrastructure | `Serilog`, `Serilog.Extensions.Hosting`, `Serilog.Sinks.Console` |
| Server | (hereda Serilog vía Infrastructure) |
| Tests | `FluentAssertions`, `NSubstitute`, `Microsoft.AspNetCore.Mvc.Testing` |

**Criterios de aceptación:**
- Restore exitoso.
- Sin advertencias de vulnerabilidad.

---

## F0-US-003 — Entidades del dominio ✅ ACTUALIZADO

POCOs en `SnapTime.Domain.Entities/`.

```csharp
// F0-US-003
public enum MediaType { Image, Video }

public class MediaAsset {
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public MediaType MediaType { get; set; }
    public long FileSize { get; set; }
    public DateTime? FileCreatedAt { get; set; }
    public DateTime? FileModifiedAt { get; set; }
    public int ConfidenceScore { get; set; }
    public DateTime? SuggestedDate { get; set; }
    public string? SuggestedByHeuristic { get; set; }
    public MediaStatus Status { get; set; }
    public SuggestionReviewStatus SuggestionStatus { get; set; } = SuggestionReviewStatus.Unreviewed;
    public Guid ScanJobId { get; set; }
    public ScanJob ScanJob { get; set; } = null!;
    public List<MetadataEntry> MetadataEntries { get; set; } = [];
    public List<EvidenceEntry> EvidenceEntries { get; set; } = [];
}

/// <summary>Estado del análisis de la foto. Determina el círculo de color en el grid.</summary>
public enum MediaStatus { Pending, Correct, Error, NoSuggestion, HasSuggestion }

/// <summary>Estado de revisión de la sugerencia (si existe).</summary>
public enum SuggestionReviewStatus { Unreviewed, Approved, Rejected }

public enum EvidenceDirection { Positive, Negative, Correction }

public class MetadataEntry {
    public Guid Id { get; set; }
    public string Tag { get; set; } = string.Empty;  // "Exif SubIFD:DateTime Original", "QuickTime:CreateDate", ...
    public string? Value { get; set; }
    public string Source { get; set; } = string.Empty; // "exif" | "quicktime" | "filesystem"
    public Guid MediaAssetId { get; set; }
    public MediaAsset MediaAsset { get; set; } = null!;
}

public class EvidenceEntry {
    public Guid Id { get; set; }
    public string HeuristicId { get; set; } = string.Empty;
    public string HeuristicName { get; set; } = string.Empty;
    public double Weight { get; set; }
    public EvidenceDirection Direction { get; set; }
    public DateTime? SuggestedDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid MediaAssetId { get; set; }
    public MediaAsset MediaAsset { get; set; } = null!;
}

public class ScanJob {
    public Guid Id { get; set; }
    public JobStatus Status { get; set; }
    public string RootPath { get; set; } = string.Empty;
    public bool IncludeSubfolders { get; set; } = true;
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int ErrorCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<MediaAsset> MediaAssets { get; set; } = [];
}

public enum JobStatus { Running, Paused, Completed, Cancelled, Error }

public class AuditEntry {
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? ScanJobId { get; set; }
    public ScanJob? ScanJob { get; set; }
}
```

**Criterios de aceptación:**
- Compilan sin errores.
- Navegabilidad EF Core configurable (virtual).

**Cambios vs. especificación original:**
- `EvidenceEntry` añadió campo `SuggestedDate` (usado por heurísticas para proponer fecha alternativa).
- `MediaAsset.SuggestionStatus` tiene default `SuggestionReviewStatus.Unreviewed`.
- `ScanJob` añadió `IncludeSubfolders` y `CreatedAt = DateTime.UtcNow`.
- `AuditEntry` añadió `ScanJobId` nullable y `CreatedAt = DateTime.UtcNow`.

---

## F0-US-004 — DbContext + migración inicial

`SnapTimeDbContext` en Infrastructure con DbSets y configuración con Fluent API.

**Incluye:**
- DbSet<MediaAsset>, DbSet<MetadataEntry>, DbSet<EvidenceEntry>, DbSet<ScanJob>, DbSet<AuditEntry>
- Configuraciones de índice (FilePath con unique, ScanJobId)
- ConnectionString construido en DbContext desde `ConfigService.DatabasePath` (file path → `Data Source={path}`)
- Migración inicial con `dotnet ef migrations add InitialCreate` (requiere `dotnet ef` tool instalada)

**Criterios de aceptación:**
- Migration genera SQL correcto.
- `Update-Database` crea las tablas con índices únicos.
- Seed data opcional para desarrollo.

---

## F0-US-005 — ConfigService + snaptime.config.json

Servicio singleton que lee y expone `SnapTimeConfig`.

```csharp
// F0-US-005
public class SnapTimeConfig
{
    public DatabaseConfig Database { get; set; } = new();
    public AnalysisConfig Analysis { get; set; } = new();
    public List<HeuristicConfig> Heuristics { get; set; } = [];
    public OllamaConfig Ollama { get; set; } = new();
    public ThumbnailConfig Thumbnails { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();

    public class DatabaseConfig {
        public string Path { get; set; } = "SnapTime.db";
    }

    public class AnalysisConfig {
        public int ConfidenceThreshold { get; set; } = 70;
        public int MaxConcurrency { get; set; } = 4;
        public int BatchSize { get; set; } = 100;
        public string[] ImageExtensions { get; set; } = [".jpg", ".jpeg"];
        public string[] VideoExtensions { get; set; } = [".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"];
    }

    public class OllamaConfig {
        public string Endpoint { get; set; } = "http://localhost:11434";
        public string Model { get; set; } = "qwen2.5-coder:14b";
        public int TimeoutSeconds { get; set; } = 60;
    }

    public class ThumbnailConfig {
        public int MaxDimension { get; set; } = 300;
        public int Quality { get; set; } = 80;
    }
}

public class LoggingConfig {
    public string Level { get; set; } = "Information";
    public string? File { get; set; }
}

public class HeuristicConfig {
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public double Weight { get; set; } = 1.0;
}
```

**Incluye:**
- Archivo `snaptime.config.json` en la raíz del Server
- ConfigService registrado como singleton en DI
- Validación básica al cargar

**Criterios de aceptación:**
- ConfigService devuelve valores por defecto si falta el archivo.
- Se puede inyectar en otros servicios.

---

## F0-US-006 — API base + health endpoint

Program.cs mínimo en SnapTime.Server.

```
GET /api/health → 200 { "status": "ok", "timestamp": "..." }
```

**Incluye:**
- CORS habilitado para Blazor WASM (`https://localhost:5001` por defecto, configurable)
- Swagger (solo en desarrollo)
- ConfigService registrado
- DbContext registrado
- Serilog como logger

**Criterios de aceptación:**
- `curl localhost:5000/api/health` devuelve 200.
- Log de inicio en consola.

---

## F0-US-007 — Test de health endpoint

Integration test con `Microsoft.AspNetCore.Mvc.Testing`.

```csharp
// [F0-US-007]
public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>> {
    [Fact]
    public async Task GetHealth_ReturnsOk() {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

**Criterios de aceptación:**
- Test pasa en CI.
- Usa `WebApplicationFactory` real (no mock).
- **Nota:** requiere que `Program.cs` sea `public partial class Program` o añadir `InternalsVisibleTo` en el Server para que el test project pueda acceder.

---

## F0-US-008 — Serilog en toda la solución

Configuración centralizada de Serilog.

**Incluye:**
- LoggerConfiguration en Infrastructure
- Mínimo nivel: Information (Debug en desarrollo)
- Console sink con formato estructurado
- Enrich con machine name, thread, etc.

**Criterios de aceptación:**
- Trazas de EF Core visibles en consola.
- Nivel configurable desde `snaptime.config.json`.

---

## F0-US-009 — Build y tooling

Scripts y configuraciones para el día a día.

**Incluye:**
- `dotnet watch run --project src/SnapTime.Server` en launchSettings
- `dotnet watch test --project tests/SnapTime.Tests`
- `global.json` con versión de SDK (10.0 / 8.0 fallback)
- `Directory.Packages.props` para Central Package Management (todas las versiones NuGet en un solo lugar)
- `.editorconfig` con reglas del equipo (var, braces, etc.)
- `Directory.Build.props` con ImplicitUsings, Nullable enable, LangVersion latest

**Criterios de aceptación:**
- `dotnet build` sin warnings.
- Watch mode recarga correctamente.

---

## F0-US-010 — Infraestructura de tests para la UI (bUnit + Playwright) ✅ COMPLETADO

> Preparar los proyectos y dependencias necesarias para testear la UI Blazor, tanto a nivel de componentes como end-to-end.

**Estado actual:** Implementado y en la solución.

**Lo que incluye:**

1. **Proyecto bUnit** — `tests/SnapTime.Client.Tests/`:
   - SDK: `Microsoft.NET.Sdk.Razor`
   - Paquete: `bunit`
   - Referencia a `SnapTime.Client`
   - Smoke test: `Home_LoadsAndDisplaysLayout` en `HomePageTests.cs`

2. **Proyecto Playwright** — `tests/SnapTime.E2ETests/`:
   - SDK: `Microsoft.NET.Sdk`
   - Paquete: `Microsoft.Playwright.NUnit`
   - Smoke test: `HomePage_LoadsAndContainsSnapTimeInTitle` en `HomePageE2ETests.cs`

3. **Fixture E2E autónomo** — `E2EWebFixture.cs`:
   - Arranca Server + Client en puertos aleatorios
   - Usa base de datos SQLite efímera
   - Parada automática al finalizar
   - Restaura configuración original al terminar

4. **bUnit tests existentes**:
   - `FolderTreePanelTests.cs` (7 tests: carga, error, vacío, selección, multi-selección)
   - `PhotoGridTests.cs` (14 tests: carga, subcarpetas, breadcrumb, status circles, video badge, tooltip, selección)
   - `PhotoDetailTests.cs` (8 tests: placeholder, metadatos escaneado/no escaneado, evidencias, confidence bar)
   - `ScanPanelTests.cs`, `PhotoDetailF7Tests.cs`, `BatchActionsF7Tests.cs`, `FolderTreeItemTests.cs`
   - `HomePageTests.cs` (2 tests: layout, paso de SelectedAssetPath)

5. **E2E tests existentes**:
   - `FolderTreeE2ETests.cs`, `PhotoGridE2ETests.cs`, `PhotoDetailE2ETests.cs`
   - `ScanJobE2ETests.cs`, `ScanPanelE2ETests.cs`, `ReviewE2ETests.cs`, `HomePageE2ETests.cs`

**Nota:** Algunos tests bUnit pueden fallar porque fueron escritos en fase RED (TDD) antes de la implementación GREEN completa. Es esperado y se corrige en el pipeline de la feature correspondiente.

**Criterios de aceptación:**
- [x] `dotnet test tests/SnapTime.Client.Tests` compila (smoke test de bUnit).
- [x] `dotnet test tests/SnapTime.E2ETests` compila (smoke test de Playwright, requiere browsers instalados).
- [x] Ambos proyectos añadidos a la solución.
- [x] Fixture E2E autónoma implementada.
