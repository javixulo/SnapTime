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

## 6) Nota sobre dataset de validación
- El dataset de validación real se incorporará progresivamente.
- Mientras tanto, los escenarios relevantes se cubrirán mediante tests unitarios representativos.
