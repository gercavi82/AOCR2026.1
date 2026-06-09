# Auditoria AOCR: matriz de correos y notificaciones

Fecha: 2026-06-02

Objetivo: consolidar los eventos reales de correo/notificacion del flujo AOCR, su origen, destinatarios, canal de envio, controles de idempotencia y riesgo actual de duplicidad.

## Resumen ejecutivo

- El sistema usa tres canales en paralelo: correo directo SMTP, cola persistente `email_queue` y notificacion interna en sistema (`NotificacionBL`).
- La infraestructura de cola existe y se procesa en background desde `Global.asax`, pero no todos los eventos AOCR la usan de forma consistente.
- Los servicios nuevos de AOCR e inspeccion usan cola, pero no asignan `EventKey`; por eso no tienen deduplicacion real.
- Existe una capa adicional de correo generico por cambio de estado AOCR que se suma a los correos funcionales del workflow.
- Hay eventos concretos con `EventKey` y bajo riesgo de repeticion, pero conviven con otros de alto riesgo.
- Los adjuntos encolados no quedan garantizados si solo viajan en memoria dentro de `EmailQueueItem`.

## Canales actuales

| Canal | Implementacion actual | Persistencia | Idempotencia | Observacion |
| --- | --- | --- | --- | --- |
| Correo directo SMTP | `EnviarCorreo.enviaMensajeCorreo*` | No | No | Bloquea request y no deja trazabilidad uniforme en `email_queue`. |
| Cola persistente | `EmailQueueService.EncolarAsync` + `EmailQueueProcessor` | Si | Solo si se usa `EventKey` | Es la ruta correcta, pero no todos los servicios AOCR asignan `EventKey`. |
| Notificacion interna | `NotificacionBL.EnviarNotificacion` | Si, campana interna | N/A | En varios casos se combina con correo generico adicional. |
| Correo generico por cambio de estado | `NotificacionBL.NotificarCambioEstado` | Si, va a cola | Si, por `AOCR:CAMBIO_ESTADO:*` | Se superpone con los correos especificos por evento de AOCR. |

## Matriz consolidada

