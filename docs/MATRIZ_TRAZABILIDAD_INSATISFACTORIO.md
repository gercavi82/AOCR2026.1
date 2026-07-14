# Matriz de trazabilidad — Flujo INSATISFACTORIO

| Requisito | Implementación principal | SQL | Prueba/evidencia | Estado |
|---|---|---|---|---|
| Relaciones NC y ciclos | `NoConformidadDAO`, modelos NC | 014 | Gate1 integration | Validado automáticamente |
| Versionado individual de documentos | `DocumentoDAO`, `DocumentoSubsanacionService` | 015 | Gate2 + Gate7A | Validado con fallos externos AS400 en parte del fixture |
| Revisión Inspector | `RevisionDocumentalService`, `InspeccionController` | 016 | Gate3 integration | Validado automáticamente |
| Nueva solicitud por NC | `NuevaInspeccionPorNcService` | 017 | Gate4 integration | Validado automáticamente |
| Destino Módulo 5/6 | `NuevaInspeccionPorNcService` | 018 | Gate4 destino | Validado automáticamente |
| Reevaluación y cierre NC | `ReevaluacionInspeccionService` | 019 | Gate5 integration | Validado automáticamente |
| Módulos 7/8 | `AocrCierrePorTipoTramiteService` | 013/flujo existente | pruebas M7/M8 | Validado automáticamente; E2E pendiente |
| Depuración PDF legacy | redirect canónico y DAO bloqueado | — | Gate7A | Validado automáticamente |
| Descargas seguras | `DocumentoSeguroService` | — | 15 pruebas GATE 7 | Validado automáticamente |
| Eventos idempotentes | `Gate8WorkflowEventService`, `Gate8EventoDAO` | 020 | 15 pruebas GATE 8 | Núcleo validado; integración completa pendiente |
| Correo en cola | `EmailQueueService` | 020 | idempotencia/reintentos | Validado; dos envíos legacy corregidos en GATE 9 |
| Auditoría y correlación | ledger `aocr_evento_workflow` | 020 | hash, versión, estados, correlation | Núcleo validado; cobertura operacional completa pendiente |
| Escenario A SIN_INSPECCION | múltiples módulos | 014–020 | requiere usuario/rol/navegador | Pendiente E2E |
| Escenario B CON_INSPECCION M7 | múltiples módulos | 014–020 | requiere usuario/rol/navegador | Pendiente E2E |
| Escenario C CON_INSPECCION M8 | múltiples módulos | 014–020 | requiere usuario/rol/navegador | Pendiente E2E |
| Escenario D reincidencia | NC/reevaluación | 014–020 | requiere datos y navegador | Pendiente E2E |

La correlación esperada es: NC → subsanación → solicitud nueva → inspección nueva → reevaluación → cierre. El ledger almacena IDs de solicitud, inspección, informe, NC y documento, además de estado anterior/nuevo, usuario, rol, IP, versión, hash, resultado y error.

