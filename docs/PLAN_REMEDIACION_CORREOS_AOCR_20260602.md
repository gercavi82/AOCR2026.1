# PLAN_REMEDIACION_CORREOS_AOCR_20260602

## 1. Resumen de hallazgos

### Resumen ejecutivo

1. Cantidad total de eventos de correo detectados: 35 eventos funcionales inventariados en la matriz.
2. Correos con duplicidad confirmada: 7 eventos/casos confirmados historicamente.
3. Correos con riesgo alto de duplicado: 14 eventos marcados con riesgo alto.
4. Correos enviados desde controlador: 16 eventos se originan en controladores o en controladores que delegan a un servicio de correo.
5. Correos enviados desde servicios: 19 eventos se originan en BL/servicios/workflows.
6. Correos con texto hardcodeado: 35 de 35 eventos contienen al menos un fragmento hardcodeado; 4 rutas RT solo tienen centralización parcial del mensaje principal.
7. Correos enviados antes de confirmar `SaveChanges` o transacción: 0 casos confirmados; el patrón dominante es persistir primero y notificar después. El riesgo real es falta de outbox transaccional, no envío previo al commit.
8. Correos sin control de idempotencia: 22 eventos sin control, 3 con control parcial y 10 con control claro.
9. Correos con adjuntos sensibles: 6 eventos de la matriz manejan adjuntos sensibles o de alto valor documental.
10. Correos que no deben tocarse en el primer parche: financieros con adjunto, resultados finales de informe técnico, informe técnico firmado final, legalización, emisión/recepción AOCR y flujos RT con documentos adjuntos.

### Conteos operativos

| Métrica | Valor | Criterio |
| --- | --- | --- |
| Eventos totales | 35 | Filas funcionales de la matriz consolidada |
| Duplicidades confirmadas | 7 | 6 por solapamiento `AOCR_CAMBIO_ESTADO` + 1 por subsanación al inspector |
| Riesgo alto | 14 | Filas marcadas como `Alto` |
| Sin idempotencia | 22 | Filas con `Idempotencia actual = No` |
| Idempotencia parcial | 3 | Filas con `Parcial` |
| Con idempotencia clara | 10 | Filas con `Si` |
| Origen en controlador | 16 | Origen real inicia en controller |
| Origen en servicio/BL | 19 | Origen real inicia en service/BL/workflow |
| Adjuntos sensibles | 6 | Eventos con PDF, comprobante, factura o documento formal |

### Estado actual de linea base validada 2026-06-02

| Frente | Estado actual | Evidencia base |
| --- | --- | --- |
| `OBSERVADA` | Parche aplicado y validado runtime | Build `CapaPresentacion` OK; cola reciente con `SOLICITUD_OBSERVADA` y sin nuevo `AOCR_CAMBIO_ESTADO` pareado |
| `ACEPTACION_DOCUMENTAL` | Parche aplicado y validado runtime | Build `CapaPresentacion` OK; cola reciente con `SOLICITUD_ACEPTACION_DOCUMENTAL` y sin nuevo `AOCR_CAMBIO_ESTADO` pareado |
| `PENDIENTE_ASIGNACION_INSPECTOR` | Reanalizado y fuera de duplicidades confirmadas automaticas | El correo especifico notifica a coordinacion; el generico sigue informando al solicitante/RT |
| `PAGO_APROBADO` | Riesgo abierto | La supresion ya esta activa en codigo, pero no existe aun validacion runtime cerrada ni candidatos actuales en el scan de BD |
| `SUBSANADA` | Siguiente caso abierto | Diagnostico separado: clasificacion `D`, con solapamiento real en inspector y sensibilidad por `SubsanarPost` |
| Historial / trigger SQL | Frente separado | No mezclar con el parche de correos; requiere remediacion propia |

### Duplicidades confirmadas

| Caso | Eventos afectados | Origen duplicado | Impacto |
| --- | --- | --- | --- |
| Solapamiento `AOCR_CAMBIO_ESTADO` + correo específico del workflow | `OBSERVADA`, `SUBSANADA`, `ACEPTACION_DOCUMENTAL`, `PAGO_APROBADO`, `AOCR_LEGALIZADO`, `AOCR_EMITIDO_RECIBIDO` | `SolicitudEstadoTransitionBL` llama a `NotificacionBL.NotificarCambioEstado` y luego a `SolicitudAocrCorreoService.NotificarEvento` | El mismo cambio de estado genera un correo genérico y otro funcional |
| Subsanación documental al inspector | `DOCUMENTACION_SUBSANADA_RT` + `SUBSANADA` | `SolicitudAOCRController.SubsanarPost` y `SolicitudEstadoTransitionBL` | Inspector/coordinación pueden recibir dos mensajes por la misma subsanación |

### Riesgos altos de duplicado

