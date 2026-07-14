# GATE 6 — Módulos 7 y 8 por tipo de trámite

## Decisión central

`AocrCierrePorTipoTramiteService` es la fuente única para decidir los documentos de cierre:

| Trámite | Módulo | Documentos obligatorios |
|---|---|---|
| EMISION | 7 | AOCR + Condiciones y Limitaciones |
| RENOVACION | 7 | AOCR + Condiciones y Limitaciones |
| MODIFICACION | 8 | Condiciones y Limitaciones |
| MODIFICACION_CON_NUEVO_AEROPUERTO | 8 | Condiciones y Limitaciones, después de inspección satisfactoria |

No se encontró una regla institucional documentada que autorice emitir un AOCR nuevo para modificaciones; por ello se bloquea en generación y firma.

## Integración

- `GeneracionAOCRService` rechaza AOCR para trámites del Módulo 8.
- `FirmaAocrController` vuelve a validar el tipo antes de firmar, incluso ante documentos heredados.
- `AocrFinalizacionService` exige dos documentos para Módulo 7 y solo Condiciones para Módulo 8.
- `AocrProcesoNotificacionService` libera y adjunta únicamente los documentos correspondientes, notifica al RT y genera notificación interna para el Inspector asignado.
- Se conservan las transiciones existentes de Coordinador y DCAV/DIRDAC.
- Las NC abiertas continúan bloqueando el cierre.

## Verificación

- Build Debug y Release: aprobados.
- Pruebas focales: 15/15.
- Regresión: 281 ejecutadas, 261 aprobadas, 19 fallidas preexistentes y 1 omitida.
- Resultados: `TestResults/gate6-focal.trx`, `TestResults/gate6-global-final.trx`.
