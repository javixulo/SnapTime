# SnapTime — Mandatory AI rules

## Commits and pushes

BEFORE any git commit or push: STOP and ask user for explicit permission.
Do not proceed until user says "yes", "adelante", "dale", "commit", "push" or similar.
This includes `git commit`, `git push`, `git add + commit`, and any alias or script that performs these operations.

## Documentation

- Do not modify documentation files (*.md in docs/ and root, ADR-*) without asking first.
- Exceptions: minor spelling fixes or obvious factual errors.

## Dependencies

- Do not add NuGet, npm, or any external dependency without prior validation.

## Agent pipeline

Every User Story follows this strict workflow:

1. 🔴 **Janus** writes failing tests first
2. 🟢 **Kip** implements backend code (API, services, domain, EF); **Karris** implements frontend code (Blazor components, services HTTP del cliente) in parallel
3. 🔵 **Kip** refactors backend; **Karris** refactors frontend (tests must stay green)
4. 🗄️ **Kip** generates EF migration if entities/DbContext changed (`dotnet ef migrations add`, `database update`, smoke test insert/read)
5. 👁 **Gavin** reviews everything
6. If Gavin finds issues, Janus fixes test issues; Kip fixes backend issues; Karris fixes frontend issues
7. Repeat from step 3/4 until Gavin approves

**Asignación por área:**
- **Karris**: todo componente Blazor (`.razor`), servicios HTTP del cliente (`IClient` / `Client`), estilos, layouts, modales, carpetas `Client/`
- **Kip**: todo backend (API endpoints, servicios de dominio/infraestructura, DTOs, EF, entidades), carpetas `Server/`, `Domain/`, `Infrastructure/`

## Stack and architecture

- Any change to the technology stack, module architecture, or design decisions recorded in ADRs must be discussed before implementation.

## Project

SnapTime is a local-first application for analyzing photo and video libraries and validating/correcting capture dates. Backend in C#/.NET 10 (fallback .NET 8 LTS), UI in Blazor WASM, SQLite database with EF Core 10 code-first (fallback EF Core 8 LTS). See `docs/` for full documentation.
