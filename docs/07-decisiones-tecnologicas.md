# SnapTime - Decisiones tecnológicas

## 1) Backend
- Lenguaje obligatorio: C#.
- Plataforma obligatoria: .NET.
- Política de versión: usar la última versión estable disponible en cada iteración mayor.
- Preferencia actual del proyecto: .NET 10, siempre que esté estable y no haya incompatibilidades de stack.
- Fallback aceptado: .NET 8 LTS cuando exista impedimento técnico justificado.

## 2) UI
- Framework: Blazor WebAssembly (Blazor WASM).
- Integración nativa con C#/.NET, mismos modelos y validaciones compartidas.
- Sin dependencias de npm/JS tooling.
- Criterio de decisión: mejor encaje con arquitectura API/MCP, portabilidad y simplicidad de toolchain.
- Template de inicio: `dotnet new blazorwasm --hosted` (cliente + servidor + shared).

## 3) Persistencia
- Motor de base de datos: SQLite.
- ORM: Entity Framework Core con enfoque code-first (POCO classes en C#).
- Las clases POCO en `SnapTime.Domain` son la fuente de verdad del esquema.
- EF Core genera y actualiza la BD automáticamente mediante migrations.
- No se mantienen archivos SQL manuales ni esquemas separados.
- Durante el desarrollo se permite modificar las POCOs libremente y regenerar migrations.
- El esquema se considerará estable con la versión final del producto.

## 4) Chat conversacional
- Motor de LLM local: Ollama.
- El chat del panel derecho envía mensajes al backend, que consulta Ollama con tool calling sobre las MCP tools.
- El LLM interpreta el mensaje en lenguaje natural, ejecuta la tool correspondiente y devuelve la respuesta formateada.
- No se envía información a internet (alineado con principio local-first).

## 5) Configuración
- Formato: JSON (`snaptime.config.json`).
- Servicio singleton con FileSystemWatcher para cambios en runtime.
- Validación de valores antes de aplicar.

## 6) Logging
- Librería seleccionada: Serilog.
- Integración recomendada: `Microsoft.Extensions.Logging` + Serilog.
- Debe mantenerse logging estructurado como estándar del proyecto.
