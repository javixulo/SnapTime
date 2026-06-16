# F10 — Limpieza de datos de escaneo

> Botón "Limpiar" que elimina todos los datos de escaneo de la base de datos (assets, metadatos, evidencias, auditoría), conservando la configuración.

**Referencias:** FR-21, docs/06-requisitos-ui.md §4.4

**Dependencias:** F0 (API, DB)

---

## Visión general

El usuario necesita una forma de resetear el estado del programa después de un escaneo, eliminando todos los datos generados pero manteniendo la configuración.

### Reglas base

- Elimina todos los `MediaAssets`, `MetadataEntries`, `EvidenceEntries` y `AuditEntries`.
- Elimina también los `ScanJobs` (el historial de trabajos de escaneo).
- **Conserva** la configuración: `Settings`, `SnapTimeConfig`.
- Requiere confirmación explícita del usuario antes de ejecutar.
- Botón "Limpiar" en el panel superior, siempre activo cuando hay datos escaneados.
- Al completar la limpieza: grid de fotos se vacía, estado del scan vuelve a idle.

---

## US-001 — Endpoint POST /api/clear 🔴 Pendiente

### Tareas

**🔴 T-001 — Tests de integración (Janus)**
- POST /api/clear → 200 OK, datos eliminados
- POST /api/clear con DB vacía → 200 OK (no falla)
- Verificar que Settings y configuración se conservan
- Verificar que ScanJobs se eliminan

**🟢 T-002 — Implementación endpoint (Kip)**
- Minimal API `POST /api/clear`
- Con `DbContext.Database.EnsureCreated()` y `ExecuteDelete`/`ExecuteSqlRaw` para borrado rápido
- Orden de borrado: MetadataEntries → EvidenceEntries → MediaAssets → AuditEntries → ScanJobs
- No usar transacción (best-effort, datos de escaneo prescindibles)

### Criterios de aceptación
- [ ] Endpoint POST /api/clear responde 200 OK
- [ ] Datos de escaneo eliminados, configuración intacta
- [ ] Tests de integración pasan

---

## US-002 — Botón Limpiar en UI 🔴 Pendiente

### Tareas

**🔴 T-001 — Tests bUnit (Janus)**
- Botón "Limpiar" visible en top bar
- Al pulsarlo, se abre modal de confirmación
- Al confirmar, llama a POST /api/clear
- Al completar, grid se vacía

**🟢 T-002 — Implementación botón (Karris)**
- Añadir botón "Limpiar" en `BatchActions.razor` o en `Home.razor` (panel superior)
- Botón siempre activo (no depende de scan state)
- Modal de confirmación: "¿Estás seguro de que quieres eliminar todos los datos de escaneo?"
- Al confirmar: POST /api/clear → refrescar grid → resetear estado

### Criterios de aceptación
- [ ] Botón "Limpiar" visible en panel superior
- [ ] Modal de confirmación aparece al pulsarlo
- [ ] Al confirmar, datos se limpian y grid se vacía
- [ ] Tests bUnit pasan

---

## US-003 — Tests E2E 🟡 En curso

### Tareas

**🔴 T-001 — E2E limpieza completa**
- Playwright: scan → limpiar → verificar grid vacío
