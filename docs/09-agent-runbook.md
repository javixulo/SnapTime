# Agent Runbook — Model Enforcement

Este documento explica cómo se asegura que los agentes Janus (tdd), Kip (backend) y Karris (frontend) se ejecuten siempre con el modelo local `ollama/qwen2.5-coder:14b`.

## Mecanismo: Config nativa de OpenCode

OpenCode permite asignar un modelo específico a cada agente mediante la sección `"agent"` en `opencode.json`. Este es el mecanismo oficial y soportado — no requiere scripts externos ni wrappers.

### Configuración

En `opencode.json` (raíz del proyecto):

```json
{
  "$schema": "https://opencode.ai/config.json",
  "default_agent": "planning",
  "instructions": ["AGENTS.md"],
  "agent": {
    "tdd": {
      "model": "ollama/qwen2.5-coder:14b"
    },
    "backend": {
      "model": "ollama/qwen2.5-coder:14b"
    },
    "frontend": {
      "model": "ollama/qwen2.5-coder:14b"
    }
  }
}
```

Cada entrada en `"agent"` asigna un modelo al agente correspondiente. OpenCode lo resuelve automáticamente cada vez que se invoca al agente (via `@mention`, Task tool, o como subagente).

### Formato del modelo

**Siempre debe ser `provider/model-id`.** En nuestro caso:

| Proveedor | Modelo | Referencia completa |
|-----------|--------|-------------------|
| Ollama (local) | `qwen2.5-coder:14b` | `ollama/qwen2.5-coder:14b` |

El prefijo del proveedor (`ollama/`) es obligatorio. Sin él, OpenCode no puede resolver el modelo y bloquea el lanzamiento del agente.

### Archivos de definición de agente (`.opencode/agents/*.md`)

Los archivos markdown de los agentes también tienen un campo `model:` en su frontmatter YAML. Este campo debe usar el mismo formato:

```yaml
---
name: Janus
mode: subagent
model: ollama/qwen2.5-coder:14b
---
```

Actualmente tienen este campo estos agentes:

| Archivo | Agente | Modelo |
|---------|--------|--------|
| `.opencode/agents/tdd.md` | Janus | `ollama/qwen2.5-coder:14b` |
| `.opencode/agents/backend.md` | Kip | `ollama/qwen2.5-coder:14b` |
| `.opencode/agents/frontend.md` | Karris | `ollama/qwen2.5-coder:14b` |
| `.opencode/agents/planning.md` | Corvan | `ollama/qwen2.5-coder:14b` |

**Nota**: OpenCode lee el `model:` del archivo `.md` además del `opencode.json`. Ambos deben estar sincronizados. Si el modelo en el `.md` no se resuelve (por formato incorrecto o provider desconocido), OpenCode desactiva el agente para invocación programática vía Task tool.

### Proveedor Ollama

El proveedor Ollama está configurado en el archivo global de OpenCode (`~/.config/opencode/opencode.jsonc`):

```json
{
  "provider": {
    "ollama": {
      "name": "Ollama",
      "npm": "@ai-sdk/openai-compatible",
      "options": {
        "baseURL": "http://localhost:11434/v1"
      },
      "models": {
        "qwen2.5-coder:14b": {
          "name": "qwen2.5-coder:14b"
        }
      }
    }
  }
}
```

## Cómo verificar que funciona

### 1. Lanzar un agente y preguntarle qué modelo usa

Desde cualquier sesión de OpenCode, invoca al agente y pídele que reporte su modelo:

```
@kip ¿qué modelo estás usando?
```

O mediante el Task tool desde otro agente:

```
task: lanza a Janus y pregúntale qué modelo tiene asignado
```

El agente responderá con el nombre del modelo (ej. `qwen2.5-coder:14b`).

### 2. Comprobar que el agente se lanza sin error

Si la configuración es correcta, los agentes se lanzan sin problema. Si hay un error de resolución de modelo, OpenCode muestra un error y no ejecuta el agente.

## Historial de problemas resueltos

### Problema: `model: qwen2.5-coder:14b` sin prefijo

**Síntoma**: Al lanzar `tdd`, `backend` o `frontend` via Task tool, devolvía `Error` sin más detalles. Los agentes sin `model:` en su frontmatter (Gavin, Ferkudi) funcionaban correctamente.

**Causa**: El campo `model:` en los archivos `.md` y en `opencode.json` usaba `qwen2.5-coder:14b` en vez de `ollama/qwen2.5-coder:14b`. OpenCode no resolvía el modelo y bloqueaba el agente.

**Solución**: Añadir el prefijo `ollama/` en todos los `model:`.

### Problema: Config en fichero global vs proyecto

**Síntoma**: La sección `"agent"` se añadió a `~/.config/opencode/opencode.jsonc` (config global), afectando a todos los proyectos de OpenCode.

**Causa**: La configuración de agentes debe ir en el `opencode.json` del proyecto, no en el global.

**Solución**: Mover la sección `"agent"` al `opencode.json` de SnapTime.

## Notas importantes

- OpenCode cachea la configuración de los agentes al inicio de la sesión. Los cambios en `opencode.json` o en los `.md` requieren reiniciar OpenCode para que surtan efecto.
- Los agentes sin `model:` en su frontmatter usan el modelo del agente que los invoca (o el modelo por defecto del sistema). Esto es correcto para agentes como Gavin o Ferkudi que no necesitan un modelo específico.
- No es necesario usar scripts wrapper externos. OpenCode resuelve el modelo nativamente.
