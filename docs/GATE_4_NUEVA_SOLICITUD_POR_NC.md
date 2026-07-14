# GATE 4 — Creación real de nueva solicitud por NC

## Implementación

`NuevaInspeccionPorNcService` reemplaza la clonación aislada de una orden realizada por
`RTController.SolicitarNuevaInspeccion`. La operación ahora:

1. bloquea y valida la NC `CON_INSPECCION` firmada por Coordinación;
2. valida que el RT sea propietario de la solicitud original;
3. retorna la solicitud activa existente cuando la NC ya originó una;
4. crea una solicitud institucional nueva precargando información compatible;
5. relaciona solicitud, inspección, informe y NC originales mediante FKs;
6. crea una inspección en estado `CREADA` para la nueva solicitud;
7. prepara una orden en borrador usando las reglas/datos de recaudación existentes cuando existe una orden base;
8. enlaza la NC con la solicitud e inspección nuevas;
9. registra historial y encola una notificación.

Solicitud, inspección, orden, historial y relaciones se crean en una sola transacción PostgreSQL. Una excepción
revierte toda la operación.

## Idempotencia

El índice parcial único `ux_solicitud_activa_nc_gate4` impide más de una solicitud activa por NC. El servicio
consulta primero el vínculo y también captura la colisión `23505`; en ambos casos retorna la solicitud existente
sin crear otra solicitud, inspección u orden.

## Destino y precarga

- `EMISION` y `RENOVACION`: `M5_SOLICITUD_INSPECCION_EMISION_RENOVACION`; continúa en el panel de Solicitud de
  Inspecciones asociado a la orden.
- `MODIFICACION_CON_NUEVO_AEROPUERTO`: `M6_SOLICITUD_INSPECCION_MODIFICACION`; continúa en la inspección creada.

Se precargan compañía, RUC, operador, RT, tipo de trámite, aeropuertos y datos administrativos compatibles. La
fecha programada queda expresamente en `PENDIENTE_PROGRAMACION`. No se copian facturas, comprobantes, pagos,
documentos financieros ni viáticos del ciclo anterior.

## SQL y pruebas

- `scripts/sql/017_gate4_nueva_solicitud_por_nc.sql` ejecutado repetidamente sin errores.
- `scripts/sql/017_gate4_nueva_solicitud_por_nc_rollback.sql` validado y migración reaplicada.
- `scripts/sql/018_gate4_destino_modulo_inspeccion.sql` registra y restringe el módulo destino.
- `Gate4NuevaSolicitudPorNcIntegrationTests`: esquema/FKs/índices, entrada inválida sin efectos e índice activo.
