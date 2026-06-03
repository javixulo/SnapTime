# SnapTime

SnapTime es una aplicación local-first para analizar bibliotecas grandes de fotografías y estimar si la fecha de captura de cada imagen es fiable. Calcula una confianza de la fecha actual, propone una fecha alternativa cuando la confianza cae por debajo de un umbral configurable y explica de forma trazable por qué sugiere ese cambio.

**Estado actual:** Fase 0 — Requisitos y diseño.

## Stack tecnológico

| Capa | Tecnología |
|------|-----------|
| Backend | C# / .NET 8 |
| ORM | EF Core code-first (POCO) |
| BD | SQLite |
| UI | Blazor WebAssembly |
| Logging | Serilog |

## Documentación

Toda la documentación del proyecto está en [`docs/`](docs/).

| Documento | Descripción |
|-----------|-------------|
| [00 - Visión y alcance](docs/00-vision-y-alcance.md) | Visión del producto, problema, principios de diseño y alcance |
| [01 - Requisitos funcionales](docs/01-requisitos-funcionales.md) | FR-01 a FR-18 con criterios de aceptación |
| [02 - Requisitos no funcionales](docs/02-requisitos-no-funcionales.md) | NFR-01 a NFR-10 |
| [03 - Blueprint y fases](docs/03-blueprint-flujo-modulos-y-fases.md) | Arquitectura de módulos, flujo E2E, plan por fases |
| [04 - Proceso de desarrollo](docs/04-proceso-de-desarrollo-tdd.md) | Política TDD y reglas de tests |
| [05 - Heurísticas](docs/05-requisitos-heuristicas.md) | Especificación de heurísticas (H-001 a H-005) |
| [06 - Requisitos de UI](docs/06-requisitos-ui.md) | Estructura visual, árbol de carpetas, paginación |
| [07 - Decisiones tecnológicas](docs/07-decisiones-tecnologicas.md) | Stack detallado y justificaciones |
| [ADR-001](docs/ADR-001-blazor-wasm.md) | Blazor WASM como framework de UI |
| [ADR-002](docs/ADR-002-ef-core-code-first.md) | EF Core code-first con POCO para persistencia |

## Licencia

Véase [LICENSE](LICENSE).
