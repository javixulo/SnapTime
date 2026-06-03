# SnapTime - Proceso de desarrollo (TDD)

## 1) Política general
El desarrollo se realizará con enfoque TDD y cobertura centrada en tests unitarios.

## 2) Ciclo obligatorio por cada cambio funcional
1. Escribir tests unitarios que describan el comportamiento esperado.
2. Ejecutar tests nuevos para confirmar que fallan por el motivo correcto.
3. Implementar el mínimo código necesario para hacerlos pasar.
4. Ejecutar la suite completa de unit tests.
5. Refactorizar manteniendo comportamiento.
6. Ejecutar de nuevo la suite completa de unit tests.

## 3) Reglas de ejecución de tests
- Regla general: ejecutar todos los unit tests con la mayor frecuencia posible.
- Excepción permitida: durante la fase inicial de creación de nuevos tests, se puede ejecutar un subconjunto para iterar más rápido.
- Al cerrar cualquier paso de programación, la suite completa debe estar en verde.

## 4) Alcance de pruebas en esta etapa
- Solo se contemplan tests unitarios.
- Los escenarios de validación de heurísticas se codificarán como casos unitarios representativos.
- Se pospone la definición de tests de integración y end-to-end para fases futuras, si fueran necesarios.

## 5) Base de datos durante el desarrollo
- La base de datos puede evolucionar libremente durante el desarrollo.
- El estado estable de esquema coincide con la versión final del producto.
- No se establece un hito de freeze de base de datos independiente.
