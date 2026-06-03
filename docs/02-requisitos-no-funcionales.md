# SnapTime - Requisitos no funcionales (NFR)

## NFR-01 Privacidad y seguridad de datos

- Todo procesamiento debe ejecutarse localmente por defecto.
- No se permite envío de imágenes/metadatos a internet salvo opt-in explícito.
- Registro de operaciones sensibles (inicio de análisis, aplicación de cambios).

## NFR-02 Rendimiento y escalabilidad local

- El sistema debe soportar bibliotecas grandes (objetivo inicial: 100k archivos) de forma incremental.
- Procesamiento por lotes y pipeline asíncrono para no bloquear UI.
- Posibilidad de limitar concurrencia para no saturar CPU/disco.

## NFR-03 Fiabilidad operativa

- Los jobs deben ser reanudables tras cierre inesperado.
- El sistema debe ser tolerante a archivos corruptos sin abortar ejecución completa.
- Errores por archivo se registran y se continúa con el resto.

## NFR-04 Trazabilidad y explicabilidad

- Cada score y sugerencia debe incluir evidencia utilizable por el usuario.
- Debe existir versionado de heurísticas y de configuración.
- Todas las decisiones deben ser auditables.

## NFR-05 Idempotencia y consistencia

- Re-ejecutar el análisis sobre la misma biblioteca no debe duplicar datos ni estados inválidos.
- Las operaciones de aplicación de cambios deben ser atómicas por archivo.

## NFR-06 Usabilidad

- UI clara para flujos de revisión masiva.
- Filtros y ordenación eficientes en listados grandes.
- Estados de proceso visibles (en curso, pausado, cancelado, finalizado, error).

## NFR-07 Mantenibilidad

- Arquitectura modular (UI, API, MCP, Core, Infra) con bajo acoplamiento.
- Cobertura de tests unitaria sobre heurísticas y parsing.
- Integración continua con validaciones básicas.

## NFR-07B Estrategia de pruebas

- En esta etapa del proyecto se implementarán exclusivamente tests unitarios.
- El flujo de trabajo seguirá TDD: test que falla -> implementación mínima -> refactor -> suite completa.
- Excepto durante la creación inicial de nuevos tests, se ejecutará siempre la suite completa de unit tests.
- La suite de unit tests debe mantenerse rápida para favorecer ejecución frecuente.

## NFR-08 Portabilidad

- Ejecución en entorno local de escritorio con dependencias mínimas.
- Backend implementado en C#/.NET y compatible con la versión objetivo definida para el proyecto.
- Preferencia de versión de runtime: última estable disponible (.NET 10 si procede; fallback .NET 8 LTS).
- UI accesible en localhost (Blazor WebAssembly).
- Compatibilidad objetivo inicial: macOS/Windows (Linux en fase posterior).

## NFR-09 Observabilidad

- Logs estructurados por componente y correlación por job.
- Métricas básicas: archivos procesados, throughput, tasa de error, sugerencias emitidas.

## NFR-10 Cumplimiento de cambios seguros

- Por defecto operar en modo "analizar y sugerir"; aplicación de cambios en paso separado.
- Requerir confirmación explícita en operaciones destructivas o masivas.

