# F3 — Heurística H-006 (fecha desde filename)

> Implementar la heurística que parsea el nombre del archivo como `yyyyMMdd`, lo compara con `DateTimeOriginal`, y genera una sugerencia si hay mismatch o EXIF ausente.

**Referencias:**
- `docs/05-heuristicas.md` — especificación completa
- H-006: `ParseFilenameDateHeuristic`

**Dependencias:** F1 (necesita metadatos EXIF en SQLite)

**Reglas base:**
- Patrón `yyyyMMdd` en el filename (ej: `20210405_123456.jpg` → `2021-04-05`)
- Si EXIF tiene fecha pero filename tiene otra → sugerencia con filename date + 5:00 AM
- Si EXIF no tiene fecha (null) → sugerencia con filename date + 5:00 AM
- Si filname no tiene fecha → sin sugerencia
- Si EXIF y filename coinciden en año/mes/día → no genera sugerencia

**Contrato (pendiente de desglosar en US):**
- Implementar `IHeuristic` con método `Task<EvidenceEntry?> EvaluateAsync(Photo photo, CancellationToken ct)`
- Pipeline de F1 extendido para evaluar heurísticas tras la extracción
- Tests con fotos cuyo filename coincide / no coincide / no tiene fecha
