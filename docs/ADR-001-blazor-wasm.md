# ADR-001: Blazor WebAssembly como framework de UI

## Estado
Aceptado

## Contexto
SnapTime necesita una interfaz de usuario web para localhost. El backend es C#/.NET. Se requiere un framework que se integre bien con el stack .NET, minimice dependencias externas y sea sencillo de mantener.

## Decisión
Usar Blazor WebAssembly (Blazor WASM) para la UI.

## Consecuencias
- **Positivas:** mismo lenguaje C# en frontend y backend; modelos y DTOs compartibles vía proyecto shared; sin tooling JS (npm, webpack); template oficial `dotnet new blazorwasm --hosted`.
- **Negativas:** tamaño de descarga inicial mayor que SPA tradicional; dependencia de WebAssembly en el navegador.
- **Neutras:** la UI se comunica con el backend vía API REST; el backend expone endpoints HTTP independientes del cliente.

## Alternativas consideradas
- **React + API REST:** buena integración pero duplica toolchain (npm, JS/TS).
- **Vue + API REST:** misma desventaja que React.
- **Angular:** excesivo para una app localhost.

## Referencias
- docs/07-decisiones-tecnologicas.md
- docs/06-requisitos-ui.md
