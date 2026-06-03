# SnapTime - Visión y alcance

## 1) Visión del producto

SnapTime es una aplicación local-first para analizar bibliotecas grandes de fotografías y estimar si la fecha de captura de cada imagen es fiable. El sistema calcula una confianza de la fecha actual, propone una fecha alternativa cuando la confianza cae por debajo de un umbral configurable y explica de forma trazable por qué sugiere ese cambio.

Objetivo principal: reducir el trabajo manual de depuración de fechas en galerías históricas sin sacrificar control humano ni privacidad.

## 2) Problema a resolver

En colecciones grandes de fotos (JPG/JPEG principalmente) la fecha de captura puede ser inconsistente o incorrecta por:

- reloj incorrecto de cámara o móvil;
- reescritura de metadatos por apps de edición/exportación;
- importaciones y copias en masa;
- ausencia parcial de EXIF.

Esto dificulta la ordenación cronológica, la búsqueda por eventos y el mantenimiento de archivos personales/profesionales.

## 3) Principios de diseño

- Local-first y sin exfiltración de datos por defecto.
- Explicabilidad obligatoria para cada score/sugerencia.
- Seguridad operativa: primero analizar, después proponer, y solo aplicar cambios con confirmación explícita.
- Iteración incremental: baseline heurístico primero, mejoras inteligentes después.

## 4) Alcance inicial (In Scope)

- Escaneo de carpetas locales de imágenes.
- Extracción de fechas relevantes de metadatos (EXIF/XMP/fs).
- Cálculo de confianza de fecha actual (0-100).
- Sugerencia de fecha alternativa para casos de baja confianza.
- Evidencia trazable por regla/señal.
- UI web con control de procesos (iniciar, pausar, cancelar) y revisión de resultados.
- Persistencia local en SQLite.

## 5) Fuera de alcance inicial (Out of Scope)

- Soporte completo de todos los formatos RAW en Fase 1.

## 6) Usuario objetivo

- Usuario principal: propietario de una gran colección de fotos con necesidad de ordenar y limpiar fechas.
- Usuario secundario: perfil técnico que desea auditar las decisiones del sistema y ajustar heurísticas.

## 7) Definiciones clave

- Fecha actual: valor de fecha de captura vigente del archivo (prioridad principal EXIF:DateTimeOriginal cuando exista).
- Confianza de fecha: probabilidad estimada de que la fecha actual sea correcta según señales combinadas.
- Sugerencia: propuesta de fecha alternativa con su propia confianza y explicación.
- Evidencia: conjunto de señales usadas para score/sugerencia, con peso e impacto.

## 8) Campo canónico de fecha de captura

El sistema utiliza un único campo canónico como fuente de verdad para la fecha de captura:

| Propósito | Tag EXIF |
|-----------|---------|
| Lectura / comparación | `EXIF:DateTimeOriginal` (o `SubSecDateTimeOriginal` si existe) |
| Escritura | `EXIF:DateTimeOriginal` |

Reglas:
- **Lectura:** siempre se lee `DateTimeOriginal`. Si el archivo tiene `SubSecDateTimeOriginal`, se prefiere este por ser más preciso (incluye subsegundos y timezone). Si ninguno existe, la fecha actual se considera ausente.
- **Escritura:** siempre se escribe en `DateTimeOriginal`. Cuando el sistema proponga o aplique una corrección, escribirá en este campo. La hora se fijará a las 5:00 AM en todas las correcciones automáticas.
- **Consistencia con Immich:** Immich utiliza esta misma prioridad (`SubSecDateTimeOriginal` → `DateTimeOriginal`) para ordenar fotos en su timeline, lo que garantiza que las correcciones aplicadas por SnapTime sean reconocidas correctamente por Immich.

