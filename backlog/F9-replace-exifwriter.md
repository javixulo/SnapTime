# F9 — Reemplazar ExifWriter custom por Magick.NET

> El `ExifWriter` actual usa manipulación binaria TIFF/JPEG casera, lo que ha causado corrupción de imágenes en múltiples ocasiones (commits `18fccba`, `8306a40`) y tiene un bug donde la anotación UserComment no se escribe cuando el archivo ya tiene EXIF con DateTimeOriginal (ruta `TryUpdateDateTimeOriginal`). Se reemplaza por **Magick.NET** (wrapping ImageMagick), librería madura y ampliamente usada que maneja correctamente toda la estructura JPEG/EXIF/XMP/ICC sin que toquemos un byte.

**Referencias:** FR-11, docs/03-blueprint-flujo-modulos-y-fases.md §4.1, backlog/F8-apply-changes.md

**Dependencias:** F8 (ApplyService, IExifWriter existentes)

---

## Visión general

### Problemas del ExifWriter actual

1. **Corrupción de JPEGs**: la manipulación binaria directa del formato TIFF dentro de JPEG es extremadamente frágil. Ya ha habido 2 hotfixes por errores de offset/length/endianness.
2. **Bug en in-place update**: `TryUpdateDateTimeOriginal` (`ExifWriter.cs:79`) solo sobreescribe DateTimeOriginal, **nunca escribe UserComment**. La anotación (fecha original + heurísticas) se pierde en el caso más común (fotos de cámara/móvil con EXIF existente).
3. **Mantenimiento**: cualquier cambio futuro (soporte de vídeo, XMP, IPTC) requeriría implementar desde cero formatos binarios complejos.
4. **Windows no muestra UserComment**: aunque el tag se escriba, Windows Explorer no muestra `UserComment` en propiedades estándar.

### Solución: Magick.NET

**Magick.NET-Q16-AnyCPU** (v14.14.0, junio 2026) es el wrapping oficial de ImageMagick para .NET:
- ~9700 descargas/día, 14 años de desarrollo
- Soporta `net8.0` y `netstandard2.0`
- Incluye native bindings para Windows, Linux, macOS (x64, ARM64, x86)
- API `ExifProfile` para lectura/escritura de cualquier tag EXIF
- Maneja automáticamente: thumbnails, perfiles ICC, XMP, IPTC, estructuras TIFF/JPEG/PNG/WebP/HEIC/etc.
- Apache 2.0 license

### Cambios:

| Archivo | Acción |
|---------|--------|
| `Directory.Packages.props` | Añadir `Magick.NET-Q16-AnyCPU` |
| `src/SnapTime.Infrastructure/SnapTime.Infrastructure.csproj` | Añadir PackageReference |
| `src/SnapTime.Infrastructure/Services/ExifWriter.cs` | Reescribir usando `MagickImage` + `ExifProfile` |
| `src/SnapTime.Domain/Interfaces/IExifWriter.cs` | Sin cambios (el contrato no cambia) |
| `tests/SnapTime.Tests/Services/ExifWriterTests.cs` | Reescribir tests (misma cobertura) |
| `tests/SnapTime.IntegrationTests/Services/ApplyServiceTests.cs` | Sin cambios (usa interfaz, no impl) |

### Consideraciones de seguridad/rendimiento

- Magick.NET carga native DLLs al iniciar. Cada `MagickImage` debe disponerse con `using`.
- No hay riesgos de seguridad adicionales vs. manipulación binaria (ImageMagick es más seguro porque valida estructuras).
- El tamaño del paquete NuGet es ~30MB (native binaries incluidas). Aceptable para una app de escritorio.

---

## F9-US-001 — Migrar ExifWriter a Magick.NET ✅ COMPLETADO

### Tareas

**🔴 T-001 — Reescribir tests (Janus)**
- Reescribir `ExifWriterTests.cs` usando Magick.NET para verificar la escritura:
  - Escribe DateTimeOriginal (0x9003) correctamente
  - Escribe UserComment (0x9286) con formato `SnapTime;original=...;heuristics=...`
  - originalDate null → `original=unknown`
  - heuristicIds vacío → `heuristics=none`
  - Múltiples heuristicIds separados por coma
  - Archivo inexistente → error "File not found"
  - Archivo readonly → error "File is read-only"
  - JPEG inválido → error (no JPEG)
  - Vídeo MP4 → error "not yet implemented" (QuickTime pendiente)