| Evento | Archivo principal | Método principal | Motivo |
| --- | --- | --- | --- |
| Solicitud AOCR registrada | `CapaPresentacion/Controllers/SolicitudAOCRController.cs` | `NotificarSolicitanteSolicitudCreada` | SMTP directo sin cola ni `EventKey` |
| Solicitud completada | `CapaPresentacion/Controllers/SolicitudAOCRController.cs` | bloque de envío tras guardado completo | Cola sin `EventKey` |
| Solicitud observada | `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `DispatchCorreoEventoPorEstado` | Solapa con `AOCR_CAMBIO_ESTADO` |
| Solicitud subsanada | `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `DispatchCorreoEventoPorEstado` | Solapa con subsanacion documental del controlador y ademas convive con otra ruta a `Subsanada` sin correo manual |
| Aceptación documental | `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `DispatchCorreoEventoPorEstado` | Solapa con `AOCR_CAMBIO_ESTADO` |
| Inspector asignado | `CapaPresentacion/Controllers/TecnicoController.cs` | bloque de asignación | Reenvío/reasignación sin `EventKey` |
| NC generadas | `CapaNegocio/Services/InspeccionWorkflowService.cs` | `EmitirNotificacionEvento` | Cola sin `EventKey` |
| Documentos subsanados en inspección | `CapaNegocio/Services/InspeccionWorkflowService.cs` | `EmitirNotificacionEvento` | Cola sin `EventKey` |
| Devolución de inspección | `CapaNegocio/Services/InspeccionWorkflowService.cs` | `EmitirNotificacionEvento` | Cola sin `EventKey` |
| Aprobación de inspección | `CapaNegocio/Services/InspeccionWorkflowService.cs` | `EmitirNotificacionEvento` | Cola sin `EventKey` |
| Revalidación OK | `CapaNegocio/Services/InspeccionWorkflowService.cs` | `EmitirNotificacionEvento` | Cola sin `EventKey` |
| Revalidación rechazada | `CapaNegocio/Services/InspeccionWorkflowService.cs` | `EmitirNotificacionEvento` | Cola sin `EventKey` |
| AOCR legalizado | `CapaNegocio/Services/AocrFinalWorkflowService.cs` | `NotificarLegalizacion` | Cola sin `EventKey` |
| AOCR emitido/recibido | `CapaNegocio/Services/AocrFinalWorkflowService.cs` | `NotificarEmision` | Cola sin `EventKey` |

### Correos con texto hardcodeado

Rutas y archivos con hardcode total o parcial:

- `CapaPresentacion/Controllers/SolicitudAOCRController.cs`
- `CapaNegocio/Services/AocrPostPagoWorkflowService.cs`
- `CapaNegocio/Services/SolicitudAocrCorreoService.cs`
- `CapaNegocio/Services/InspeccionCorreoService.cs`
- `CapaNegocio/Services/OrdenRecaudacionCorreoService.cs`
- `CapaPresentacion/Helpers/NotificacionCorreoHelper.cs`
- `CapaPresentacion/Controllers/UsuarioController.cs`
- `CapaNegocio/UsuarioBL.cs`
- `CapaNegocio/AdminUsuariosBL.cs`
- `CapaPresentacion/Controllers/AdminUsuariosController.cs`
- `CapaNegocio/NotificacionBL.cs`

Observación: `RtCorreoTextoHelper` y `Web.config` ya externalizan parte de los asuntos/textos RT, pero el wrapping visual, títulos, cierres, detalles y mensajes complementarios siguen en código.

### Correos con adjuntos sensibles

| Evento | Tipo de adjunto | Riesgo |
| --- | --- | --- |
| `ORDEN_CREADA` | PDF de orden de recaudación | Documento financiero formal |
| `ORDEN_RECAUDACION_GENERADA_FINANCIERO` | PDF de orden | Documento financiero institucional |
| `PAGO_REGISTRADO` | Comprobante de pago | Evidencia financiera |
| `FACTURA_GENERADA` | Factura | Documento tributario/financiero |
| `INFORME_TECNICO_FIRMADO` | Informe técnico firmado PDF | Documento técnico institucional |
| Declaración de responsabilidad aceptada | PDF de declaración | Documento formal RT |

### Correos que no deben tocarse en el primer parche

- `Resultado final del informe técnico al RT`
- `Informe técnico firmado final con PDF`
- `ORDEN_CREADA`
- `ORDEN_RECAUDACION_GENERADA_FINANCIERO`
- `PAGO_REGISTRADO`
- `FACTURA_GENERADA`
- `AOCR_LEGALIZADO`
- `AOCR_EMITIDO_RECIBIDO`
- `Declaración de responsabilidad aceptada`
- `Aceptación RT con clave temporal`

Razon: todos involucran adjuntos, documentos formales, hitos finales del flujo o contratos sensibles que no deben ser la superficie del primer parche.

## 2. Duplicidades confirmadas

### Grupo A: correo genérico de cambio de estado + correo específico de workflow AOCR

| Prioridad | Evento | Archivo | Método | Duplicado actual | Riesgo |
| --- | --- | --- | --- | --- | --- |
| Alta | `OBSERVADA` | `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `NotificarCambioEstadoAocr` / `DispatchCorreoEventoPorEstado` | `AOCR_CAMBIO_ESTADO` + `OBSERVADA` | Bajo-medio |
| Alta | `ACEPTACION_DOCUMENTAL` | `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `NotificarCambioEstadoAocr` / `DispatchCorreoEventoPorEstado` | `AOCR_CAMBIO_ESTADO` + `ACEPTACION_DOCUMENTAL` | Bajo-medio |
| Media | `PAGO_APROBADO` | `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `NotificarCambioEstadoAocr` / `DispatchCorreoEventoPorEstado` | `AOCR_CAMBIO_ESTADO` + `PAGO_APROBADO` | Medio |
| Baja | `AOCR_LEGALIZADO` | `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `NotificarCambioEstadoAocr` / `DispatchCorreoEventoPorEstado` | `AOCR_CAMBIO_ESTADO` + `AOCR_LEGALIZADO` | Medio-alto |
| Baja | `AOCR_EMITIDO_RECIBIDO` | `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `NotificarCambioEstadoAocr` / `DispatchCorreoEventoPorEstado` | `AOCR_CAMBIO_ESTADO` + `AOCR_EMITIDO_RECIBIDO` | Medio-alto |

Nota de estado actual: `OBSERVADA` y `ACEPTACION_DOCUMENTAL` permanecen en esta tabla como duplicidades confirmadas historicas, pero ya quedaron remediadas y validadas en runtime el 2026-06-02. `PAGO_APROBADO` permanece aqui solo como riesgo abierto pendiente de cierre runtime; la supresion ya esta activa en `DebeOmitirCorreoGenericoCambioEstado`, pero todavia no existe caso validado extremo a extremo.

### Grupo B: subsanación documental al inspector

