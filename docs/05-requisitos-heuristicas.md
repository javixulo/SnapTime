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

### H-006: Parseo de fecha en nombre de archivo (multipatrón)

- **Identificador:** H-006
- **Nombre:** Parseo de fecha en nombre de archivo (multipatrón)
- **Descripción:** Extrae fechas del nombre del archivo buscando múltiples patrones en cualquier posición del nombre (no solo al inicio). Soporta formatos numéricos y textuales con meses en español e inglés. Funciona igual para imágenes y vídeos. Si existe fecha en metadatos (EXIF o QuickTime), compara contra ella. Si no existe, la fecha del nombre se usa directamente como sugerencia. En ambos casos, si hay discrepancia o ausencia de fecha en metadatos, se sugiere la fecha extraída del nombre con hora 5:00 AM.
- **Entradas necesarias:**
  - Nombre del archivo (sin extensión).
  - Fecha canónica del archivo (primer tag con valor en la lista de prioridad: `SubSecDateTimeOriginal` → `DateTimeOriginal` → `CreationDate` → etc.).
- **Regla de cálculo / decisión:**
  1. Buscar en el nombre del archivo (en cualquier posición) los patrones de fecha en este orden de prioridad. Usar el **primer patrón que coincida**:
     - **P1 — `yyyyMMdd`**: 8 dígitos consecutivos (ej: `20250315`). Sin ambigüedad.
     - **P2 — `yyyy-MM-dd`**: con guiones (ej: `2025-03-15`). Sin ambigüedad.
     - **P3 — `yyyy.MM.dd`**: con puntos (ej: `2025.03.15`). Sin ambigüedad.
     - **P4 — `yyyy_MM_dd`**: con guión bajo (ej: `2025_03_15`). Sin ambigüedad.
     - **P5 — `DD MMM YYYY`**: día numérico + abreviatura de mes (3 letras, inglés/español) + año de 4 dígitos (ej: `10 abr 2025`, `10 Apr 2025`). Sin ambigüedad porque el mes es textual. Case-insensitive.
     - **P6 — `MMM DD YYYY`**: abreviatura de mes (3 letras, inglés/español) + día numérico + año de 4 dígitos (ej: `abr 10 2025`, `Apr 10 2025`). Sin ambigüedad. Case-insensitive.
     - **P7 — `DD-MM-YYYY`**: día + mes + año con guiones (ej: `15-03-2025`). El parseo asume orden DD-MM (europeo).
     - **P8 — `DD.MM.YYYY`**: día + mes + año con puntos (ej: `15.03.2025`). El parseo asume orden DD-MM (europeo).
     - **P9 — `DD/MM/YYYY`**: día + mes + año con barras (ej: `15/03/2025`). El parseo asume orden DD-MM (europeo).
  2. Si el patrón incluye hora opcional después de la fecha (ej: `10 abr 2025, 11_03_36`, `20250315_123456`, `Screenshot 2025-03-15 at 10.30.45`), se ignora la hora; solo se extrae año, mes y día.
  3. Si no se encuentra ningún patrón → la heurística no emite señal.
  4. Si se extrae una fecha del nombre:
     - Si existe fecha canónica en los metadatos del archivo, comparar solo año, mes y día (la hora se ignora).
       - Si coinciden → evidencia positiva (la fecha del nombre respalda el metadato).
       - Si no coinciden → anomalía: la fecha del nombre sugiere que la fecha en metadatos podría ser incorrecta.
     - Si no existe fecha en metadatos → no hay fecha actual que evaluar; se propone la fecha del nombre como sugerencia directamente.
  5. En caso de anomalía o metadatos ausentes, la sugerencia se compone con la fecha extraída del nombre y hora fijada a las 5:00 AM.
- **Abreviaturas de mes soportadas (case-insensitive):**
  - **Español:** `ene`, `feb`, `mar`, `abr`, `may`, `jun`, `jul`, `ago`, `sep`, `oct`, `nov`, `dic`
  - **Inglés:** `jan`, `feb`, `mar`, `apr`, `may`, `jun`, `jul`, `aug`, `sep`, `oct`, `nov`, `dec`
- **Impacto esperado en score:**
  - Coincidencia: impacto positivo moderado (la EXIF se refuerza).
  - Discrepancia: impacto negativo significativo (la EXIF es sospechosa).
  - Sin fecha en nombre: sin impacto.
- **Casos límite conocidos:**
  - Archivos renombrados por el usuario (pierden la fecha original del nombre).
  - Múltiples patrones de fecha en el mismo nombre: se usa el primero que coincida según el orden de prioridad.
  - Ambigüedad en patrones P7-P9 (DD-MM vs MM-DD): se asume orden europeo (día-mes). La implementación debe priorizar la interpretación DD-MM cuando ambos sean válidos.
  - Meses en otros idiomas (francés, alemán, etc.) no están soportados en esta versión.
  - Fechas con mes completo (`10 April 2025`, `10 abril 2025`) no están soportados en esta versión (solo abreviaturas de 3 letras).
- **Tests unitarios mínimos asociados:**
  - Nombre `20250315_123456.jpg` con metadatos del mismo día → coincide (P1).
  - Nombre `20250315_123456.jpg` con metadatos de 2024-07-10 → anomalía, sugerencia 2025-03-15 05:00 (P1).
  - Nombre `20250315_123456.mp4` con metadatos del mismo día → coincide (P1, vídeo).
  - Nombre `20250315_123456.mp4` sin metadatos → sugerencia 2025-03-15 05:00 (P1, vídeo).
  - Nombre `IMG_20250315.jpg` → P1 captura los 8 dígitos en cualquier posición → extrae 2025-03-15 (antes daba null por estar al inicio).
  - Nombre `vacaciones.jpg` → sin fecha, sin señal.
  - Nombre `ChatGPT Image 10 abr 2025, 11_03_36.png` → P5 captura `10 abr 2025` → fecha 2025-04-10.
  - Nombre `ChatGPT Image 10 Apr 2025, 11_03_36.png` → P5 captura `10 Apr 2025` → fecha 2025-04-10 (inglés).
  - Nombre `Screenshot 2025-03-15 at 10.30.45.png` → P2 captura `2025-03-15` → fecha 2025-03-15.
  - Nombre `vacaciones 2025.03.15.jpg` → P3 captura `2025.03.15` → fecha 2025-03-15.
  - Nombre `foto 15-03-2025.jpg` → P7 captura `15-03-2025` → fecha 2025-03-15.
  - Nombre `15.03.2025 cumpleaños.jpg` → P8 captura `15.03.2025` → fecha 2025-03-15.
  - Nombre `abr 10 2025 selfie.jpg` → P6 captura `abr 10 2025` → fecha 2025-04-10.
  - Nombre `20250315_123456.jpg` sin metadatos → sugerencia 2025-03-15 05:00 (P1).
- **Estado:** activa.

## 7) Nota sobre dataset de validación
- El dataset de validación real se incorporará progresivamente.
- Mientras tanto, los escenarios relevantes se cubrirán mediante tests unitarios representativos.
