# F2 — SQLite real en tests de integración

> Sustituir EF Core InMemory por SQLite real en `tests/SnapTime.IntegrationTests/`, usando una BD temporal por colección de tests.

**Motivación:** InMemory no soporta transacciones, constraints únicos, ni replica fielmente el comportamiento de SQLite. Usar SQLite real elimina workarounds (`IsRelational()`) y da mayor fidelidad.

**Dependencias:** F1 (proyecto `SnapTime.IntegrationTests` ya existe)

## Diseño

```csharp
[CollectionDefinition("SqliteIntegration")]
public class SqliteIntegrationCollection : ICollectionFixture<SqliteDbFixture> {}

public class SqliteDbFixture : IDisposable
{
    private static readonly string DbPath = Path.Combine(
        Path.GetTempPath(), $"snaptime-test-{Guid.NewGuid()}.db");

    public string ConnectionString => $"Data Source={DbPath}";

    public DbContextOptions<SnapTimeDbContext> Options => new DbContextOptionsBuilder<SnapTimeDbContext>()
        .UseSqlite(ConnectionString)
        .Options;

    public void Dispose()
    {
        try { File.Delete(DbPath); } catch { }
    }
}
```

## US

### F2-US-001 — Fixture SQLite compartida

**Requisitos:**
- [ ] `SqliteDbFixture` con BD temporal, creada al inicio de la colección, eliminada al final
- [ ] `SqliteIntegrationCollection` que agrupa todos los tests de integración
- [ ] Cada clase de test se marca con `[Collection("SqliteIntegration")]`

**Criterios de aceptación:**
- [ ] BD se crea una vez por colección (no por test)
- [ ] BD se elimina al finalizar la colección
- [ ] No hay fugas de archivos `.db` en temp

### F2-US-002 — Migrar tests existentes a SQLite real

**Requisitos:**
- [ ] `MetadataExtractorIntegrationTests` usa `SqliteDbFixture` en vez de InMemory
- [ ] `ScanJobServiceIntegrationTests` usa `SqliteDbFixture` en vez de InMemory
- [ ] `BackgroundJobRunnerIntegrationTests` usa `SqliteDbFixture` en vez de InMemory

**Criterios de aceptación:**
- [ ] Todos los tests pasan con SQLite real
- [ ] Se revierte el `IsRelational()` en `PersistProgressCheckpointAsync` (transacciones reales funcionan)

### F2-US-003 — Limpiar dependencias InMemory

**Requisitos:**
- [ ] Remover `Microsoft.EntityFrameworkCore.InMemory` de `Directory.Packages.props` si no lo usa otro proyecto

**Criterios de aceptación:**
- [ ] Build sin errores
- [ ] Todos los tests pasan (63 unit + 10 integration)

---

**Referencias:**
- `tests/SnapTime.IntegrationTests/`
- `src/SnapTime.Infrastructure/Services/ScanJobService.cs` (revertir `IsRelational()`)
- `Directory.Packages.props`