- **CRÍTICO**: Test que verifica que UserComment se escribe incluso cuando el JPEG ya tiene EXIF con DateTimeOriginal (el bug actual)
- Test que verifica que todos los demás tags EXIF existentes se preservan (no se pierde nada)

**🟢 T-002 — Implementar ExifWriter con Magick.NET (Kip)**
- Reemplazar todo el contenido de `ExifWriter.cs`:
  ```csharp
  using ImageMagick;

  public class ExifWriter : IExifWriter
  {
      public async Task<ExifWriteResult> WriteAsync(
          string filePath, MediaType mediaType,
          DateTime newDate, DateTime? originalDate,
          IReadOnlyList<string> heuristicIds, CancellationToken ct)
      {
          // 1. Validar archivo existe y no readonly
          // 2. Si Image: usar MagickImage
          //    - leer con new MagickImage(filePath)
          //    - ExifProfile.GetValue(ExifTag.DateTimeOriginal) / SetValue
          //    - ExifProfile.GetValue(ExifTag.UserComment) / SetValue
          //    - image.Write(filePath)
          // 3. Si Video: return not implemented
      }
  }
  ```
- Añadir `using` para Magick.NET en Infrastructure
- Registrar en DI (no cambia, ya está `AddScoped<IExifWriter, ExifWriter>`)

**🔵 T-003 — Refactor y cleanup (Kip)**
- Eliminar métodos muertos del ExifWriter anterior:
  - `WriteJpegAsync`
  - `TryUpdateDateTimeOriginal`
  - `FindTagValueOffset`, `FindTagDataOffset`
  - `GetTypeSize`
  - `ReadU16`, `ReadU32`
  - `BuildAnnotation`, `BuildApp1Segment`
  - `ReplaceSegment`, `InsertAfterMarker`, `FindMarker`
- Eliminar `ReadUserCommentFromJpeg` del test helper (ya no necesario, Magick.NET lee solo)

**🔴 T-004 — Tests de integración con archivo real con EXIF (Janus)**
- Usar un JPEG real con EXIF completo (DateTimeOriginal, Make, Model, GPS, thumbnail, ICC)
- Aplicar ExifWriter con nueva fecha + heurísticas
- Verificar:
  - DateTimeOriginal actualizado
  - UserComment contiene la anotación
  - Make, Model, GPS, thumbnail, ICC **preservados** (no se perdieron)

**👁 T-005 — Review (Gavin)**
- Revisar que MagickImage se usa correctamente (Dispose, using)
- Verificar que no hay fugas de recursos
- Confirmar que el bug del in-place update está resuelto (UserComment siempre se escribe)

---

## Criterios de aceptación

- [x] `Magick.NET-Q16-AnyCPU` añadido a `Directory.Packages.props`, `Infrastructure.csproj`, test projects
- [x] ExifWriter reescrito usando `ImageMagick.MagickImage` y `ExifProfile`
- [x] Todos los tests de ExifWriter (11 unitarios + 7 integración) pasan con la nueva impl
- [x] UserComment se escribe correctamente en TODOS los casos (con o sin EXIF preexistente) — bug del in-place update corregido
- [x] Los tags EXIF existentes (Make, Model) se preservan (test `WriteAsync_PreservesOtherExifTags`)
- [x] Video → sigue devolviendo "not yet implemented"
- [x] Todo el código muerto de manipulación binaria eliminado (274 líneas → <50 líneas)
- [x] `dotnet build` y `dotnet test` (unit + integración) pasan
- [x] Gavin aprueba

---

## Notas

- El contrato `IExifWriter` no cambia, solo la implementación.
- No hay cambios en API, frontend, DB, ni ApplyService.
- No se requiere migración de BD.
- Las dependencias nativas de Magick.NET se descargan automáticamente vía NuGet.

## Integración con docs

- `docs/01-requisitos-funcionales.md` FR-11: actualizar "campo EXIF UserComment (0x9286)" con nota de que usa Magick.NET
- `docs/03-blueprint-flujo-modulos-y-fases.md` §4.1: añadir que la escritura se hace vía Magick.NET
- `backlog/F8-apply-changes.md` F8-US-002: marcar como obsoleto el enfoque de manipulación binaria
