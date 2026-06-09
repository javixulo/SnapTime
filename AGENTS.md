# SnapTime — Mandatory AI rules

## Corvan (primary agent)

Corvan is the user's direct interlocutor. His job is **documentation and delegation**:
- **Never writes application code.** Never edits `.razor`, `.cs`, `.csproj`, `.sln`, CSS, or config files outside `docs/` and `backlog/`.
- **Never runs build or test commands.**
- Detects gaps between `docs/` and `backlog/` → updates the relevant specs.
- Delegates **all** implementation to specialized subagents via the pipeline below.

## Commits and pushes

BEFORE any git commit or push: STOP and ask user for explicit permission.
Do not proceed until user says "yes", "adelante", "dale", "commit", "push" or similar.
This includes `git commit`, `git push`, `git add + commit`, and any alias or script that performs these operations.

## Documentation

- Only **Corvan** modifies documentation files (*.md in docs/, backlog/, and root, ADR-*).
- Other agents (Kip, Karris, Janus, Gavin, Ferkudi) must never touch them.
- Exceptions for non-Corvan agents: minor spelling fixes or obvious factual errors in commit messages only.

## Dependencies

- Do not add NuGet, npm, or any external dependency without prior validation.

## Tests de frontend: obligatorios siempre

Toda funcionalidad frontend (Blazor, CSS, client services) requiere **ambos** niveles de test:
- **bUnit** (`tests/SnapTime.Client.Tests/`): tests unitarios de componentes (renderizado, eventos, estados visuales). Rápidos, sin navegador.
- **Playwright** (`tests/SnapTime.E2ETests/`): tests E2E con navegador real (flujos completos: selección → grid → detalle → navegación).

Sin excepción. Janus debe escribir ambos en la fase roja, y ambos deben pasar antes de que Gavin apruebe.

## Tests E2E autónomos

Los tests E2E deben ejecutarse sin dependencia de un servidor externo ya funcionando:
- **Arranque automático:** al inicio de la ejecución, el servidor web se inicia como parte del fixture de tests (vía `WebApplicationFactory<Program>` u otro mecanismo que lo haga accesible desde el navegador real).
- **Parada automática:** al finalizar la ejecución, el servidor se detiene y libera recursos.
- **Base de datos efímera:** cada ejecución usa una base de datos SQLite temporal, aislada.

Esto aplica a todos los tests E2E (F4, F5, F6 y futuros).

## Agent pipeline

Every User Story follows this strict workflow:

0. 🗂 **Corvan** detects the need, writes/updates the backlog spec, then delegates
1. 🔴 **Janus** writes failing tests first — **bUnit + E2E** para frontend
2. 🟢 **Kip** implements backend code (API, services, domain, EF); **Karris** implements frontend code (Blazor components, client HTTP services) in parallel
3. 🔵 **Kip** refactors backend; **Karris** refactors frontend (tests must stay green)
4. 🗄️ **Kip** generates EF migration if entities/DbContext changed (`dotnet ef migrations add`, `database update`, smoke test insert/read)
5. 👁 **Gavin** reviews everything
6. If Gavin finds issues, Janus fixes test issues; Kip fixes backend issues; Karris fixes frontend issues
7. Repeat from step 3/4 until Gavin approves
8. 🗂 **Corvan** updates documentation with implementation insights, closes the US

**Area assignment:**
- **Janus**: tests bUnit + E2E para frontend (Playwright). Tests de integración para backend. Unit tests para backend.
- **Karris**: all Blazor components (`.razor`), client HTTP services (`IClient` / `Client`), styles, layouts, modals, everything under `Client/`
- **Kip**: all backend (API endpoints, domain/infrastructure services, DTOs, EF, entities), everything under `Server/`, `Domain/`, `Infrastructure/`
- **Corvan**: documentation only (`backlog/`, `docs/`, `AGENTS.md`, `README.md`) — never touches code

## Stack and architecture

- Any change to the technology stack, module architecture, or design decisions recorded in ADRs must be discussed before implementation.

## Project

SnapTime is a local-first application for analyzing photo and video libraries and validating/correcting capture dates. Backend in C#/.NET 10 (fallback .NET 8 LTS), UI in Blazor WASM, SQLite database with EF Core 10 code-first (fallback EF Core 8 LTS). See `docs/` for full documentation.
