# Matriz de Notificaciones Workflow AOCR

## Objetivo

Definir un contrato funcional unico para correos automáticos del sistema AOCR, indicando evento, destinatarios y plantilla base. La resolucion de destinatarios debe centralizarse en servicios de politica y no en controladores o DAOs dispersos.

## Politica compartida vigente

- Servicio base: `CapaNegocio/Services/NotificacionDestinatarioPolicyService.cs`
- Servicios que ya consumen esta politica:
  - `InspeccionCorreoService`
  - `SolicitudAocrCorreoService`
  - `OrdenRecaudacionCorreoService`

## Grupos de destinatarios

| Grupo | Fuente actual | Uso |
|---|---|---|
| `OPERADOR_SOLICITANTE` | `SolicitudAOCR.Email` / `OrdenRecaudacion.Correo` | Usuario externo principal |
| `REPRESENTANTE_TECNICO` | `SolicitudAOCR.CorreoRepresentanteTecnico` | RT / representante |
| `INSPECTOR_ASIGNADO` | `SolicitudAOCR.CodigoTecnico` / `Inspeccion.CodigoInspector` | Inspector responsable |
| `COORDINACION_INSPECCION` | `usuario_rol + rol` | Coordinación de inspección |
| `COORDINACION_LEGAL` | `usuario_rol + rol` | Coordinación legal |
| `DIRECCION_FINAL` | `usuario_rol + rol` | Dirección / director general / jefatura |
| `FINANCIERO` | `usuario_rol + rol` | Financiero / coordinación financiera |

## Matriz de eventos

### Inspección

| Evento | Servicio | Destinatarios | Plantilla |
|---|---|---|---|
| `NC_GENERADAS` | `InspeccionCorreoService` | `REPRESENTANTE_TECNICO`, `OPERADOR_SOLICITANTE`, `COORDINACION_INSPECCION` | NC registradas |
| `DOCUMENTOS_SUBSANADOS` | `InspeccionCorreoService` | `INSPECTOR_ASIGNADO`, `COORDINACION_INSPECCION` | Documentación subsanada |
| `DEVOLUCION_INSPECCION` | `InspeccionCorreoService` | `INSPECTOR_ASIGNADO`, `REPRESENTANTE_TECNICO`, `OPERADOR_SOLICITANTE`, `COORDINACION_INSPECCION` | Trámite devuelto |
| `APROBACION_INSPECCION` | `InspeccionCorreoService` | `COORDINACION_INSPECCION`, `COORDINACION_LEGAL`, `DIRECCION_FINAL` | Inspección aprobada |
| `REVALIDACION_OK` | `InspeccionCorreoService` | `COORDINACION_INSPECCION`, `REPRESENTANTE_TECNICO`, `OPERADOR_SOLICITANTE` | Revalidación satisfactoria |
| `REVALIDACION_RECHAZADA` | `InspeccionCorreoService` | `INSPECTOR_ASIGNADO`, `COORDINACION_INSPECCION`, `REPRESENTANTE_TECNICO`, `OPERADOR_SOLICITANTE` | Revalidación observada |

### Solicitud AOCR

| Evento | Servicio | Destinatarios | Plantilla |
|---|---|---|---|
| `AOCR_APROBADO_DIRECCION` | `SolicitudAocrCorreoService` | `OPERADOR_SOLICITANTE`, `REPRESENTANTE_TECNICO`, `COORDINACION_LEGAL` | Aprobación por dirección |
| `AOCR_LEGALIZADO` | `SolicitudAocrCorreoService` | `OPERADOR_SOLICITANTE`, `REPRESENTANTE_TECNICO`, `COORDINACION_LEGAL`, `DIRECCION_FINAL` | Legalización AOCR |
| `AOCR_EMITIDO_RECIBIDO` | `SolicitudAocrCorreoService` | `OPERADOR_SOLICITANTE`, `REPRESENTANTE_TECNICO`, `COORDINACION_LEGAL`, `DIRECCION_FINAL` | Emisión y entrega AOCR |

### Orden de Recaudación

| Evento | Servicio | Destinatarios | Plantilla |
|---|---|---|---|
| `ORDEN_CREADA` | `OrdenRecaudacionCorreoService` | `OPERADOR_SOLICITANTE` | Orden generada |
| `PAGO_REGISTRADO` | `OrdenRecaudacionCorreoService` | `OPERADOR_SOLICITANTE`, `FINANCIERO` | Pago registrado |
| `PAGO_VALIDADO` | `OrdenRecaudacionCorreoService` | `OPERADOR_SOLICITANTE`, `FINANCIERO` | Pago validado |
| `FACTURA_GENERADA` | `OrdenRecaudacionCorreoService` | `OPERADOR_SOLICITANTE`, `FINANCIERO` | Factura generada |

## Reglas de implementación

1. Todo correo nuevo debe pasar por la politica compartida.
2. No se deben resolver roles ni correos dentro de controllers.
3. Los controladores solo disparan eventos de negocio.
4. Si un evento requiere adjunto, se debe encolar mediante `EmailQueueService` con metadatos del adjunto.
5. Si un destinatario externo no existe en el expediente, el evento debe degradar sin bloquear el request.

## Riesgos vigentes

1. La calidad de destinatarios internos depende de asignaciones correctas en `usuario_rol`.
2. Algunas rutas legacy siguen pudiendo disparar eventos equivalentes desde distintos controladores.
3. El grupo `FINANCIERO` ya existe en politica, pero puede requerir ampliacion si aparecen roles operativos adicionales.