| Prioridad | Evento | Archivo | Método | Duplicado actual | Riesgo |
| --- | --- | --- | --- | --- | --- |
| Alta | `DOCUMENTACION_SUBSANADA_RT` + `SUBSANADA` | `CapaPresentacion/Controllers/SolicitudAOCRController.cs` y `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `SubsanarPost` y `DispatchCorreoEventoPorEstado` | Notificación manual + notificación de transición | Medio-alto |

Nota de estado actual: `SUBSANADA` sigue abierto, pero ya no debe tratarse como un duplicado simple. La evidencia runtime del 2026-06-02 en solicitud 7 confirmo simultaneamente `AOCR_CAMBIO_ESTADO`, `SOLICITUD_SUBSANADA` y `DOCUMENTACION_SUBSANADA_RT`, con solapamiento real en el inspector. A la vez, existe otra ruta a `Subsanada` en `MarcarSubsanadaDespuesDeGuardar(...)` sin correo manual al inspector, por lo que un parche futuro no puede apagar `SUBSANADA` de forma global.

## 3. Riesgos altos de duplicado

### Clasificación por causa raíz

| Causa raíz | Eventos | Archivo pivote | Observación |
| --- | --- | --- | --- |
| Sin `EventKey` en servicios AOCR | `SOLICITUD_COMPLETADA`, `INSPECTOR_ASIGNADO`, `AOCR_LEGALIZADO`, `AOCR_EMITIDO_RECIBIDO` | `CapaNegocio/Services/SolicitudAocrCorreoService.cs` | Cualquier reintento reencola |
| Sin `EventKey` en servicios de inspección | `NC_GENERADAS`, `DOCUMENTOS_SUBSANADOS`, `DEVOLUCION_INSPECCION`, `APROBACION_INSPECCION`, `REVALIDACION_OK`, `REVALIDACION_RECHAZADA`, `PENDIENTE_FIRMA_DIRDAC` | `CapaNegocio/Services/InspeccionCorreoService.cs` | Reprocesos o retries repiten envíos |
| Controlador con SMTP directo | `Solicitud AOCR registrada`, `Registro RT pendiente aprobación`, `Declaración aceptada`, `Aviso a Dirección`, `Recuperación contraseña` | `SolicitudAOCRController.cs`, `UsuarioController.cs`, `UsuarioBL.cs` | No hay cola ni dedupe |
| Solapamiento semántico | `OBSERVADA`, `ACEPTACION_DOCUMENTAL`, `SUBSANADA`, `PAGO_APROBADO`, `AOCR_LEGALIZADO`, `AOCR_EMITIDO_RECIBIDO` | `SolicitudEstadoTransitionBL.cs` | Correo genérico + específico |

## 4. Plan por fases

### FASE 1: Parche mínimo anti-duplicado

Objetivo: eliminar duplicados confirmados sin cambiar textos, vistas, rutas, estados, contratos ni descargas.

| Archivo exacto | Método exacto | Evento afectado | Riesgo actual | Corrección mínima | Criterio de idempotencia | Prueba funcional |
| --- | --- | --- | --- | --- | --- | --- |
| `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `NotificarCambioEstadoAocr` | `OBSERVADA` | Doble correo por `AOCR_CAMBIO_ESTADO` + `OBSERVADA` | Ya aplicado y validado: mantener campana interna, omitir correo generico cuando ya existe correo especifico de workflow para `OBSERVADA` | `SolicitudId + EstadoDestino + Destinatario`, usando el workflow especifico como canal unico de email | Cambiar solicitud a Observada y verificar 1 campana interna + 1 correo especifico en cola + 0 `AOCR_CAMBIO_ESTADO` para el mismo usuario/solicitud/estado |
| `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `NotificarCambioEstadoAocr` | `ACEPTACION_DOCUMENTAL` | Doble correo por transicion | Ya aplicado y validado: misma estrategia de `OBSERVADA`, con supresion del generico y preservacion de campana interna | `SolicitudId + EstadoDestino + Destinatario` | Aprobar revision documental y verificar un solo correo externo |
| `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `DispatchCorreoEventoPorEstado` | `SUBSANADA` | Se solapa con `DOCUMENTACION_SUBSANADA_RT` | No incluir en un parche global. Requiere diagnostico separado por `SubsanarPost` y respetar la ruta alterna `MarcarSubsanadaDespuesDeGuardar(...)` | N/A en esta linea base | Dejar para remediacion especifica posterior |

Recomendacion de alcance de Fase 1 al cierre del 2026-06-02: el slice inicial ya se ejecuto con `OBSERVADA` y luego se extendio a `ACEPTACION_DOCUMENTAL`, ambos con validacion de build y runtime. El siguiente frente de correo ya no es repetir Fase 1, sino diagnosticar `SUBSANADA`/`SubsanarPost` y mantener `PAGO_APROBADO` como riesgo abierto hasta tener evidencia runtime.

### FASE 2: Centralización de plantillas

Objetivo: mover textos hardcodeados a un servicio de plantillas sin alterar contenido funcional.

| Archivo actual | Textos actuales | Plantilla propuesta | Variables necesarias | Servicio destino sugerido | Validaciones |
| --- | --- | --- | --- | --- | --- |
| `CapaNegocio/Services/SolicitudAocrCorreoService.cs` | Asuntos/títulos/mensajes del workflow AOCR | Plantilla por evento AOCR | `SolicitudId`, `NumeroSolicitud`, `Operador`, `Estado`, `Observacion`, `Destinatario` | `CapaNegocio/Services/AocrEmailTemplateService.cs` o similar | Comparación literal de asunto, cuerpo renderizado y destinatarios antes/después |
| `CapaNegocio/Services/InspeccionCorreoService.cs` | Mensajes de inspección, DIRDAC, resultado final | Plantilla por evento inspección | `CodigoInspeccion`, `NumeroSolicitud`, `Operadora`, `Resultado`, `Hallazgos`, `Observacion`, `Destinatario` | `CapaNegocio/Services/InspeccionEmailTemplateService.cs` | Snapshot de HTML renderizado y validación de campos obligatorios |
| `CapaNegocio/Services/OrdenRecaudacionCorreoService.cs` | Mensajes OR/pago/factura | Plantilla financiera por evento | `OrdenId`, `NumeroOrden`, `SolicitudId`, `Monto`, `RUC`, `Operadora`, `Destinatario` | `CapaNegocio/Services/OrdenEmailTemplateService.cs` | Validar que no cambie contenido funcional ni naming de adjuntos |
| `CapaPresentacion/Controllers/SolicitudAOCRController.cs` | Correo de solicitud registrada | Plantilla `SOLICITUD_REGISTRADA` | `NumeroSolicitud`, `Operador`, `CodigoOaci`, `FechaRegistro`, `UrlDetalle` | Reutilizar servicio de plantillas AOCR | Igualar texto existente al 100% en primer paso |
| `CapaPresentacion/Controllers/UsuarioController.cs` y `CapaNegocio/UsuarioBL.cs` | Textos RT y recuperación | Plantillas RT / credenciales | `Nombre`, `Correo`, `Identificacion`, `Usuario`, `ClaveTemporal`, `Compania` | Extender `RtCorreoTextoHelper` o crear `RtEmailTemplateService` | No cambiar semántica ni requisitos regulatorios |

Validaciones mínimas de Fase 2:

- asunto idéntico al actual en la primera migración;
- mismo orden de variables visibles al usuario;
- mismo layout final o equivalente visual aprobado;
- fallback seguro cuando falte variable;
- cobertura de pruebas por evento renderizado.

### FASE 3: Salida de correos desde controlador

Objetivo: eliminar envío directo desde `SolicitudAOCRController.cs` cuando ya exista un servicio natural.

| Método del controlador | Servicio destino | Resultado esperado | Riesgo de regresión | Prueba necesaria |
| --- | --- | --- | --- | --- |
| `SolicitudAOCRController.NotificarSolicitanteSolicitudCreada` | `SolicitudAocrCorreoService` con evento `SOLICITUD_REGISTRADA` nuevo o equivalente | Correo encolado, trazable y deduplicable | Bajo | Crear solicitud nueva y verificar cola + contenido igual |
| Bloque de `SolicitudAOCRController` que encola documentación lista | Mantener en controlador en fase 3 inicial | No mover en primera ola; ya usa cola y `EventKey` | Bajo | Ninguna modificación temprana |
| `SolicitudAOCRController` aceptación documental firmada | `SolicitudAocrCorreoService` se mantiene, solo añadir `EventKey` después | Sin cambio funcional | Bajo | Firmar aceptación documental y validar una sola cola |

Nota: en esta fase también deben incluirse controladores `UsuarioController` y `AdminUsuariosController`, pero no dentro del primer parche AOCR.

### FASE 4: Idempotencia formal por evento

Objetivo: asegurar un solo envío por `SolicitudId + Evento + Destinatario + EstadoDestino`.

