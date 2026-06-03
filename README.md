# SnapTime

SnapTime es una aplicación **local-first** para analizar bibliotecas grandes de fotografías y determinar si su fecha de captura es fiable. Cuando detecta una fecha sospechosa, sugiere una alternativa y explica por qué, basándose en señales como el contexto de la carpeta, los metadatos del archivo, o pistas semánticas en los nombres.

## Motivación

Si tienes una colección grande de fotos (miles o cientos de miles), probablemente algunas fechas de captura son incorrectas. Las causas son variadas:

- El reloj de la cámara o el móvil estaba mal configurado.
- Una app de edición o exportación reescribió los metadatos y puso su propia fecha.
- Copias en masa desde el móvil al PC que asignaron la fecha de copia como fecha de captura.
- Archivos sin metadatos EXIF, donde la única fecha disponible es la del sistema de archivos.

El resultado es el mismo: tus fotos aparecen desordenadas, los eventos se mezclan y mantener el archivo ordenado se vuelve una pesadilla.

SnapTime aborda este problema analizando cada foto y asignando un **score de confianza (0-100)** a su fecha actual. Si la confianza es baja, propone una fecha alternativa utilizando múltiples señales:

- **Contexto de carpeta:** si el 90% de las fotos de una carpeta comparten una fecha y el 10% restante tiene una muy diferente, esas son sospechosas.
- **Nombre de carpeta/archivo:** una carpeta llamada "cumpleaños_mayo_2021" es una pista sobre la fecha esperada.
- **Metadatos del archivo:** inconsistencias entre EXIF:DateTimeOriginal, EXIF:CreateDate y EXIF:ModifyDate.
- **Sistema de archivos:** una foto con fecha 2021 pero cuyo archivo tiene `mtime` de 2015 es imposible — la fecha es incorrecta.
- Y más heurísticas configurables que se irán añadiendo.

Todo el procesamiento es **100% local**. No se envía ninguna imagen, metadato ni información a internet por defecto. La privacidad es un principio de diseño, no una ocurrencia tardía.

## Estado actual

**Fase 0 — Requisitos y diseño.** La documentación de visión, requisitos funcionales y no funcionales, arquitectura, heurísticas y configuración está completa y disponible en [`docs/`](docs/).

## Stack tecnológico

| Capa | Tecnología |
|------|-----------|
| Backend | C# / .NET 8 |
| ORM | EF Core code-first (POCO → esquema SQLite automático) |
| BD | SQLite |
| UI | Blazor WebAssembly |
| Logging | Serilog |

## Principios de diseño

- **Local-first:** ningún dato sale de tu máquina sin consentimiento explícito.
- **Explicabilidad obligatoria:** cada score y sugerencia incluye su evidencia trazable.
- **Seguridad operativa:** primero analizar, después proponer, y solo escribir cambios con confirmación explícita.
- **Iteración incremental:** baseline heurístico primero, mejoras basadas en datos después.

## Documentación

| Documento | Descripción |
|-----------|-------------|
| [00 - Visión y alcance](docs/00-vision-y-alcance.md) | Visión del producto, problema, principios de diseño y alcance |
| [01 - Requisitos funcionales](docs/01-requisitos-funcionales.md) | FR-01 a FR-18 con criterios de aceptación |
| [02 - Requisitos no funcionales](docs/02-requisitos-no-funcionales.md) | NFR-01 a NFR-10 |
| [03 - Arquitectura del sistema](docs/03-blueprint-flujo-modulos-y-fases.md) | Módulos, decisiones tecnológicas, contratos API/MCP, fases |
| [04 - Proceso de desarrollo](docs/04-proceso-de-desarrollo-tdd.md) | Política TDD y reglas de ejecución de tests |
| [05 - Heurísticas](docs/05-requisitos-heuristicas.md) | Especificación de heurísticas (H-001 a H-006) |
| [06 - Requisitos de UI](docs/06-requisitos-ui.md) | Estructura visual, árbol de carpetas, paginación, chat |
| [08 - Configuración](docs/08-configuracion.md) | Esquema de configuración JSON del sistema |

## Licencia

Véase [LICENSE](LICENSE).
