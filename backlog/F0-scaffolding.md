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

## F0-US-003 — Entidades del dominio

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
    public Guid ScanJobId { get; set; }
    public ScanJob ScanJob { get; set; } = null!;
    public List<MetadataEntry> MetadataEntries { get; set; } = [];
    public List<EvidenceEntry> EvidenceEntries { get; set; } = [];
}

public enum MediaStatus { Pending, Review, Approved, Rejected }

public enum EvidenceDirection { Positive, Negative }

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
    public string Description { get; set; } = string.Empty;
    public Guid MediaAssetId { get; set; }
    public MediaAsset MediaAsset { get; set; } = null!;
}

public class ScanJob {
    public Guid Id { get; set; }
    public JobStatus Status { get; set; }
    public string RootPath { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int ErrorCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<MediaAsset> MediaAssets { get; set; } = [];
}

public enum JobStatus { Running, Paused, Completed, Cancelled, Error }

public class AuditEntry {
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
```

**Criterios de aceptación:**
- Compilan sin errores.
- Navegabilidad EF Core configurable (virtual).

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
public class SnapTimeConfig {
    public string DatabasePath { get; set; } = "snaptime.db";  // ruta al archivo SQLite
    public int ConfidenceThreshold { get; set; } = 80;
    public string[] ImageExtensions { get; set; } = [".jpg", ".jpeg"];
    public string[] VideoExtensions { get; set; } = [".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"];
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama3.2";
    public List<HeuristicConfig> Heuristics { get; set; } = [];
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
