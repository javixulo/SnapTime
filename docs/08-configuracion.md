# SnapTime - Configuración del sistema

## 1) Archivo de configuración

La configuración se almacena en un archivo JSON (`snaptime.config.json`) ubicado en el mismo directorio que la base de datos SQLite. Es la fuente de verdad persistente, editable tanto desde la UI como directamente a mano.

## 2) Esquema completo

```jsonc
{
  "$schema": "snaptime.config.schema.json",

  // --- Base de datos ---
  "database": {
    "path": "SnapTime.db"
  },

  // --- Análisis ---
  "analysis": {
    "confidenceThreshold": 50,
    "maxConcurrency": 4,
    "batchSize": 100,
    "imageExtensions": [".jpg", ".jpeg"],
    "videoExtensions": [".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"]
  },

  // --- Heurísticas ---
  "heuristics": [
    {
      "id": "H-001",
      "enabled": true,
      "weight": 1.0
    },
    {
      "id": "H-002",
      "enabled": true,
      "weight": 1.0
    },
    {
      "id": "H-003",
      "enabled": true,
      "weight": 1.0
    },
    {
      "id": "H-004",
      "enabled": true,
      "weight": 0.5
    },
    {
      "id": "H-005",
      "enabled": true,
      "weight": 0.7
    },
    {
      "id": "H-006",
      "enabled": true,
      "weight": 1.0
    }
  ],

  // --- Chat (Ollama) ---
  "ollama": {
    "endpoint": "http://localhost:11434",
    "model": "llama3.2",
    "timeoutSeconds": 60
  },

  // --- Miniaturas ---
  "thumbnails": {
    "maxDimension": 300,
    "quality": 80
  },

  // --- Logging ---
  "logging": {
    "level": "Information",
    "file": "snaptime.log"
  }
}
```

## 3) Descripción de campos

### database
| Campo | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `path` | string | `"SnapTime.db"` | Ruta del archivo SQLite. Puede ser relativa al directorio de datos de la app o absoluta. |

### analysis
| Campo | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `confidenceThreshold` | int | `50` | Umbral global de confianza (0-100). Por debajo de este valor se genera sugerencia. |
| `maxConcurrency` | int | `4` | Número máximo de análisis en paralelo. |
| `batchSize` | int | `100` | Archivos por lote en cada iteración de escaneo. |
| `imageExtensions` | string[] | `[".jpg", ".jpeg"]` | Extensiones de imagen a incluir en el escaneo. |
| `videoExtensions` | string[] | `[".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"]` | Extensiones de vídeo a incluir en el escaneo. |

### heuristics
Array de objetos con:
| Campo | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `id` | string | — | Identificador único de la heurística (ej: `"H-006"`). |
| `enabled` | bool | `true` | Si está activa o no. Si es `false`, no influye en score ni sugerencias. |
| `weight` | number | `1.0` | Peso relativo de la heurística en el cálculo del score. |

### ollama
| Campo | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `endpoint` | string | `"http://localhost:11434"` | URL del servidor Ollama local. |
| `model` | string | `"llama3.2"` | Modelo a usar para el chat conversacional. |
| `timeoutSeconds` | int | `60` | Timeout máximo para cada petición al LLM. |

### thumbnails
| Campo | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `maxDimension` | int | `300` | Tamaño máximo en píxeles del lado más largo de la miniatura. |
| `quality` | int | `80` | Calidad JPEG de la miniatura (1-100). |

### logging
| Campo | Tipo | Default | Descripción |
|-------|------|---------|-------------|
| `level` | string | `"Information"` | Nivel de log de Serilog (Verbose, Debug, Information, Warning, Error, Fatal). |
| `file` | string | `"snaptime.log"` | Ruta del archivo de log. |

## 4) Servicio de configuración (ConfigService)

- **Singleton** en el contenedor DI.
- Carga el JSON al arrancar en `SnapTime.Domain/Config/SnapTimeConfig.cs`.
- Expone listas `ImageExtensions` y `VideoExtensions` que el scanner usa como filtro.
- Expone la configuración actual como `SnapTimeConfig Current`.
- Expone un evento `event Action<SnapTimeConfig> OnConfigChanged`.
- **FileSystemWatcher** sobre el archivo para detectar cambios externos (edición manual).
- Valida los valores (rangos, tipos) antes de aplicar. Si hay error, lo notifica y no aplica.
- Cada cambio se registra en la tabla de auditoría (valor anterior → valor nuevo).

## 5) Flujo de aplicación de cambios

```
Usuario edita en UI
       ↓
UI llama a ConfigService.Update(changes)
       ↓
ConfigService valida → si error, devuelve error a UI
       ↓
ConfigService guarda JSON en disco
       ↓
ConfigService registra auditoría en BD
       ↓
ConfigService emite OnConfigChanged
       ↓
Motor de heurísticas / Worker / etc. reaccionan con nuevos valores
```

## 6) Precedencia

1. Archivo JSON (`snaptime.config.json`) — fuente de verdad.
2. UI escribe siempre al JSON, nunca bypass.
3. Edición manual del JSON también se detecta y aplica en runtime.
