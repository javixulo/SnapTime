# F3 — Motor de heurísticas (H-001 a H-006) — H-006 actualizado a multipatrón

> Implementar el motor de heurísticas que evalúa la confianza de la fecha canónica de cada archivo y genera sugerencias de corrección. Esta feature abarca TODAS las heurísticas (H-001 a H-006), pero se implementan de una en una.

**Referencias:**
- `docs/05-requisitos-heuristicas.md` — especificación completa

**Dependencias:** F1 (metadatos EXIF/QuickTime + filesystem en SQLite), F2 (SQLite en tests de integración)

**Arquitectura:**

```csharp
// [F3-US-001]
public interface IHeuristic
{
    string Id { get; }             // "H-006"
    string Name { get; }           // "ParseFilenameDateHeuristic"
    bool IsEnabled { get; }
    Task<EvidenceEntry?> EvaluateAsync(
        MediaAsset asset,
        IReadOnlyList<MetadataEntry> metadata,
        CancellationToken ct);
}
```

Cada heurística implementa `IHeuristic`. El motor itera las heurísticas activas y recolecta `EvidenceEntry` por archivo. El pipeline de F1 se extiende para ejecutarlas tras la extracción de metadatos.

---

## F3-US-001 — Interfaz IHeuristic + H-006 (fecha desde filename) ✅ COMPLETADO

### Tareas

**🔴 T-001 — Tests de H-006 (Janus)** ✅
- Tests unitarios para `H006FilenameHeuristic` (7 casos de `docs/05`):
  1. `20250315_123456.jpg`, metadatos mismo día → `EvidenceEntry` con `Direction = Positive`
  2. `20250315_123456.jpg`, metadatos 2024-07-10 → `Correction`, `SuggestedDate = 2025-03-15 05:00`
  3. `20250315_123456.mp4`, metadatos mismo día → `Positive` (vídeo)
  4. `20250315_123456.mp4`, sin metadatos → `Correction`, `SuggestedDate = 2025-03-15 05:00`
  5. `IMG_20250315.jpg` → `null` (prefijo variable) ⚠️ OBSOLETO con H-006 multipatrón — P1 ahora captura `20250315` en cualquier posición
  6. `vacaciones.jpg` → `null` (sin fecha)
  7. `20250315_123456.jpg`, sin metadatos → `Correction`, `SuggestedDate = 2025-03-15 05:00`
- Test: el método recibe `(string fileName, DateTime? canonicalDate)` o `(MediaAsset, IReadOnlyList<MetadataEntry>, CancellationToken)` según diseño

**🟢 T-002 — Implementar IHeuristic + H006FilenameHeuristic (Kip)** ✅
- `IHeuristic` en `Domain/Interfaces/`
- `H006FilenameHeuristic` en `Domain/Services/`:
  - Parsear `yyyyMMdd` (8 dígitos) al inicio del filename sin extensión
  - Fallback `yyyy-MM-dd`
  - Resolver fecha canónica desde `metadata` usando lista de prioridad (F1)
  - Comparar año/mes/día. Si coinciden → `EvidenceEntry(Direction.Positive)`. Si no o sin metadatos → `EvidenceEntry(Direction.Correction, SuggestedDate = fecha + 5:00)`. Sin fecha en filename → `null`
- Hacer pasar los 7 tests

**🔵 T-003 — Refactor (Kip)** ✅
- Mejorar legibilidad, nombres, estructura del heuristic
- Tests deben seguir verdes

**👁 T-004 — Review (Gavin)** ✅
- Revisar calidad, cobertura, arquitectura

---

## F3-US-002 — Integrar H-006 en el pipeline de jobs ✅ COMPLETADO

**Estado actual:** Implementado en `ScanJobService.ProcessSingleFileAsync`.

- [x] En `ScanJobService.ProcessSingleFileAsync`, tras extraer metadatos EXIF/QuickTime + filesystem, ejecutar todas las `IHeuristic` activas
- [x] Cada `EvidenceEntry` generado se agrega al `MediaAsset.EvidenceEntries`
- [x] Los `EvidenceEntry` se persisten junto con el asset en el checkpoint
- [x] Tras las heurísticas, se ejecuta `HeuristicEngine` para calcular `ConfidenceScore`, `AnalysisStatus`, `SuggestedDate` y `SuggestionReviewStatus`

**Criterios de aceptación:**
- [x] Al escanear una foto con filename `20250315_123456.jpg` y EXIF con fecha distinta → el asset persistido tiene un `EvidenceEntry` en BD
- [x] Test de integración con SQLite real que verifica el ciclo completo

---

## F3-US-003 — H-001 (consistencia entre EXIF:DateTimeOriginal, CreateDate y ModifyDate) ⏳ Pendiente

> Evaluar si las tres fechas EXIF principales son consistentes entre sí.

**Requiere ficha detallada** antes de implementar (similar a H-006 en docs/05).

---