| Evento funcional | Origen real | Destinatarios reales | Canal | Idempotencia actual | Riesgo de duplicado | Observaciones |
| --- | --- | --- | --- | --- | --- | --- |
| Solicitud AOCR registrada | `SolicitudAOCRController.NotificarSolicitanteSolicitudCreada` | RT o email del solicitante | SMTP directo | No | Alto | Asunto/cuerpo hardcodeados en controlador. |
| Solicitud completada por RT, pendiente revision documental | `SolicitudAOCRController` -> `SolicitudAocrCorreoService.NotificarEvento("SOLICITUD_COMPLETADA")` | Coordinacion de inspeccion | Cola | No | Alto | No usa `EventKey`; doble submit puede reenviar. |
| Documentacion lista para revision | `SolicitudAOCRController.NotificarInspectorDocumentacionLista` | Inspector asignado + campana interna | Cola + notificacion interna | Si | Bajo | Usa `TipoNotificacion=DOCUMENTACION_LISTA_RT` y `EventKey` por solicitud+inspector. |
| Documentacion subsanada enviada al inspector | `SolicitudAOCRController` en `SubsanarPost` | Inspector asignado + campana interna | Cola + notificacion interna | Parcial | Medio | Usa `EventKey` por solicitud+inspector+documentos/versiones. |
| Solicitud Observada | `SolicitudEstadoTransitionBL.DispatchCorreoEventoPorEstado("OBSERVADA")` | RT y operador | Cola | No | Alto | Se suma al correo generico de cambio de estado AOCR. |
| Solicitud Subsanada | `SolicitudEstadoTransitionBL.DispatchCorreoEventoPorEstado("SUBSANADA")` | Inspector asignado y coordinacion | Cola | No | Alto | Se cruza con la notificacion manual `DOCUMENTACION_SUBSANADA_RT` del controlador. |
| Aceptacion documental aprobada | `SolicitudEstadoTransitionBL.DispatchCorreoEventoPorEstado("ACEPTACION_DOCUMENTAL")` | RT, inspector y coordinacion | Cola | No | Alto | Tambien convive con correo generico por cambio de estado. |
| Aceptacion documental firmada | `SolicitudAOCRController` -> `SolicitudAocrCorreoService.NotificarEvento("ACEPTACION_COORDINADOR_FIRMADA")` | RT y operador | Cola | No | Medio | Evento aislado, pero sin `EventKey`. |
| Inspector asignado | `TecnicoController` -> `SolicitudAocrCorreoService.NotificarEvento("INSPECTOR_ASIGNADO")` | Inspector, RT, operador y coordinacion | Cola | No | Alto | Reasignaciones o reenvios repiten correo. |
| Pago aprobado, modulo AOCR habilitado al RT | `AocrPostPagoWorkflowService.NotificarRtModuloHabilitado` | RT + campana interna | Cola + notificacion interna | Si | Bajo | Usa `EventKey` por solicitud+correo y marca banderas en solicitud. |
| Pago aprobado, pendiente asignacion de inspector | `AocrPostPagoWorkflowService.NotificarCoordinadoresAsignacionPendiente` | Coordinacion institucional y/o coordinadores por rol + campana interna | Cola + notificacion interna | Si | Bajo | Usa `EventKey` por solicitud+correo y marca banderas en solicitud. |
| Orden de recaudacion creada al solicitante | `OrdenRecaudacionController` -> `OrdenRecaudacionCorreoService.NotificarEvento("ORDEN_CREADA")` | Operador/RT de la orden | Cola | Si | Bajo | Usa `EventKey` por tipo+solicitud+correo. Riesgo adicional: adjunto no garantizado. |
| Orden de recaudacion generada a Financiero | `OrdenRecaudacionController` -> `OrdenRecaudacionCorreoService.NotificarEvento("ORDEN_RECAUDACION_GENERADA_FINANCIERO")` | Correo institucional FINANCIERO_AOCR | Cola | Si | Bajo | Depende de configuracion de correo institucional activa. |
| Pago registrado de orden | `OrdenRecaudacionController` -> `OrdenRecaudacionCorreoService.NotificarEvento("PAGO_REGISTRADO")` | Operador/RT y financiero | Cola | Si | Bajo | Usa `EventKey`. Riesgo adicional: adjunto en cola no garantizado. |
| Pago validado de orden | `OrdenRecaudacionCorreoService.NotificarEvento("PAGO_VALIDADO")` | Operador/RT y financiero | Cola | Si | Bajo | Preparado en servicio; revisar si todos los caminos lo invocan. |
| Factura generada | `OrdenRecaudacionCorreoService.NotificarEvento("FACTURA_GENERADA")` | Operador/RT y financiero | Cola | Si | Bajo | El servicio existe; requiere verificar disparadores concretos en financiero. |
| No conformidades generadas en inspeccion | `InspeccionWorkflowService.EmitirNotificacionEvento("NC_GENERADAS")` | RT, operador, coordinacion + campana interna | Cola + notificacion interna | No | Alto | Servicio de inspeccion no usa `EventKey`. |
| Documentos subsanados en inspeccion | `InspeccionWorkflowService.EmitirNotificacionEvento("DOCUMENTOS_SUBSANADOS")` | Inspector y coordinacion + campana interna | Cola + notificacion interna | No | Alto | Sin `EventKey`. |
| Devolucion de inspeccion | `InspeccionWorkflowService.EmitirNotificacionEvento("DEVOLUCION_INSPECCION")` | Inspector, RT, operador y coordinacion + campana interna | Cola + notificacion interna | No | Alto | Sin `EventKey`. |
| Aprobacion de inspeccion | `InspeccionWorkflowService.EmitirNotificacionEvento("APROBACION_INSPECCION")` | Coordinacion, legal, direccion + campana interna | Cola + notificacion interna | No | Alto | Sin `EventKey`. |
| Revalidacion satisfactoria | `InspeccionWorkflowService.EmitirNotificacionEvento("REVALIDACION_OK")` | Coordinacion, RT y operador + campana interna | Cola + notificacion interna | No | Alto | Sin `EventKey`. |
| Revalidacion con observaciones | `InspeccionWorkflowService.EmitirNotificacionEvento("REVALIDACION_RECHAZADA")` | Inspector, coordinacion, RT y operador + campana interna | Cola + notificacion interna | No | Alto | Sin `EventKey`. |
| Informe pendiente de revision DIRDAC / Direccion | `InspeccionController.EnviarInformeADirdacInterno` -> `InspeccionCorreoService.NotificarEvento("PENDIENTE_FIRMA_DIRDAC")` | Direccion/Jefatura institucional | Cola | No | Medio | El flujo del informe controla reenvio funcional, pero el correo no tiene `EventKey`. |
| Resultado final del informe tecnico al RT | `InspeccionController.NotificarResultadoInformeTecnicoAlRtDesdeDireccion` -> `InspeccionCorreoService.EnviarResultadoInformeTecnicoDesdeDireccion` | RT | Cola | Si | Bajo | Usa prefijo `RESULTADO_INFORME_TECNICO_DIRDAC_*` y verificacion previa en cola. |
| Informe tecnico firmado final con PDF | `InspeccionCorreoService.NotificarInformeTecnicoFirmadoFinal` | RT, coordinacion e inspector | SMTP directo con adjunto | No | Medio | No usa cola; si hay retry manual, repite envio. |
| AOCR legalizado | `AocrFinalWorkflowService.NotificarLegalizacion` -> `SolicitudAocrCorreoService.NotificarEvento("AOCR_LEGALIZADO")` | Operador, RT, coordinacion legal, direccion | Cola | No | Alto | Sin `EventKey`. |
| AOCR emitido y recibido | `AocrFinalWorkflowService.NotificarEmision` -> `SolicitudAocrCorreoService.NotificarEvento("AOCR_EMITIDO_RECIBIDO")` | Operador, RT, coordinacion legal, direccion | Cola | No | Alto | Sin `EventKey`. |
| Cambio de estado AOCR generico | `NotificacionBL.NotificarCambioEstado` | Usuario solicitante y tecnico asociado | Cola + campana interna | Si | Medio | Es idempotente por usuario+solicitud+estado, pero duplica semanticamente otros correos especificos. |
| Credenciales de usuario administrativo/interno | `AdminUsuariosBL` y `AdminUsuariosController` | Usuario destino | Cola con fallback a SMTP directo | Parcial | Medio | Usa `TipoNotificacion`, pero no `EventKey`; puede reenviar si se repite operacion. |
| Alta de usuario RT / inspector interno | `AdminUsuariosController.EnviarNotificacionAltaUsuarioInternoRT` | Inspector/usuario interno | Cola con fallback a SMTP directo | Parcial | Medio | Tipo `RT_USUARIO_CREADO`, sin `EventKey`. |
| Recuperacion de contraseña | `UsuarioBL` | Usuario destino | SMTP directo | No | Medio | Fuera del flujo AOCR principal, pero aun legacy. |
| Aceptacion RT con clave temporal | `UsuarioBL.NotificarAceptacionConClaveTemporal` | Usuario RT aprobado | SMTP directo | No | Medio | Texto parcialmente centralizado con `RtCorreoTextoHelper`. |
| Registro RT pendiente aprobacion | `UsuarioController` | Solicitante RT | SMTP directo | No | Medio | Usa helper de textos RT, pero no cola. |
| Declaracion de responsabilidad aceptada | `UsuarioController` | Solicitante RT | SMTP directo, con o sin adjunto | No | Medio | Usa textos RT centralizados, pero no cola. |
| Aviso a Direccion por nuevo RT pendiente | `UsuarioController` | Correo institucional de Direccion/Jefatura | SMTP directo | No | Medio | No usa cola ni `EventKey`. |

