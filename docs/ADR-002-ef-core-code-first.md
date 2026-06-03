# ADR-002: EF Core code-first con POCO para persistencia

## Estado
Aceptado

## Contexto
SnapTime necesita persistencia local en SQLite. Se busca evitar el bloqueo por definición anticipada del esquema y permitir iterar sobre el modelo de datos conforme evoluciona el desarrollo.

## Decisión
Usar Entity Framework Core con enfoque code-first. Las clases POCO en C# son la fuente de verdad del esquema. EF Core genera migrations y actualiza la BD automáticamente.

## Consecuencias
- **Positivas:** sin archivos SQL manuales; el código ES el esquema; migrations auto-generadas; cambios iterativos sin fricción.
- **Negativas:** dependencia de EF Core; migrations manuales necesarias en cambios estructurales complejos.
- **Neutras:** el DbContext se configura en el proyecto de Infraestructura; las POCOs viven en Domain.

## Alternativas consideradas
- **Dapper + SQL manual:** máximo control pero requiere mantener esquemas SQL a mano (bloqueante).
- **SQLite raw con ADO.NET:** mínimo overhead pero sin migraciones ni mapeo automático.
- **LiteDB:** BD NoSQL embedded, pero se prefirió SQLite por ser el estándar del proyecto.

## Referencias
- docs/07-decisiones-tecnologicas.md (§3)
- docs/03-blueprint-flujo-modulos-y-fases.md (§1)