| Pregunta | Respuesta técnica actual | Propuesta |
| --- | --- | --- |
| ¿Existe `email_queue`? | Sí, existe y ya tiene `event_key`, `tipo_notificacion`, `solicitud_id`, `correlation_id` | Reusar estructura existente |
| ¿Puede usarse la estructura actual? | Sí, para la mayoría de eventos AOCR e inspección | Estandarizar `EventKey` en todos los servicios de correo de workflow |
| ¿Falta llave lógica? | Falta convención uniforme por evento | Definir formato `AOCR:{EVENTO}:{SOLICITUD}:{DESTINATARIO}:{ESTADO?}` |
| ¿Hace falta tabla nueva? | No para fase inicial | Solo considerar tabla nueva si se requiere histórico funcional separado de `email_queue` |
| ¿Riesgos? | Supresión accidental de eventos distintos pero semánticamente cercanos | Diseñar `EventKey` con suficiente granularidad y pruebas de reenvío manual |

Eventos donde la estructura actual ya permite solución sin nueva tabla:

- todos los eventos de `SolicitudAocrCorreoService`;
- todos los eventos de `InspeccionCorreoService`;
- todos los eventos de `OrdenRecaudacionCorreoService`;
- eventos administrativos que ya pasan por `EnviarEncolado`.

### FASE 5: Mensajes institucionales corregidos

Objetivo: corregir tono, consistencia institucional y denominaciones oficiales una vez estabilizados duplicidad e idempotencia.

| Alcance | Regla |
| --- | --- |
| Asuntos y saludos | Unificar remitente, tono y firma DGAC/AOCR |
| Referencias a áreas | Homogeneizar `DIRDAC / Dirección - Jefatura`, `Coordinación`, `Financiero AOCR` |
| Textos legales/finales | Revisar con negocio antes de desplegar |
| Timing | No ejecutar hasta cerrar Fase 1 y Fase 4 |

## 5. Orden de intervención