## F3-US-004 — H-002 (consenso temporal en carpeta/lote) ⏳ Pendiente

> Agrupar archivos por carpeta y detectar la tendencia temporal del grupo.

**Requiere ficha detallada.**

---

## F3-US-005 — H-003 (detección de outlier temporal) ⏳ Pendiente

> Detectar archivos cuya fecha canónica se desvía significativamente del resto del lote.

**Requiere ficha detallada.**

---

## F3-US-006 — H-004 (pistas temporales en nombre de carpeta) ⏳ Pendiente

> Extraer fechas del nombre de la carpeta como evidencia blanda.

**Requiere ficha detallada.**

---

## F3-US-007 — H-005 (coherencia con metadatos de filesystem) ⏳ Pendiente

> Comparar fecha canónica contra `ctime`/`mtime` del sistema de archivos.

**Requiere ficha detallada.**

---

## F3-US-008 — H-006: extensión a multipatrón (fechas en cualquier posición + meses textuales ES/EN) ✅ COMPLETADO

> Extender H-006 para buscar patrones de fecha en cualquier posición del nombre del archivo (no solo al inicio) y soportar 9 patrones distintos incluyendo meses abreviados en español e inglés.

**Referencia:** `docs/05-requisitos-heuristicas.md` — sección H-006 actualizada.

**Dependencias:** F3-US-001 (IHeuristic + H-006 base), F3-US-002 (pipeline)

**Cambios respecto a versión anterior:**
- Los patrones se buscan en **cualquier posición** del nombre, no solo al inicio.
- Se añaden patrones con separadores: `yyyy.MM.dd`, `yyyy_MM_dd`, `DD-MM-YYYY`, `DD.MM.YYYY`, `DD/MM/YYYY`.
- Se añaden patrones con mes textual (`DD MMM YYYY`, `MMM DD YYYY`) con soporte para abreviaturas de 3 letras en español e inglés.
- Orden de prioridad de patrones (P1→P9) definido en la especificación.
- ⚠️ El test anterior `IMG_20250315.jpg → null` ahora debe devolver `2025-03-15` (P1 captura los 8 dígitos en cualquier posición).

### Tareas

**🔴 T-001 — Actualizar tests de H-006 (Janus)**
- Agregar nuevos tests unitarios para todos los patrones nuevos:
  1. `ChatGPT Image 10 abr 2025, 11_03_36.png` → P5, extrae 2025-04-10
  2. `ChatGPT Image 10 Apr 2025, 11_03_36.png` → P5, extrae 2025-04-10 (inglés)
  3. `Screenshot 2025-03-15 at 10.30.45.png` → P2, extrae 2025-03-15
  4. `vacaciones 2025.03.15.jpg` → P3, extrae 2025-03-15
  5. `foto 15-03-2025.jpg` → P7, extrae 2025-03-15
  6. `15.03.2025 cumpleaños.jpg` → P8, extrae 2025-03-15
  7. `abr 10 2025 selfie.jpg` → P6, extrae 2025-04-10
- Actualizar el test existente #5 (`IMG_20250315.jpg`):
  - Antes: esperaba `null` (no parseaba por estar al inicio con prefijo)
  - Ahora: debe esperar `2025-03-15` (P1 captura en cualquier posición)
- Verificar que los 7 tests originales siguen pasando con la nueva lógica

**🟢 T-002 — Refactorizar H006FilenameHeuristic.TryParseDate (Kip)**
- Modificar `TryParseDate()` para que busque patrones en **cualquier posición** del string (no anclado al inicio con `^`).
- Implementar los 9 patrones (P1-P9) en el orden de prioridad especificado.
- Para P1 (`yyyyMMdd`): regex `\b\d{8}\b` o similar para capturar 8 dígitos consecutivos en cualquier posición.
- Para P5 y P6 (mes textual): implementar un diccionario de meses español/inglés para convertir abreviatura → número de mes.
- Para P7-P9 (orden DD-MM): parsear como DD-MM-YYYY usando `DateTime.TryParseExact` con formato `dd-MM-yyyy`.
- Los patrones deben probarse secuencialmente; devolver el primer match.
- La hora opcional tras la fecha debe ignorarse (solo año/mes/día se extraen).

**🔵 T-003 — Refactor (Kip)**
- Asegurar código limpio, expresiones regulares nombradas, diccionario de meses como static readonly.

**👁 T-004 — Review (Gavin)**
- Revisar que todos los patrones funcionan, cobertura de tests, casos límite.

**Criterios de aceptación:**
- [x] Los 16 tests unitarios pasan (7 originales actualizados + 9 nuevos)
- [x] El archivo `ChatGPT Image 10 abr 2025, 11_03_36.png` extrae fecha 2025-04-10 correctamente
- [x] Los tests de integración del pipeline (F3-US-002) siguen verdes (los 4 fallos son preexistentes, no relacionados)
- [x] No se rompe ninguna funcionalidad existente (90/90 tests bUnit, 42/46 integración preexistente)
