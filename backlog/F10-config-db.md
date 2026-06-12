# F10 — Configuración runtime en BD

> Migrar la configuración de análisis, ollama, thumbnails y heurísticas del JSON (`snaptime.config.json`) a tablas SQLite (`Settings` + `HeuristicConfig`). El JSON se reduce a bootstrap mínimo: `database.path` y `logging`.

**Referencias:** docs/08-configuracion.md, docs/07-api-contracts.md, F0-US-005

**Dependencias:** F0 (ConfigService ya existe como singleton), F2 (SQLite + EF listos)

---

## Visión general

### Problema actual
- `snaptime.config.json` es la única fuente de verdad de toda la configuración.
- Los tests E2E modifican este archivo directamente en `src/SnapTime.Server/`, corrompiéndolo si el proceso crashea.
- `ConfigService` no puede separar bootstrap (necesario antes de BD) de runtime (modificable en caliente).

### Solución
Modelo híbrido:

| Nivel | Medio | Contenido |
|-------|-------|-----------|
| Bootstrap | `snaptime.config.json` | `database.path`, `logging.*` |
| Runtime | `Settings` + `HeuristicConfig` (SQLite) | analysis, ollama, thumbnails, heuristics |

### Startup de ConfigService
1. Lee JSON bootstrap → obtiene `database.path`.
2. Conecta a SQLite.
3. Si `Settings` está vacía (BD nueva), seed con defaults.
4. Si `HeuristicConfig` está vacía, seed con las 6 heurísticas baseline.
5. Expone `Current` combinando bootstrap + runtime.

### Migración automática
Si al arrancar el JSON contiene secciones `analysis`, `ollama`, `thumbnails` o `heuristics`, `ConfigService` las migra a BD y las elimina del JSON.

---

## F10-US-001 — Entidades de dominio para configuración runtime

### Tareas

**🔴 T-001 — Tests de entidades (Janus)**
- `Settings` entity: valores por defecto desde constantes, propiedad Id = 1, clamped ranges.
- `HeuristicConfigEntity` (si no existe): Id PK string, Enabled, Weight.
- Verificar que seed produce filas en BD.

**🟢 T-002 — Implementar entidades (Kip)**
- `Settings` en `SnapTime.Domain/Entities/` con columnas de `docs/08-configuracion.md §2`.
- `HeuristicConfigEntity` en `SnapTime.Domain/Entities/`.
- DbSets en `SnapTimeDbContext`:
  ```csharp
  public DbSet<Settings> Settingss => Set<Settings>();
  public DbSet<HeuristicConfigEntity> HeuristicConfigs => Set<HeuristicConfigEntity>();
  ```
- Fluent API configuration en `IEntityTypeConfiguration<T>` si es necesario.

Acceptance: migration genera las tablas, smoke test insert/read.

**🔵 T-003 — Refactor (Kip)**

**🟢 T-004 — Migración EF (Kip)**
- `dotnet ef migrations add F10_AddSettingsTables`
- `dotnet ef database update`
- Smoke test básico: insertar y leer de ambas tablas.

---

## F10-US-002 — Refactor de ConfigService

### Tareas

**🔴 T-001 — Tests del nuevo ConfigService (Janus)**
- Bootstrap desde JSON (solo database + logging).
- DB vacía → seed automático con defaults.
- `UpdateRuntime()` → persiste en BD, emite evento.
- Migración automática: JSON con secciones legacy → se migran a BD y se limpian del JSON.

**🟢 T-002 — Implementar ConfigService híbrido (Kip)**
- Separar lógica bootstrap (lectura JSON) de runtime (lectura/escritura BD).
- `ConfigService` ahora:
  - Constructor: recibe `ISnapTimeDbContext` (o `IDbContextFactory<SnapTimeDbContext>`).
  - Carga JSON → conecta → seed si vacío → expone `Current`.
  - `UpdateRuntime(SettingsChanges changes)`: valida, persiste, emite `OnConfigChanged`.
- Eliminar `FileSystemWatcher` (ya no aplica — no se edita JSON a mano).
- Asegurar que `Current` devuelve `SnapTimeConfig` completo (bootstrap + runtime combinados).
- `SnapTimeConfig` del dominio: `LoggingConfig` y `DatabaseConfig` se siguen poblando desde JSON; el resto desde BD.

Acceptance: el sistema arranca con JSON mínimo + DB seed, y la API `/api/config` devuelve el config completo.

---

## F10-US-003 — Actualizar API `/api/config`

### Tareas

**🔴 T-001 — Tests de endpoints (Janus)**
- `GET /api/config` devuelve `SnapTimeConfig` completo con valores actuales de BD.
- `PUT /api/config` con cambios parciales persiste en BD.
- `PUT /api/config` con valores inválidos devuelve 400.
- Auditoría registrada tras cada cambio.

**🟢 T-002 — Implementar/actualizar ConfigController (Kip)**
- `GET /config` → `ConfigService.Current`.
- `PUT /config` → `ConfigService.UpdateRuntime(...)`.
- DTOs `ConfigUpdateRequest` y `HeuristicConfigDto` se mantienen (ver `docs/07-api-contracts.md`).

Acceptance: endpoints pasan tests de integración con BD real.

---

## F10-US-004 — E2E tests autónomos con config en BD

### Tareas

**🟢 T-001 — Refactor E2E fixture (Kip/Karris)**
- Eliminar la mutación de `snaptime.config.json` en `E2EWebFixture.cs`.
- El servidor arranca con su propio JSON bootstrap; la configuración runtime se gestiona vía API.
- `SNAPTIME_CONFIG_PATH` env var ya implementada (cambio previo en `Program.cs`).

Acceptance: E2E tests arrancan sin modificar archivos fuente.

---

## F10-US-005 — Actualizar tests existentes

### Tareas

**🔴 T-001 — Tests de integración (Janus)**
- Revisar tests de `SqliteDbFixture` y `ReviewEndpointsTests` que dependan de `ConfigService`.
- Asegurar que usan el nuevo ConfigService híbrido con BD seed.

**🟢 T-002 — Ajustes en tests (Janus)**

---

## F10-US-006 — QA / Code review (Gavin)

### Tareas

**👁 T-001 — Review completo**
- Revisar ConfigService híbrido, seed lógico, migración automática, endpoints.
- Verificar que no se pierden configuraciones existentes durante la migración.

Acceptance: Gavin aprueba.