| Prioridad | Evento | Justificación | Archivo | Método | Cambio recomendado | Riesgo de romper flujo | Prueba mínima |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Alta | `OBSERVADA` | Duplicidad confirmada y bajo riesgo; no toca adjuntos ni finales | `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `NotificarCambioEstadoAocr` | Ya aplicado y validado en runtime; mantener como patron cerrado del slice inicial | Bajo | 1 transición a Observada = 1 campana + 1 correo específico |
| Alta | `ACEPTACION_DOCUMENTAL` | Duplicidad confirmada y comportamiento estable | `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `NotificarCambioEstadoAocr` | Ya aplicado y validado en runtime; mismo patron de `OBSERVADA` | Bajo | 1 aceptación documental = 1 correo externo |
| Alta | `PAGO_APROBADO` | La supresión ya está activa en código, pero falta validación operacional real | `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `NotificarCambioEstadoAocr` | No cerrar ni extender cambios hasta tener evidencia runtime de un caso real | Medio | Encontrar una transición real post-pago y confirmar ausencia de `AOCR_CAMBIO_ESTADO` pareado |
| Media | `PENDIENTE_ASIGNACION_INSPECTOR` | Requiere decisión funcional separada; no replica el patrón de destinatarios de `OBSERVADA` | `CapaNegocio/SolicitudEstadoTransitionBL.cs` | `NotificarCambioEstadoAocr` | No aplicar supresión ciega de `AOCR_CAMBIO_ESTADO`: el correo específico notifica a coordinación y el genérico informa al solicitante/RT | Medio | Validar con negocio si ambos canales deben coexistir o si el genérico debe reemplazarse por una campana interna sin correo |
| Alta | `DOCUMENTACION_SUBSANADA_RT` + `SUBSANADA` | Duplicidad confirmada con impacto real y sensibilidad alta | `SolicitudAOCRController.cs` y `SolicitudEstadoTransitionBL.cs` | `SubsanarPost` / `DispatchCorreoEventoPorEstado` | Clasificacion `D`: hay duplicidad real en inspector, pero no debe apagarse `SUBSANADA` globalmente por la ruta `MarcarSubsanadaDespuesDeGuardar(...)` | Medio-alto | Subsanación con separación explícita entre inspector, coordinación y operador/RT |
| Media | `INSPECTOR_ASIGNADO` | Sin `EventKey`, fácil de deduplicar | `CapaNegocio/Services/SolicitudAocrCorreoService.cs` | `NotificarEvento` | Agregar `EventKey` por evento+solicitud+destinatario | Bajo | Reasignación idéntica no duplica |
| Media | `SOLICITUD_COMPLETADA` | Doble submit puede reenviar | `CapaNegocio/Services/SolicitudAocrCorreoService.cs` | `NotificarEvento` | Agregar `EventKey` | Bajo | Retry POST no genera segundo correo |
| Media | `PENDIENTE_FIRMA_DIRDAC` | Sin `EventKey`, pero flujo sensible | `CapaNegocio/Services/InspeccionCorreoService.cs` | `NotificarEvento` | Agregar `EventKey` y conservar control funcional existente | Medio | Reenvío explícito controlado |
| Media | `NC_GENERADAS`, `DOCUMENTOS_SUBSANADOS`, `DEVOLUCION_INSPECCION`, `APROBACION_INSPECCION`, `REVALIDACION_OK`, `REVALIDACION_RECHAZADA` | Alto riesgo por retries y doble clic | `CapaNegocio/Services/InspeccionCorreoService.cs` | `NotificarEvento` | Estandarizar `EventKey` | Medio | Retry mismo evento no duplica |
| Media | Correo de solicitud registrada | SMTP directo en controlador | `CapaPresentacion/Controllers/SolicitudAOCRController.cs` | `NotificarSolicitanteSolicitudCreada` | Mover a servicio AOCR | Bajo-medio | Alta de solicitud produce cola, no SMTP directo |
| Media | `AOCR_LEGALIZADO`, `AOCR_EMITIDO_RECIBIDO` | Finales del flujo con alto riesgo por falta de `EventKey` | `CapaNegocio/Services/SolicitudAocrCorreoService.cs` | `NotificarEvento` | Agregar `EventKey` después de estabilizar AOCR medio | Medio-alto | Reintento no duplica final |
| Baja | Financieros con adjunto | Riesgo documental mayor que beneficio temprano | `OrdenRecaudacionCorreoService.cs` / `EmailQueueService.cs` | varios | Corregir persistencia de adjuntos y luego dedupe fina | Alto | Adjuntos intactos |
| Baja | `INFORME_TECNICO_FIRMADO` | SMTP directo con PDF | `InspeccionCorreoService.cs` | `NotificarInformeTecnicoFirmadoFinal` | Migrar a cola con adjunto persistido | Alto | PDF llega intacto |
| Baja | Textos institucionales | No corrige duplicidad inmediata | múltiples | múltiples | Centralizar plantillas | Bajo | Comparación de contenido |

Nota de validación 2026-06-02: `PENDIENTE_ASIGNACION_INSPECTOR` fue reanalizado después del parche `PAGO_APROBADO`. El correo específico `SOLICITUD_PENDIENTE_ASIGNACION_INSPECTOR` hoy se dirige a coordinación de inspección, mientras `AOCR_CAMBIO_ESTADO` sale al solicitante/RT por `NotificacionBL.NotificarCambioEstado`. Por eso no debe reutilizar el mismo parche de supresión aplicado a `OBSERVADA`, `ACEPTACION_DOCUMENTAL` o `PAGO_APROBADO` sin una decisión funcional explícita.

## 6. Archivos impactados

### Primera ola

- `CapaNegocio/SolicitudEstadoTransitionBL.cs`
- `CapaNegocio/NotificacionBL.cs`
- `CapaNegocio/Services/SolicitudAocrCorreoService.cs`
- `CapaNegocio/Services/InspeccionCorreoService.cs`

### Segunda ola

- `CapaPresentacion/Controllers/SolicitudAOCRController.cs`
- `CapaPresentacion/Controllers/TecnicoController.cs`
- `CapaPresentacion/Controllers/InspeccionController.cs`
- `CapaNegocio/Services/AocrFinalWorkflowService.cs`
- `CapaNegocio/Services/AocrPostPagoWorkflowService.cs`

### Tercera ola

- `CapaNegocio/Services/OrdenRecaudacionCorreoService.cs`
- `CapaDatos/Services/EmailQueueService.cs`
- `CapaDatos/Services/EnviarCorreo.cs`
- `CapaPresentacion/Controllers/UsuarioController.cs`
- `CapaNegocio/UsuarioBL.cs`
- `CapaNegocio/AdminUsuariosBL.cs`
- `CapaPresentacion/Controllers/AdminUsuariosController.cs`
- `CapaNegocio/Helpers/RtCorreoTextoHelper.cs`
- `CapaPresentacion/Web.config`

## 7. Guardrails

### No tocar

- `DescargarAceptacionDocumental`
- Descargas RT
- `Finalizado`
- `Subsanada` como contrato/estado funcional
- `SubsanarPost`, salvo lectura de estado si fuera indispensable y aprobada
- Carga de archivos
- `SecurityFilters`
- `AocrAuthorizationContext`
- Roles
- Autorizaciones
- Vistas
- Rutas
- Actions
- Nombres de estados
- Tablas o columnas sin aprobación
- Contratos congelados

### No cambiar en el parche mínimo

- flujo funcional;
- permisos;
- bandejas;
- redirecciones;
- descargas;
- estados existentes;
- textos institucionales.

## 8. Primer parche mínimo recomendado

### Alcance recomendado

Parche único, quirúrgico y de no regresión para eliminar el duplicado confirmado del evento `OBSERVADA`.

1. Archivo exacto: `CapaNegocio/SolicitudEstadoTransitionBL.cs`
2. Método exacto: `NotificarCambioEstadoAocr` con apoyo del flujo ya existente en `DispatchCorreoEventoPorEstado`
3. Evento exacto: `OBSERVADA`
4. Duplicado que elimina: correo genérico `AOCR_CAMBIO_ESTADO` más correo específico `OBSERVADA` para la misma transición
5. Condición anti-duplicado: cuando el estado destino sea `Observada` y el flujo ya vaya a despachar el correo específico `OBSERVADA`, preservar la campana interna y omitir solo el correo genérico de cambio de estado
6. Por qué no rompe comportamiento:
   - la transición ya se ejecutó con éxito antes de notificar;
   - el usuario sigue recibiendo campana interna;
   - el usuario sigue recibiendo correo funcional del workflow;
   - no cambian rutas, vistas, estados, roles, permisos ni acciones;
   - no toca `SubsanarPost`, descargas ni adjuntos.
7. Tests que se deben ejecutar:
   - transición documental a `Observada`;
   - validación de 1 registro en `email_queue` para correo específico del evento;
   - validación de 0 registro `AOCR_CAMBIO_ESTADO` para esa misma transición;
   - validación de campana interna visible en UI o persistida en notificaciones;
   - retry POST / doble clic del caso observado;
   - smoke test de otras transiciones no tocadas.
8. Diff esperado a alto nivel:
   - separar conceptualmente campana interna de correo genérico en `SolicitudEstadoTransitionBL`;
   - para `Observada`, mantener `NotificacionBL.EnviarNotificacion` interna o equivalente y suprimir la llamada que encola `AOCR_CAMBIO_ESTADO`;
   - conservar intacto `DispatchCorreoEventoPorEstado("OBSERVADA")`.

### Razón para elegir este primer parche

- resolvio una duplicidad confirmada;
- afecto un solo archivo pivote;
- no requirio esquema nuevo;
- se ejecuto post-persistencia, no antes del exito funcional;
- no toco zonas congeladas;
- dejo un patron reutilizado ya en `ACEPTACION_DOCUMENTAL`.

## 9. Riesgo abierto: PAGO_APROBADO activo en codigo sin validacion runtime

Estado actual del frente:

- `PAGO_APROBADO` ya esta activo en codigo dentro de `DebeOmitirCorreoGenericoCambioEstado(...)`.
- La intencion del parche ya quedo alineada con `OBSERVADA` y `ACEPTACION_DOCUMENTAL`: mantener campana interna y evitar el pareo nuevo de `AOCR_CAMBIO_ESTADO` con el correo especifico.
- Sin embargo, el scan runtime ejecutado sobre `aocr_tbsolicitud` para estados candidatos (`PAGO_PENDIENTE`, `PAGO_VALIDADO`, `SOLICITUD_CREADA`, `DOCUMENTACION_PENDIENTE`) no devolvio casos activos al 2026-06-02.
- Por eso este evento no debe cerrarse como validado todavia. Sigue siendo un riesgo abierto de remediacion ya codificada pero sin verificacion operacional extremo a extremo.

Regla documental: mantenerlo fuera del grupo de parches cerrados hasta disponer de una corrida real en BD/UI/cola.

### Protocolo de validacion runtime: PAGO_APROBADO

Estado actual: pendiente hasta tener solicitud candidata real.

#### Condicion para ejecutar la prueba

- Solo ejecutar con una solicitud real ya existente en BD, ligada a una orden real y con comprobante real disponible para Finanzas.
- No crear datos manualmente, no alterar estados por SQL, no forzar pagos, no reencolar correos y no modificar adjuntos.
- Tomar `@FechaInicioPrueba` inmediatamente antes de aprobar el pago, para acotar la ventana de inspeccion en `email_queue`, `aocr_tbnotificacion` y `aocr_audit_trail`.
- Si hay varias candidatas, priorizar una sin inspector asignado para cubrir las dos ramas reales del servicio post-pago: correo al RT y aviso adicional a coordinacion.

#### Trazado tecnico previo obligatorio

- Action/UI real de entrada: `FinancieroController.AprobarPago(...)`, `FinancieroController.AprobarOrden(...)` y `OrdenRecaudacionController.ValidarPago(...)` llaman a `AocrPostPagoWorkflowService.ProcesarPagoAprobado(ordenId, usuario)`.
- Metodo equivalente al cambio de estado: en este slice no se encontro caller runtime de `CambiarEstadoConReglasAocr(...)`; el equivalente real es `AocrPostPagoWorkflowService.ProcesarPagoAprobado(...)`.
- Persistencia esperada: el servicio marca `pago_aprobado = TRUE`, `modulo_solicitud_rt_habilitado = TRUE`, `fecha_aprobacion_pago`, y deja el estado en `PENDIENTE_CARGA_DOCUMENTAL_RT` o `PENDIENTE_REVISION_DOCUMENTAL` segun existan documentos habilitantes.
- Campana interna esperada: `NotificacionBL.EnviarNotificacion(...)` inserta en `aocr_tbnotificacion` con `tipo = 'PAGO_APROBADO'` para el RT y, si no hay inspector asignado, tambien para coordinacion.
- Correo especifico esperado en runtime real: `PAGO_APROBADO_RT_SOLICITUD_AOCR_HABILITADA` al RT y, si aplica, `PAGO_APROBADO_COORDINADOR_ASIGNACION_INSPECTOR` a coordinacion. Ambos se encolan desde `AocrPostPagoWorkflowService.EncolarCorreo(...)` y si usan cola si tienen `event_key`.
- Hipotesis de riesgo que sigue abierta: `SolicitudEstadoTransitionBL.ResolverEventoCorreoPorEstado(...)` aun modela `PAGO_APROBADO` para transiciones `Pago Pendiente`/`Pago Validado` -> `Solicitud Creada`/`Documentacion Pendiente`, y `NotificacionBL.NotificarCambioEstado(...)` es la ruta que produciria `AOCR_CAMBIO_ESTADO`. La validacion runtime debe confirmar si ese generico aparece o no durante la aprobacion financiera real.
- Cobertura funcional del correo especifico: el correo post-pago cubre el evento funcional de habilitar la solicitud AOCR para RT y, cuando falta inspector, el aviso operativo a coordinacion. Si solo aparece este paquete y no aparece `AOCR_CAMBIO_ESTADO`, no hay duplicidad nueva en runtime.

#### Consulta para identificar candidato

```sql
WITH orden_ultima AS (
   SELECT DISTINCT ON (o.codigo_solicitud)
      NULLIF(TRIM(o.codigo_solicitud::text), '')::integer AS solicitud_id,
      o.id AS orden_id,
      o.numero_orden,
      o.estado AS estado_orden,
      o.compania,
      o.fecha_creacion
   FROM aocr_or_orden o
   WHERE NULLIF(TRIM(o.codigo_solicitud::text), '') IS NOT NULL
   ORDER BY o.codigo_solicitud, o.fecha_creacion DESC NULLS LAST, o.id DESC
),
documentos_habilitantes AS (
   SELECT
      d.codigo_solicitud,
      COUNT(*) AS total_documentos
   FROM aocr_tbdocumento d
   WHERE COALESCE(d.tamano_bytes, 0) > 0
     AND NULLIF(TRIM(COALESCE(d.nombre_archivo, '')), '') IS NOT NULL
     AND NULLIF(TRIM(COALESCE(d.ruta_guardada, '')), '') IS NOT NULL
     AND UPPER(TRIM(COALESCE(d.tipo_documento, ''))) NOT IN ('BORRADOR_AOCR', 'AOCR_GENERADO', 'AOCR')
   GROUP BY d.codigo_solicitud
)
SELECT
   s.codigo_solicitud AS solicitud_id,
   COALESCE(NULLIF(TRIM(s.numero_solicitud), ''), s.codigo_solicitud::text) AS codigo_solicitud,
   s.estado AS estado_actual,
   COALESCE(NULLIF(TRIM(s.razon_social), ''), NULLIF(TRIM(s.nombre_operador), ''), NULLIF(TRIM(o.compania), ''), 'N/D') AS operador_compania,
   o.orden_id,
   o.numero_orden,
   o.estado_orden,
   (o.orden_id IS NOT NULL) AS tiene_orden_recaudacion,
   COALESCE(pago.ruta_comprobante, factura.file_path) AS respaldo_pago_registrado,
   COALESCE(pago.estado_pago, 'SIN_PAGO') AS estado_pago,
   COALESCE(dh.total_documentos, 0) AS documentos_habilitantes,
   EXISTS (
      SELECT 1
      FROM aocr_tbinspeccion i
      WHERE i.codigo_solicitud = s.codigo_solicitud
        AND i.codigo_inspector IS NOT NULL
   ) AS tiene_inspector_asignado,
   CASE
      WHEN REPLACE(UPPER(COALESCE(o.estado_orden, '')), ' ', '_') = 'EN_REVISION_FINANCIERA'
       AND COALESCE(NULLIF(TRIM(COALESCE(pago.ruta_comprobante, factura.file_path, '')), ''), '') <> ''
      THEN TRUE
      ELSE FALSE
   END AS flujo_financiero_listo,
   CASE
      WHEN REPLACE(UPPER(COALESCE(o.estado_orden, '')), ' ', '_') = 'EN_REVISION_FINANCIERA'
       AND COALESCE(NULLIF(TRIM(COALESCE(pago.ruta_comprobante, factura.file_path, '')), ''), '') <> ''
       AND REPLACE(UPPER(COALESCE(s.estado, '')), ' ', '_') IN (
          'PAGO_PENDIENTE',
          'PAGO_VALIDADO',
          'SOLICITUD_CREADA',
          'DOCUMENTACION_PENDIENTE'
       )
      THEN TRUE
      ELSE FALSE
   END AS puede_avanzar_naturalmente_a_evento_post_pago