## Duplicidades confirmadas

### 1. Cambio de estado AOCR + correo especifico del workflow

Cuando `SolicitudEstadoTransitionBL` cambia un estado AOCR:

- genera campana interna y correo generico idempotente por `AOCR_CAMBIO_ESTADO`;
- luego dispara `SolicitudAocrCorreoService.NotificarEvento(...)` para el evento funcional equivalente.

Esto afecta al menos a:

- `OBSERVADA`
- `SUBSANADA`
- `ACEPTACION_DOCUMENTAL`
- `PENDIENTE_ASIGNACION_INSPECTOR`
- `PAGO_APROBADO`
- `AOCR_LEGALIZADO`
- `AOCR_EMITIDO_RECIBIDO`

Resultado: aunque el correo generico tenga `EventKey`, el usuario puede recibir otro correo adicional del workflow por la misma transicion.

### 2. Subsanacion documental al inspector

Hay dos rutas funcionales distintas para el mismo hecho de negocio:

- `DOCUMENTACION_SUBSANADA_RT` desde `SolicitudAOCRController`;
- `SUBSANADA` desde `SolicitudEstadoTransitionBL` -> `SolicitudAocrCorreoService`.

Ambas apuntan a inspector/coordinacion. Es el caso de duplicidad mas claro dentro de AOCR.

