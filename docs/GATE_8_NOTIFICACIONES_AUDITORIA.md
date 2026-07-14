# GATE 8 — Notificaciones y auditoría

Fecha: 2026-07-14  
Rama: `firma-dirdac-tec`  
Commit base: `da964be92ca99370c92e4aa3e0c29f283fcd2cb0`

## Arquitectura

`Gate8WorkflowEventService` se ejecuta después del commit de negocio. Primero registra el evento en `aocr_evento_workflow`; la restricción única de `event_key` determina si es un evento efectivo o un reintento. Después procesa de forma aislada la notificación interna, la cola de correo y la auditoría. Un fallo de cualquier canal deja `PENDIENTE_REINTENTO` y `detalle_error`, incrementa `intentos` en posteriores llamadas y nunca lanza hacia la operación principal.

El `correlation_id` existente se conserva; si el inicio del ciclo no lo aporta, se crea una única correlación basada en la NC y debe propagarse a solicitud, inspección, informe, cierre y `email_queue`. La migración es idempotente y su rollback es no destructivo.

## Matriz de eventos

| Evento | Emisor | Destinatario | Canal | event_key | Entidad | Estado previo | Estado posterior | Prueba |
|---|---|---|---|---|---|---|---|---|
| `NC_GENERADA` | Inspector | Inspector | Interno/auditoría | `NC_GENERADA:{nc}:{version}` | NC | — | GENERADA | versión/idempotencia |
| `NC_FIRMADA_INSPECTOR` | Inspector | Coordinador | Interno+correo | `NC_FIRMADA_INSPECTOR:{nc}:{version}:{hash}` | NC | GENERADA | FIRMADA_INSPECTOR | hash/versión |
| `NC_ENVIADA_COORDINADOR` | Inspector | Coordinador | Interno+correo | `NC_ENVIADA_COORDINADOR:{nc}:{version}` | NC | FIRMADA_INSPECTOR | ENVIADA_COORDINADOR | Coordinador recibe NC |
| `NC_DEVUELTA_INSPECTOR` / `NC_CORREGIDA_INSPECTOR` | Coordinador/Inspector | Inspector/Coordinador | Interno+correo | `{evento}:{nc}:{version}` | NC | revisión/devuelta | devuelta/corregida | nueva versión |
| `NC_APROBADA_COORDINADOR` / `NC_FIRMADA_COORDINADOR` | Coordinador | RT | Interno+correo | `{evento}:{nc}:{version}:{hash}` | NC | enviada/aprobada | aprobada/firmada | hash/versión |
| `NC_NOTIFICADA_RT` | Coordinador | RT propietario | Interno+correo | `NC_NOTIFICADA_RT:{nc}:{version}` | NC | FIRMADA_COORDINADOR | NOTIFICADA_RT | RT expediente propio |
| `SUBSANACION_INICIADA` | RT | RT | Auditoría | `SUBSANACION_INICIADA:{nc}:{ciclo}` | NC | NOTIFICADA_RT | EN_SUBSANACION | correlación |
| `DOCUMENTO_SUBSANADO_RT` | RT | Inspector asignado | Interno | `DOCUMENTO_SUBSANADO_RT:{nc}:{documento}:{version}` | Documento | observado | nueva versión | versión diferente |
| `SUBSANACION_ENVIADA_INSPECTOR` | RT | Inspector asignado | Interno+correo | `SUBSANACION_ENVIADA_INSPECTOR:{nc}:{ciclo}` | NC | EN_SUBSANACION | EN_REVISION_INSPECTOR | Inspector recibe |
| `DOCUMENTO_SUBSANADO_ACEPTADO` / `RECHAZADO` | Inspector | RT | Interno+correo al rechazo | `{evento}:{nc}:{documento}:{version}` | Documento | pendiente | aceptado/rechazado | destinatario RT |
| `SUBSANACION_DEVUELTA_RT` / `SUBSANACION_ACEPTADA` | Inspector | RT/Inspector | Interno+correo | `{evento}:{nc}:{ciclo}` | NC | revisión | devuelta/aceptada | idempotencia |
| `NUEVO_INFORME_REQUERIDO` | Sistema | Inspector | Interno | `NUEVO_INFORME_REQUERIDO:{nc}:{ciclo}` | Informe | — | requerido | Inspector asignado |
| `NUEVA_INSPECCION_SOLICITADA` | RT | Coordinador | Interno | `NUEVA_INSPECCION_SOLICITADA:{nc}:{ciclo}` | NC | NOTIFICADA_RT | solicitada | fallo no revierte |
| `NUEVA_SOLICITUD_CREADA` | Sistema | RT | Interno+correo | `NUEVA_SOLICITUD_CREADA:{nc}:{solicitudNueva}` | Solicitud | — | creada | fallo no revierte |
| `NUEVA_ORDEN_PREPARADA` | Sistema | RT/Financiero | Interno | `NUEVA_ORDEN_PREPARADA:{nc}:{orden}` | Orden | — | preparada | idempotencia |
| `NUEVA_INSPECCION_CREADA` / `ASIGNADA` | Sistema/Coordinador | Inspector | Interno+correo | `{evento}:{nc}:{inspeccion}` | Inspección | —/pendiente | creada/asignada | Inspector recibe |
| `REEVALUACION_INICIADA` | Inspector | Inspector | Auditoría | `REEVALUACION_INICIADA:{nc}:{informe}` | Informe | requerido | en evaluación | correlación |
| `REEVALUACION_INSATISFACTORIA` / `NUEVA_NC_GENERADA` | Inspector | Coordinador | Interno+correo | `{evento}:{nc}:{informe/version}` | Informe/NC | evaluación | insatisfactoria/nueva NC | versión |
| `REEVALUACION_SATISFACTORIA` / `NC_CERRADA` | Inspector/Sistema | RT e Inspector | Interno+correo | `NC_CERRADA:{nc}:{informeCierre}` | NC | abierta | CERRADA | fallo no revierte cierre |
| `AOCR_GENERADA` / `CONDICIONES_GENERADAS` | Sistema | Coordinador | Interno | `{evento}:{solicitud}:{documento}` | Documento | — | generado | Módulos 7/8 |
| `DOCUMENTOS_ENVIADOS_COORDINADOR` | Sistema | Coordinador | Interno+correo | `DOCUMENTOS_ENVIADOS_COORDINADOR:{solicitud}:{version}` | Expediente | generado | revisión | Coordinador correcto |
| `DOCUMENTOS_ENVIADOS_DIRDAC` | Coordinador | DIRDAC/DCAV | Interno+correo | `DOCUMENTOS_ENVIADOS_DIRDAC:{solicitud}:{version}` | Expediente | revisión | firma | DIRDAC correcto |
| `DOCUMENTOS_FIRMADOS` | DIRDAC/DCAV | RT/Inspector | Interno+correo | `DOCUMENTOS_FIRMADOS:{solicitud}:{tipo/version}` | Documento | enviado | firmado | integración cierre |
| `DOCUMENTOS_LIBERADOS_RT` | Sistema | RT e Inspector | Interno+correo | `DOCUMENTOS_LIBERADOS_RT:{solicitud}:{modulo}` | Expediente | firmado | liberado | M7/M8 adjuntos |

## Validación

- 15 pruebas focales: aprobadas.
- La cola recibe el mismo `event_key` y `correlation_id` del ledger.
- Módulo 7 exige AOCR y Condiciones; Módulo 8 solo Condiciones.
- `AocrProcesoNotificacionService` registra firma y liberación mediante el ledger.
- Búsqueda estática de SMTP en controladores del flujo: sin coincidencias.
- SQL: `020_gate8_notificaciones_auditoria.sql`; rollback no destructivo: `020_gate8_notificaciones_auditoria_rollback.sql`.