FROM aocr_tbsolicitud s
LEFT JOIN orden_ultima o
      ON o.solicitud_id = s.codigo_solicitud
LEFT JOIN LATERAL (
   SELECT
      p.codigo_pago,
      p.estado AS estado_pago,
      p.numero_comprobante,
      p.ruta_comprobante,
      p.fecha_pago,
      p.fecha_validacion
   FROM aocr_tbpago p
   WHERE p.codigo_solicitud = s.codigo_solicitud
      OR (o.orden_id IS NOT NULL AND p.codigo_solicitud = o.orden_id)
   ORDER BY p.fecha_pago DESC NULLS LAST, p.codigo_pago DESC
   LIMIT 1
) pago ON TRUE
LEFT JOIN LATERAL (
   SELECT
      f.file_path,
      f.creado_en
   FROM aocr_tb_factura_pago f
   WHERE o.orden_id IS NOT NULL
     AND f.orden_id = o.orden_id
   ORDER BY f.creado_en DESC
   LIMIT 1
) factura ON TRUE
LEFT JOIN documentos_habilitantes dh
      ON dh.codigo_solicitud = s.codigo_solicitud
WHERE s.deleted_at IS NULL
  AND (
     REPLACE(UPPER(COALESCE(s.estado, '')), ' ', '_') IN (
        'PAGO_PENDIENTE',
        'PAGO_VALIDADO',
        'SOLICITUD_CREADA',
        'DOCUMENTACION_PENDIENTE'
     )
     OR REPLACE(UPPER(COALESCE(o.estado_orden, '')), ' ', '_') = 'EN_REVISION_FINANCIERA'
  )
