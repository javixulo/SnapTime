# F9 — Chat contextual con LLM (Ollama)

> Panel derecho (15% ancho) con chat conversacional hacia un LLM local (Ollama). El LLM tiene tool calling sobre las MCP tools para consultar fotos, evidencia y sugerencias del contexto actual.

**Referencias:** FR-19, docs/03-blueprint.md §3, docs/06-ui.md

**Dependencias:** F1 (datos en SQLite), F5 (contexto actual del grid)

**Reglas base:**
- Panel de chat en el lado derecho (15% ancho)
- El usuario escribe mensajes en lenguaje natural
- El backend envía el mensaje a Ollama con tool calling
- Tools disponibles: `scan_library`, `list_low_confidence`, `get_photo_evidence`, `suggest_date`, `apply_fix`
- El LLM puede consultar herramientas y devolver respuesta formateada
- Indicador visual de "escribiendo..." mientras Ollama procesa
- Historial de conversación visible en el panel
- Las operaciones ejecutadas desde el chat se reflejan en los demás paneles

**Contrato (pendiente de desglosar en US):**
- Componente Blazor `ChatPanel.razor`
- Servicio `IChatService` en Server
- MCP Server con herramientas registradas
- Cliente HTTP para Ollama (`POST /api/chat`)
- Tests del chat con mock de Ollama
