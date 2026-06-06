# SnapTime — Proceso de desarrollo (TDD)

## 1) Política general

El desarrollo sigue **Test-Driven Development (TDD)** estricto, con el ciclo **🔴 Red → 🟢 Green → 🔵 Refactor** en cada cambio funcional. Todos los tests son unitarios (xUnit, NSubstitute). La suite completa debe estar siempre en verde al cerrar cualquier paso.

## 2) Ciclo obligatorio (Red-Green-Refactor)

### 🔴 Fase Red — Escribir test fallido

1. Entender el requisito de la especificación.
2. Escribir **un** test unitario que describa el comportamiento esperado.
3. El test debe fallar por el motivo correcto (funcionalidad no implementada, no error de sintaxis).
4. **No escribir código de producción todavía.**

### 🟢 Fase Green — Implementación mínima

1. Escribir el **mínimo** código necesario para que el test pase.
2. No optimizar, no refactorizar, no anticipar requisitos futuros.
3. Ejecutar la suite completa para confirmar que nada se rompe.
4. **No modificar el test.** El test es la especificación.

### 🔵 Fase Refactor — Mejorar calidad

1. Con todos los tests en verde, limpiar el código:
   - Eliminar duplicación.
   - Mejorar nombres y estructura.
   - Aplicar SOLID si procede.
   - Revisar seguridad (inputs, secrets, errores).
2. Ejecutar la suite completa tras cada cambio.
3. Si algo se rompe, revertir el refactor o corregirlo antes de seguir.

## 3) Reglas de ejecución

- Ejecutar la **suite completa** de tests con la mayor frecuencia posible.
- Excepción: durante la creación de nuevos tests (Fase Red), se puede ejecutar un subconjunto para iterar más rápido.
- Al cerrar cualquier paso de programación, la suite completa debe estar en **verde**.
- Si un test falla inesperadamente, detenerse y diagnosticar antes de continuar.

## 4) Agente responsable

**Janus** es el agente TDD del equipo SnapTime. Sigue este ciclo para todo el código que genera o valida. Los tests se escriben en `tests/SnapTime.Domain.Tests/` usando xUnit + NSubstitute.

## 5) Alcance de pruebas

- **Backend:** tests unitarios (xUnit + NSubstitute) + tests de integración con SQLite real.
- **Heurísticas:** tests unitarios parametrizados (`[Theory]` + `[InlineData]`).
- **UI (componentes Blazor):** bUnit para tests unitarios de componentes (render, eventos, lógica visual).
- **UI (end-to-end):** Playwright para tests E2E con navegador real (flujos completos, interacción con API real).
- Los tests E2E dependen de que el servidor API y el cliente WASM estén en ejecución.

## 6) Base de datos durante el desarrollo

- La base de datos puede evolucionar libremente.
- Las migraciones de EF Core se generan automáticamente desde las POCOs.
- No hay freeze de esquema hasta el lanzamiento.
