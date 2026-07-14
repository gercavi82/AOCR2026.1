# GATE 5 — Reevaluación

## Resultado

- La aceptación de la subsanación prepara un ciclo idempotente con un Informe Técnico nuevo.
- La LV/EAE se versiona cuando el trámite utiliza ese flujo.
- Informe y LV/EAE guardan antecedente, NC de origen y número de ciclo; no se sobrescriben documentos anteriores.
- Una reevaluación insatisfactoria exige `CON_INSPECCION` o `SIN_INSPECCION` y crea una NC formal de versión/ciclo siguiente.
- Una reevaluación satisfactoria solo cierra la NC con informe finalizado, firma del inspector y hash documental.
- La generación de AOCR queda bloqueada por NC abiertas de la inspección original o de la nueva inspección. Un error de lectura también bloquea (fail closed).

## SQL

- `scripts/sql/019_gate5_reevaluacion.sql`
- `scripts/sql/019_gate5_reevaluacion_rollback.sql` (no elimina columnas ni evidencia histórica)

## Verificación

- Build Debug: aprobado.
- Build Release: aprobado.
- Migración aplicada dos veces: aprobada.
- Pruebas focales: 2/2 aprobadas.
- Regresión: 278 ejecutadas, 258 aprobadas, 19 fallidas externas/preexistentes y 1 omitida.
- TRX: `TestResults/gate5-integration.trx` y `TestResults/gate5-global-final.trx`.

La línea base histórica 91/588 permanece descartada por no existir un comando reproducible que la demuestre.