## Hardcodes y fragmentacion de textos

### Textos centralizados parcialmente

- RT: `RtCorreoTextoHelper` consume `Web.config` para asuntos/textos de designacion, declaracion, aceptacion y devolucion.
- Layout visual: `EmailTemplateRenderer` envuelve cuerpos de correo en formato comun.

### Textos aun hardcodeados

- `SolicitudAOCRController.NotificarSolicitanteSolicitudCreada`
- `AocrPostPagoWorkflowService`
- `SolicitudAocrCorreoService`
- `InspeccionCorreoService`
- `OrdenRecaudacionCorreoService`
- `NotificacionCorreoHelper`
- varios bloques de `UsuarioController`, `UsuarioBL`, `AdminUsuariosBL`, `AdminUsuariosController`

### Identidad/remitente fragmentados

Conviven varias claves de configuracion:

- `FromEmail`
- `FromName`
- `EmailFrom`
- `MailFrom`
- `EmailFromName`
- `Email:FromAddress`
- `system.net/mailSettings smtp from`

Hoy la practica dominante ya apunta a `aocr@aviacioncivil.gob.ec`, pero la configuracion sigue duplicada y con valores historicos distintos.

## Atomicidad negocio-correo

Estado actual:

- En la mayoria de casos, el cambio funcional se persiste primero y el correo se intenta despues.
- Eso evita enviar correo si falla la operacion principal antes del commit.
- Pero no hay un patron outbox transaccional real que comprometa negocio y mensaje en la misma unidad atomica.
- `EmailQueueService` usa su propia transaccion al encolar; no participa en la transaccion del caso de uso que ya guardo la solicitud/inspeccion.

Conclusión:

- La notificacion suele ejecutarse solo despues del exito funcional visible.
- No existe garantia transaccional fuerte de tipo post-commit outbox.

## Priorizacion tecnica sugerida

1. Eliminar duplicidad semantica entre `AOCR_CAMBIO_ESTADO` y los correos especificos del workflow AOCR.
2. Asignar `EventKey` obligatorio en `SolicitudAocrCorreoService` e `InspeccionCorreoService`.
3. Unificar la subsanacion documental para que exista una sola notificacion al inspector.
4. Migrar los envios SMTP directos del flujo AOCR a cola persistente.
5. Corregir la persistencia real de adjuntos en los eventos que hoy dependen de bytes en memoria.
6. Centralizar asuntos/cuerpos institucionales en un catalogo de plantillas por evento.

## Archivos clave del diagnostico

- `CapaNegocio/Services/SolicitudAocrCorreoService.cs`
- `CapaNegocio/Services/InspeccionCorreoService.cs`
- `CapaNegocio/SolicitudEstadoTransitionBL.cs`
- `CapaNegocio/NotificacionBL.cs`
- `CapaNegocio/Services/AocrPostPagoWorkflowService.cs`
- `CapaNegocio/Services/OrdenRecaudacionCorreoService.cs`
- `CapaDatos/Services/EmailQueueService.cs`
- `CapaDatos/Services/EnviarCorreo.cs`
- `CapaPresentacion/Controllers/SolicitudAOCRController.cs`
- `CapaPresentacion/Controllers/InspeccionController.cs`
- `CapaPresentacion/Controllers/OrdenRecaudacionController.cs`
- `CapaPresentacion/Controllers/UsuarioController.cs`
- `CapaNegocio/Helpers/RtCorreoTextoHelper.cs`
- `CapaPresentacion/Web.config`