ORDER BY
   flujo_financiero_listo DESC,
   puede_avanzar_naturalmente_a_evento_post_pago DESC,
   o.fecha_creacion DESC NULLS LAST,
   s.codigo_solicitud DESC;
```

Nota operativa: la consulta solo identifica candidatas. Antes de ejecutar la aprobacion, abrir la orden en UI y confirmar que el comprobante realmente existe para Finanzas. La condicion definitiva la resuelve `ComprobanteService.ExisteComprobanteValido(ordenId)`, no solo la presencia de una ruta en BD.

#### Pasos funcionales

1. Seleccionar una fila con `flujo_financiero_listo = TRUE` y `puede_avanzar_naturalmente_a_evento_post_pago = TRUE`.
2. Registrar `solicitud_id`, `codigo_solicitud`, `estado_actual`, `operador_compania`, `orden_id`, `numero_orden`, `tiene_inspector_asignado`, `documentos_habilitantes` y `respaldo_pago_registrado`.
3. Fijar `@FechaInicioPrueba` y abrir la orden en el flujo normal de Finanzas, sin tocar SQL ni backoffice tecnico.
4. Ejecutar la aprobacion desde la action real disponible en el ambiente (`AprobarPago`, `AprobarOrden` o `ValidarPago`, segun la pantalla que muestre la orden candidata).
5. Confirmar persistencia inmediata en `aocr_tbsolicitud`:

```sql
SELECT
   codigo_solicitud,
   estado,
   pago_aprobado,
   modulo_solicitud_rt_habilitado,
   fecha_aprobacion_pago,
   pendiente_carga_documental_rt,
   pendiente_asignacion_inspector,
   notificado_rt_modulo_habilitado,
   notificado_coordinador_pago_aprobado,
   updated_at,
   updated_by
FROM aocr_tbsolicitud
WHERE codigo_solicitud = @SolicitudId;
```

6. Confirmar historial/auditoria del post-pago en `aocr_audit_trail`:

```sql
SELECT
   fecha_creacion,
   accion,
   estado_anterior,
   estado_nuevo,
   usuario_nombre,
   metadata
FROM aocr_audit_trail
WHERE tabla = 'aocr_tbsolicitud'
  AND registro_id = @SolicitudId
  AND fecha_creacion >= @FechaInicioPrueba
  AND accion IN (
     'PAGO_APROBADO_FINANCIERO',
     'MODULO_SOLICITUD_RT_HABILITADO',
     'SOLICITUD_PENDIENTE_CARGA_DOCUMENTAL_RT',
     'NOTIFICACION_RT_MODULO_HABILITADO',
     'NOTIFICACION_COORDINADOR_ASIGNACION_PENDIENTE'
  )
ORDER BY fecha_creacion DESC;
```

7. Confirmar campanas internas en `aocr_tbnotificacion`:

```sql
SELECT
   codigonotificacion,
   codigousuario,
   titulo,
   tipo,
   url,
   modulo,
   entidad_id,
   tipo_entidad,
   fechacreacion
FROM aocr_tbnotificacion
WHERE entidad_id = @SolicitudId
  AND fechacreacion >= @FechaInicioPrueba
  AND tipo = 'PAGO_APROBADO'
ORDER BY fechacreacion DESC;
```

8. Confirmar que no se rompio el flujo financiero: la orden debe quedar aprobada/facturada segun la pantalla usada, sin eliminar comprobantes ni factura asociada.
9. Confirmar que no se afectaron documentos ni adjuntos: el conteo y rutas de `aocr_tbdocumento` deben permanecer estables antes y despues de la aprobacion.

#### Consulta email_queue

Consulta base solicitada para el riesgo abierto:

```sql
SELECT
   tipo_notificacion,
   para,
   solicitud_id,
   event_key,
   created_at
FROM email_queue
WHERE solicitud_id = @SolicitudId
  AND created_at >= @FechaInicioPrueba
  AND tipo_notificacion IN ('PAGO_APROBADO', 'AOCR_CAMBIO_ESTADO')
ORDER BY created_at DESC;
```

Consulta operativa ampliada para la implementacion runtime actual:

```sql
SELECT
   tipo_notificacion,
   para,
   solicitud_id,
   orden_id,
   event_key,
   created_at
FROM email_queue
WHERE solicitud_id = @SolicitudId
  AND created_at >= @FechaInicioPrueba
  AND tipo_notificacion IN (
     'PAGO_APROBADO',
     'AOCR_CAMBIO_ESTADO',
     'PAGO_APROBADO_RT_SOLICITUD_AOCR_HABILITADA',
     'PAGO_APROBADO_COORDINADOR_ASIGNACION_INSPECTOR',
     'SOLICITUD_PAGO_APROBADO'
  )
