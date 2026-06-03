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

## Stack and architecture

- Any change to the technology stack, module architecture, or design decisions recorded in ADRs must be discussed before implementation.

## Project

SnapTime is a local-first application for analyzing photo libraries and validating/correcting capture dates. Backend in C#/.NET 10, UI in Blazor WASM, SQLite database with EF Core 10 code-first. See `docs/` for full documentation.
