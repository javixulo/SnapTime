# SnapTime - Configuración del sistema

## 1) Modelo híbrido: bootstrap + runtime

La configuración se divide en dos niveles:

| Nivel | Almacenamiento | Contenido | Se necesita antes de conectar BD |
|-------|---------------|-----------|-----------------------------------|
| **Bootstrap** | `snaptime.config.json` | `database.path`, `logging.*` | Sí |
| **Runtime** | Tabla `Settings` + `HeuristicConfig` en SQLite | `analysis.*`, `ollama.*`, `thumbnails.*`, heuristics[] | No |

### Bootstrap JSON (`snaptime.config.json`)

Ubicado junto al ejecutable del servidor. Contiene solo lo imprescindible para arrancar:

```jsonc
{
  // --- Base de datos ---
  "database": {
    "path": "SnapTime.db"
  },

  // --- Logging (antes de tener BD) ---
  "logging": {
    "level": "Information",
    "file": "snaptime.log"
  }
}
```

### Configuración runtime (BD)

Todo lo demás se persiste en SQLite una vez que la conexión está establecida. El `ConfigService`, tras cargar el bootstrap, conecta a la BD y lee/escribe la configuración runtime.

## 2) Entidades de BD

### Settings (fila única, Id = 1)

| Columna | Tipo | Default | Descripción |
|---------|------|---------|-------------|
| `ConfidenceThreshold` | int | `80` | Umbral global de confianza (0–100) |
| `MaxConcurrency` | int | `4` | Máximo de análisis en paralelo |
| `BatchSize` | int | `100` | Archivos por lote en cada iteración de escaneo |
| `ImageExtensionsCsv` | string | `".jpg,.jpeg"` | Extensiones de imagen separadas por coma |
| `VideoExtensionsCsv` | string | `".mp4,.mov,.avi,.mkv,.webm,.m4v"` | Extensiones de vídeo separadas por coma |
| `OllamaEndpoint` | string | `"http://localhost:11434"` | URL del servidor Ollama |
| `OllamaModel` | string | `"qwen2.5-coder:14b"` | Modelo de chat. Backend agent Kip MUST use this model. |
| `OllamaTimeoutSeconds` | int | `60` | Timeout máximo por petición al LLM |
| `ThumbnailMaxDimension` | int | `300` | Tamaño máximo del lado más largo de miniatura (px) |
| `ThumbnailQuality` | int | `80` | Calidad JPEG de miniatura (1–100) |

### HeuristicConfig (una fila por heurística)

| Columna | Tipo | Default | Descripción |
|---------|------|---------|-------------|
| `Id` | string PK | — | Identificador único (ej. `"H-006"`) |
| `Enabled` | bool | `true` | Si la heurística está activa |
| `Weight` | double | `1.0` | Peso relativo en el cálculo del score |

## 3) Servicio de configuración (ConfigService)

### Comportamiento en startup

1. Lee `snaptime.config.json` del disco y extrae `database.path` y `logging.*`.
2. Conecta a SQLite usando `database.path`.
3. Consulta la tabla `Settings`. Si no existe fila (BD nueva), la crea con los valores por defecto (definidos en el código de la entidad).
4. Consulta la tabla `HeuristicConfig`. Si está vacía, la seed con las 6 heurísticas baseline.
5. Expone `Current` como un objeto `SnapTimeConfig` que mezcla valores de bootstrap + runtime.

### API pública

- `ConfigService` es **Singleton** en DI.
- `Current` → `SnapTimeConfig` (combinación de bootstrap + runtime).
- `UpdateRuntime(SettingsChanges changes)` → valida, persiste en BD, emite `OnConfigChanged`.
- `event Action<SnapTimeConfig>? OnConfigChanged` → notifica a consumidores (motor de heurísticas, workers, etc.).
- Ya no hay `FileSystemWatcher` — la configuración runtime no se edita a mano.

### Validación

- `ConfidenceThreshold` se clamp a [0, 100].
- `Weight` de cada heurística se clamp a >= 0.
- Si hay error de validación, `UpdateRuntime` devuelve error sin persistir.

## 4) Flujo de aplicación de cambios

```
Usuario edita en UI
       ↓
UI llama a PATCH /api/config (o PUT)
       ↓
ConfigService valida → si error, devuelve error a UI
       ↓
ConfigService persiste en BD (Settings + HeuristicConfig)
       ↓
ConfigService registra auditoría en BD
       ↓
ConfigService emite OnConfigChanged
       ↓
Motor de heurísticas / Worker / etc. reaccionan
```

## 5) Precedencia

1. Bootstrap JSON (`snaptime.config.json`) — solo para `database.path` y `logging.*`.
2. BD (`Settings` + `HeuristicConfig`) — fuente de verdad del runtime.
3. UI escribe siempre a BD, nunca al JSON.

## 6) Migración desde versión anterior (todo-en-JSON)

Si al arrancar se detecta que el JSON contiene secciones `analysis`, `ollama`, `thumbnails` o `heuristics` (además de las de bootstrap), el sistema migra esos valores a BD automáticamente:

1. Lee los valores del JSON.
2. Los persiste en `Settings` y `HeuristicConfig`.
3. Elimina esas secciones del JSON (deja solo `database` y `logging`).
4. Continúa con el modelo híbrido normal.

Esto garantiza que usuarios con configuraciones existentes no pierdan sus ajustes.