ORDER BY created_at DESC;
```

Lectura esperada del resultado:

- `PAGO_APROBADO_RT_SOLICITUD_AOCR_HABILITADA` al RT confirma el correo especifico real del servicio post-pago.
- `PAGO_APROBADO_COORDINADOR_ASIGNACION_INSPECTOR` solo debe aparecer si no habia inspector asignado.
- `AOCR_CAMBIO_ESTADO` solo debe considerarse duplicidad si cae en la misma ventana, para el mismo `solicitud_id`, y apunta al mismo destinatario funcional que el correo post-pago.
- Si aparece `PAGO_APROBADO` o `SOLICITUD_PAGO_APROBADO`, documentar el caller exacto observado antes de concluir duplicidad, porque ese tipo no es el principal del post-pago actual.

#### Criterio para confirmar duplicidad

- Misma solicitud y misma ventana temporal de la aprobacion financiera.
- Mismo destinatario funcional, o destinatarios equivalentes RT/operador para el mismo aviso de habilitacion.
- Mismo proposito semantico: informar que el pago fue aprobado y que la solicitud queda habilitada para continuar.
- El correo especifico cubre por si solo el evento funcional completo y `AOCR_CAMBIO_ESTADO` no agrega un destinatario ni una accion distinta.

#### Criterio para descartar duplicidad

- Solo aparece el correo especifico post-pago y no aparece `AOCR_CAMBIO_ESTADO`.
- Aparecen correos distintos pero con destinatarios y objetivos distintos, por ejemplo RT vs coordinacion.
- El generico no se observa en cola, aunque existan campanas internas `PAGO_APROBADO` en `aocr_tbnotificacion`.
- La evidencia muestra que `PAGO_APROBADO` en este slice es evento/aviso operativo y no un duplicado funcional del generico.

#### Clasificacion al cerrar la prueba

- A. Duplicidad real: mismo evento, mismo destinatario, mismo proposito.
- B. Duplicidad parcial: evento parecido con destinatario o accion distinta.
- C. No duplicado: cada correo cumple una funcion separada.
- D. Riesgo no verificable: no hubo evidencia runtime suficiente.

#### Guardrails

- No tocar `SUBSANADA`.
- No tocar `SubsanarPost`.
- No tocar `DescargarAceptacionDocumental`.
- No tocar descargas RT.
- No tocar `Finalizado`.
- No tocar roles ni autorizaciones.
- No tocar vistas, rutas ni actions.
- No tocar trigger de historial.
- No tocar textos institucionales.
- No tocar adjuntos ni legalizacion.
- No tocar `AOCR_EMITIDO_RECIBIDO`.
- No aplicar parche aunque se confirme duplicidad en esta fase.

#### Entregable al momento de ejecutar la corrida real

- Solicitud usada y orden asociada.
- Estado origen observado antes de aprobar.
- Estado persistido despues de aprobar.
- Filas generadas en `email_queue`.
- Filas generadas en `aocr_tbnotificacion`.
- Filas registradas en `aocr_audit_trail`.
- Clasificacion final del caso.
- Recomendacion.
- Pruebas minimas necesarias para un parche futuro, si algun parche llegara a considerarse.

## 10. Inventario de migracion de historial de estados

### Hallazgo separado del problema de correos

Durante la validación funcional del parche `OBSERVADA` se confirmó un segundo problema, independiente del correo: la solicitud puede registrar historial duplicado porque coexisten dos fuentes de escritura.

- historial explícito desde aplicación, por ejemplo en `SolicitudEstadoTransitionBL`;
- historial automático por trigger SQL `trg_cambio_estado` sobre `aocr_tbsolicitud`.

La fuente automática no preserva necesariamente el actor real del cambio, porque usa `NEW.codigo_usuario` y observación fija `Cambio automático`. Por eso este hallazgo debe tratarse como una línea de remediación distinta y no como parte del parche mínimo de correos.

### Callers actuales de `SolicitudAOCRDAO.CambiarEstado`

| Prioridad | Entrada funcional | Archivo actual | Método actual | Estado destino | Hoy depende del trigger | Cambio recomendado | Riesgo | Prueba mínima |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Alta | Envío de solicitud por el solicitante | `CapaPresentacion/Controllers/SolicitudController.cs` → `CapaNegocio/SolicitudBL.cs` | `Enviar` | `EnRevision` | Sí | Reemplazar la llamada directa a `SolicitudAOCRDAO.CambiarEstado` por un punto canónico con historial explícito | Bajo | Enviar una solicitud y verificar 1 sola fila de historial con actor real y observación funcional |
| Alta | Eliminación lógica de solicitud | `CapaPresentacion/Controllers/SolicitudController.cs` → `CapaNegocio/SolicitudBL.cs` | `EliminarSoft` | `ELIMINADO` | Sí | Igual patrón que `Enviar`: mover al punto canónico con historial explícito | Bajo | Eliminar una solicitud y verificar 1 sola fila de historial con usuario real |
| Media | Autoavance cuando todos los documentos quedan aprobados | `CapaPresentacion/Controllers/DocumentoController.cs` → `CapaNegocio/DocumentoBL.cs` | `VerificarDocumentosCompletos` | `DOCUMENTOS_COMPLETOS` | Sí | Encapsular el cambio en el punto canónico y registrar historial explícito con observación de sistema | Medio | Aprobar el último documento pendiente y verificar 1 sola fila de historial para `DOCUMENTOS_COMPLETOS` |
| Baja | Marcado automático para inspección | `CapaNegocio/SolicitudAOCRBL.cs` | `MarcarParaInspeccion` | `INSPECCION_SOLICITADA` | Sí | Migrar al punto canónico o retirar si la ruta ya no tiene callers reales | Medio | Si sigue en uso, ejecutar el flujo y validar 1 sola fila de historial; si no tiene callers, eliminar o marcar obsoleto |

### Callers que ya no dependen del trigger

- `CapaNegocio/SolicitudEstadoTransitionBL.cs`: ya registra historial explícito después del cambio principal.
- `CapaNegocio/Services/GeneracionAOCRService.cs`: aun en fallback registra historial explícito.

### Orden recomendado de migración

1. Migrar `Enviar` y `EliminarSoft` en `CapaNegocio/SolicitudBL.cs`, porque son rutas simples y de bajo riesgo.
2. Migrar `VerificarDocumentosCompletos` en `CapaNegocio/DocumentoBL.cs`, preservando la observación de sistema.
3. Confirmar si `MarcarParaInspeccion` en `CapaNegocio/SolicitudAOCRBL.cs` sigue vivo; si no tiene callers reales, no vale la pena migrarlo antes de limpiarlo.
4. Solo después de cubrir estas rutas conviene retirar `trg_cambio_estado` para eliminar la duplicidad de historial.

## 11. Pruebas obligatorias

### Funcionales de flujo

- inicio de trámite;
- observaciones documentales;
- subsanación;
- aceptación documental;
- autorización de inspección;
- informe técnico satisfactorio;
- informe técnico insatisfactorio;
- AOCR pendiente de firma;
- legalización;
- AOCR emitida/recibida.

### Técnicas de duplicidad e idempotencia

- reintento de POST;
- doble clic;
- refresh del navegador después de submit;
- repetición del mismo evento de negocio;
- validación de `email_queue` por `tipo_notificacion`, `event_key`, `solicitud_id`, `created_at`.

### Adjuntos y documentos

- correos con adjuntos financieros;
- informe técnico firmado final;
- declaración de responsabilidad con adjunto.

### Base de datos / cola

- existencia de fila en `email_queue`;
- unicidad lógica por `event_key` donde aplique;
- comportamiento cuando existe cola previa;
- comportamiento del worker `EmailQueueProcessor` en reintentos.

## 12. Riesgos pendientes

1. La ausencia de outbox transaccional sigue siendo un riesgo estructural aunque el envío ocurra después del éxito funcional.
2. Los servicios de AOCR e inspección seguirán reenviando correos mientras no se estandarice `EventKey` en `SolicitudAocrCorreoService` e `InspeccionCorreoService`.
3. Los adjuntos encolados no quedan plenamente garantizados hasta corregir su persistencia real en la cola.
4. Los flujos RT y administrativos todavía dependen en gran parte de SMTP directo legacy.
5. La identidad institucional del remitente sigue fragmentada en configuración.
6. El caso `SUBSANADA` requiere decisión explícita para no tocar indebidamente `SubsanarPost` ni alterar semántica del inspector.
7. Legalización y emisión final AOCR deben moverse después del cierre de duplicidad básica e idempotencia formal.
8. La duplicidad de historial por coexistencia entre trigger SQL y registro explícito desde aplicación seguirá generando trazas inconsistentes hasta centralizar el cambio de estado y retirar `trg_cambio_estado`.