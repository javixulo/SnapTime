# SnapTime — Copilot Instructions

## Reglas generales

- **No hacer commit sin permiso explícito del usuario.** Antes de ejecutar `git commit` o `git push`, pregunta siempre y espera confirmación.
- No modificar archivos de documentación sin preguntar primero.
- No añadir dependencias externas sin validación previa.
- Cualquier cambio en el stack tecnológico, arquitectura o decisiones de diseño debe discutirse antes de implementarse.

## Proyecto

SnapTime es una aplicación local-first para analizar bibliotecas de fotografías y validar/corregir fechas de captura. Backend en C#/.NET, UI en Blazor WASM, BD SQLite con EF Core code-first. Ver `docs/` para documentación completa.
