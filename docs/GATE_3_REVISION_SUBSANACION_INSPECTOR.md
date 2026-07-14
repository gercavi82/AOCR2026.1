# GATE 3 — Revisión de subsanación por Inspector

## Resultado funcional

El Inspector asignado revisa cada versión individual vinculada a la NC `SIN_INSPECCION`. El panel permite
comparar y descargar de forma autorizada la versión anterior y la nueva, consultar la observación y aceptar o
rechazar cada documento.

- Aceptación: `ACEPTADO_SUBSANACION`.
- Rechazo: `RECHAZADO_SUBSANACION`, con comentario obligatorio.
- Un rechazo cambia la NC a `SUBSANACION_DEVUELTA`, notifica al RT y habilita una nueva versión.
- Todas las versiones permanecen almacenadas y enlazadas.
- El cierre considera las hojas vigentes de cada cadena de versiones y se bloquea si alguna está pendiente o
  rechazada, o si no existe ninguna nueva versión.

Cuando todas las hojas vigentes están aceptadas, una única transacción cambia la NC a
`SUBSANACION_ACEPTADA`, la inspección a `EN_INSPECCION`, registra auditoría y notifica al Inspector que debe
elaborar un nuevo Informe Técnico.

## Regla crítica

El cierre no actualiza `resultado` ni `resultado_evaluacion` de la inspección. La documentación aceptada solo
habilita la reevaluación; nunca convierte automáticamente el resultado en satisfactorio.

## SQL y pruebas

- Migración: `scripts/sql/016_gate3_revision_subsanacion_inspector.sql`.
- Rollback no destructivo: `scripts/sql/016_gate3_revision_subsanacion_inspector_rollback.sql`.
- La migración se ejecutó dos veces sin errores; el rollback fue ejecutado y la migración reaplicada.
- Pruebas reales: `Gate3RevisionSubsanacionInspectorIntegrationTests` (3/3 aprobadas).
