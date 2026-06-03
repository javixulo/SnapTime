# SnapTime - Requisitos de heurísticas

## 1) Objetivo del documento
Este documento define, de forma centralizada, todas las heurísticas que el sistema utiliza para:
- estimar la confianza de la fecha actual;
- proponer fechas alternativas cuando proceda.

El desarrollo del motor heurístico debe implementarse siempre en base a este documento.

## 2) Política de evolución
- Toda heurística nueva debe añadirse primero aquí, antes de implementarse.
- Cada heurística debe incluir reglas claras, entradas, salida esperada y límites conocidos.
- Los cambios deben quedar versionados en auditoría de configuración.

## 3) Ficha obligatoria por heurística
Cada heurística se documentará con esta plantilla:
- Identificador único (`H-XXX`).
- Nombre.
- Descripción.
- Entradas necesarias.
- Regla de cálculo/decisión.
- Impacto esperado en score (positivo/negativo y rango).
- Casos límite conocidos.
- Tests unitarios mínimos asociados.
- Estado (`activa`, `inactiva`, `experimental`).

## 4) Requisito de configuración en runtime
- Todas las heurísticas deben poder activarse o desactivarse por configuración.
- El cambio de estado debe aplicarse en runtime.
- Si una heurística está desactivada, no debe influir en score ni sugerencias.

## 5) Heurísticas iniciales candidatas
- `H-001`: Consistencia entre `EXIF:DateTimeOriginal`, `EXIF:CreateDate` y `EXIF:ModifyDate`.
- `H-002`: Consenso temporal en carpeta/lote.
- `H-003`: Detección de outlier temporal respecto al grupo.
- `H-004`: Pistas temporales en nombre de carpeta/archivo (evidencia blanda).
- `H-005`: Coherencia temporal con metadatos de sistema de archivos (evidencia secundaria).
- `H-006`: Parseo de fecha en nombre de archivo (formato móvil).

## 6) Fichas de heurísticas

### H-006: Parseo de fecha en nombre de archivo (formato móvil)

- **Identificador:** H-006
- **Nombre:** Parseo de fecha en nombre de archivo (formato móvil)
- **Descripción:** Los móviles y cámaras generan nombres de archivo con la fecha como prefijo (ej: `20250315_123456.jpg`, `20250315_123456.mp4`). Esta heurística extrae la fecha del nombre del archivo (funciona igual para imágenes y vídeos). Si existe fecha en metadatos (EXIF o QuickTime), compara contra ella. Si no existe, la fecha del nombre se usa directamente como sugerencia. En ambos casos, si hay discrepancia o ausencia de fecha en metadatos, se sugiere la fecha extraída del nombre con hora 5:00 AM.
- **Entradas necesarias:**
  - Nombre del archivo (sin extensión).
  - Fecha canónica del archivo (primer tag con valor en la lista de prioridad: `SubSecDateTimeOriginal` → `DateTimeOriginal` → `CreationDate` → etc.).
- **Regla de cálculo / decisión:**
  1. Intentar parsear el inicio del nombre del archivo con el patrón `yyyyMMdd` (año+mes+día, 8 dígitos consecutivos).
  2. Si no coincide, intentar `yyyy-MM-dd` (con guiones).
  3. Si no se puede parsear ninguna fecha → la heurística no emite señal.
  4. Si se extrae una fecha del nombre:
     - Si existe fecha canónica en los metadatos del archivo, comparar solo año, mes y día (la hora se ignora).
       - Si coinciden → evidencia positiva (la fecha del nombre respalda la metadato).
       - Si no coinciden → anomalía: la fecha del nombre sugiere que la fecha en metadatos podría ser incorrecta.
     - Si no existe fecha en metadatos → no hay fecha actual que evaluar; se propone la fecha del nombre como sugerencia directamente.
  5. En caso de anomalía o metadatos ausentes, la sugerencia se compone con la fecha extraída del nombre y hora fijada a las 5:00 AM.
- **Impacto esperado en score:**
  - Coincidencia: impacto positivo moderado (la EXIF se refuerza).
  - Discrepancia: impacto negativo significativo (la EXIF es sospechosa).
  - Sin fecha en nombre: sin impacto.
- **Casos límite conocidos:**
  - Archivos renombrados por el usuario (pierden la fecha original del nombre).
  - Formatos de fecha no estándar en el nombre (ej: `IMG_20250315.jpg` con prefijo variable).
  - Teléfonos que usan `yyyyMMdd_HHmmss` (el parseo debe ignorar lo que sigue a los 8 dígitos).
- **Tests unitarios mínimos asociados:**
  - Nombre `20250315_123456.jpg` con metadatos del mismo día → coincide.
  - Nombre `20250315_123456.jpg` con metadatos de 2024-07-10 → anomalía, sugerencia 2025-03-15 05:00.
  - Nombre `20250315_123456.mp4` con metadatos del mismo día → coincide (vídeo).
  - Nombre `20250315_123456.mp4` sin metadatos → sugerencia 2025-03-15 05:00 (vídeo).
  - Nombre `IMG_20250315.jpg` → no se puede parsear (prefijo variable), sin señal.
  - Nombre `vacaciones.jpg` → sin fecha, sin señal.
  - Nombre `20250315_123456.jpg` sin metadatos → sugerencia 2025-03-15 05:00.
- **Estado:** activa.

## 7) Nota sobre dataset de validación
- El dataset de validación real se incorporará progresivamente.
- Mientras tanto, los escenarios relevantes se cubrirán mediante tests unitarios representativos.
