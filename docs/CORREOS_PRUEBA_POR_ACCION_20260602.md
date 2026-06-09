# CORREOS_PRUEBA_POR_ACCION_20260602

## 1. Objetivo

Este documento reúne ejemplos de correos de prueba para las acciones principales del flujo AOCR.

Los asuntos y mensajes están alineados con las plantillas reales vigentes en:

- `CapaNegocio/Services/SolicitudAocrCorreoService.cs`
- `CapaNegocio/Services/InspeccionCorreoService.cs`
- `CapaNegocio/Services/OrdenRecaudacionCorreoService.cs`
- `CapaNegocio/NotificacionBL.cs`
- `CapaPresentacion/Controllers/SolicitudAOCRController.cs`

## 2. Datos base sugeridos para pruebas

Usar datos ficticios consistentes ayuda a revisar el contenido visual y funcional sin mezclar casos reales.

| Campo | Valor de ejemplo |
| --- | --- |
| Solicitud AOCR | `#101` |
| Numero de solicitud | `AOCR-2026-000101` |
| Operador | `AeroAndes S.A.` |
| Codigo OACI | `AAND` |
| Estado actual | `OBSERVADA` |
| Inspeccion | `#55` |
| Orden de recaudacion | `OR-2026-00125` |
| Representante Tecnico | `Mariana Salazar` |
| Inspector | `Carlos Paredes` |
| Coordinacion | `Coordinacion de Inspeccion AOCR` |
| Nombre visible sandbox | `Pruebas AOCR` |
| Destinatario prueba RT | `gercavi82@gmail.com` |
| Destinatario prueba Operador | `gercavi82@gmail.com` |
| Destinatario prueba Inspector | `gercavi82@gmail.com` |
| Destinatario prueba Coordinacion | `gercavi82@gmail.com` |
| Destinatario prueba Financiero | `gercavi82@gmail.com` |
| Fecha de registro | `02/06/2026 10:30` |
| Observacion de ejemplo | `Se requiere actualizar la vigencia del certificado de aeronavegabilidad y corregir la firma del documento de poder.` |

Nota: en el ambiente actual de QA se usa un único buzón sandbox para todos los roles de prueba. Por eso los ejemplos del documento y la pantalla `CorreosPrueba` apuntan por defecto a `gercavi82@gmail.com` con nombre visible `Pruebas AOCR`.

## 3. Formato sugerido para pruebas manuales

Para cada prueba de correo conviene registrar al menos:

| Campo | Valor a capturar |
| --- | --- |
| Accion ejecutada | nombre funcional del evento |
| Asunto esperado | asunto exacto del correo |
| Destinatario esperado | grupo o correo de prueba |
| Tipo notificacion | valor de `tipo_notificacion` si aplica |
| EventKey | valor esperado o `N/A` |
| Cuerpo validado | `SI/NO` |
| Observaciones | diferencias encontradas |

## 4. Correos de prueba por accion

### 4.1 Solicitud AOCR registrada

Accion: registro inicial de solicitud desde controlador.

Asunto:

```text
AOCR - Solicitud registrada AOCR-2026-000101
```

Cuerpo ejemplo:

```html
<p>Estimado/a solicitante,</p>
<p>Su solicitud AOCR se registró correctamente en el sistema.</p>
<ul>
  <li><strong>Número de solicitud:</strong> AOCR-2026-000101</li>
  <li><strong>Operador:</strong> AeroAndes S.A.</li>
  <li><strong>Código OACI:</strong> AAND</li>
  <li><strong>Fecha de registro:</strong> 02/06/2026 10:30</li>
</ul>
<p>Puede revisar el detalle en el siguiente enlace: <a href="https://aocr.local/SolicitudAOCR/Detalle/101">Ver solicitud</a>.</p>
<p>Atentamente,<br/>Dirección General de Aviación Civil</p>
```

Destinatario de prueba:

- `rt.pruebas@aocr.local`

### 4.2 AOCR_CAMBIO_ESTADO

Accion: correo generico por cambio de estado en `NotificacionBL`.

Asunto:

```text
AOCR - Cambio de Estado
```

Cuerpo ejemplo:

```html
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; padding: 16px;'>
  <div style='max-width: 620px; margin: 0 auto; border: 1px solid #e5e7eb; border-radius: 8px; overflow: hidden;'>
    <div style='background:#1B4F72; color:#fff; padding: 12px 16px; font-weight: 600;'>Sistema AOCR</div>
    <div style='padding: 16px;'>
      <p>Estimado/a <strong>Mariana Salazar</strong>,</p>
      <p>La solicitud <strong>#101</strong> cambió al estado <strong>OBSERVADA</strong>.</p>
      <p>Puede revisar el detalle en el portal AOCR.</p>
      <hr style='border:none;border-top:1px solid #eee;margin:16px 0;' />
      <p style='font-size:12px;color:#666;'>Notificación automática, por favor no responder.</p>
    </div>
  </div>
</body>
</html>
```

Destinatario de prueba:

- `rt.pruebas@aocr.local`

### 4.3 AOCR_APROBADO_DIRECCION

Asunto:

```text
AOCR - Solicitud aprobada por Dirección #101
```

Cuerpo ejemplo:

```text
Titulo: Solicitud aprobada por Dirección
Mensaje principal: La solicitud AOCR fue aprobada por Dirección y pasa al tramo de legalización institucional.
Resumen:
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Estado actual: APROBADA_DIRECCION
- Observacion: Expediente aprobado para continuar con la legalización.
```

Destinatarios de prueba:

- `operador.pruebas@aocr.local`
- `rt.pruebas@aocr.local`
- `coordinacion.pruebas@aocr.local`

### 4.4 AOCR_LEGALIZADO

Asunto:

```text
AOCR - Solicitud legalizada #101
```

Cuerpo ejemplo:

```text
Titulo: AOCR legalizado
Mensaje principal: La solicitud AOCR fue legalizada y el certificado queda habilitado para su emisión institucional.
Resumen:
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Estado actual: AOCR_LEGALIZADO
- Observacion: Expediente legalizado por coordinación legal.
```

Destinatarios de prueba:

- `operador.pruebas@aocr.local`
- `rt.pruebas@aocr.local`
- `coordinacion.pruebas@aocr.local`

### 4.5 AOCR_EMITIDO_RECIBIDO

Asunto:

```text
AOCR - Certificado emitido y entregado #101
```

Cuerpo ejemplo:

```text
Titulo: AOCR emitido y entregado
Mensaje principal: El certificado AOCR fue emitido y marcado como recibido. El tramite queda completado en su tramo institucional final.
Resumen:
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Estado actual: AOCR_EMITIDO_RECIBIDO
- Observacion: Certificado entregado al operador y recibido conforme.
```

Destinatarios de prueba:

- `operador.pruebas@aocr.local`
- `rt.pruebas@aocr.local`
- `coordinacion.pruebas@aocr.local`

### 4.6 INSPECTOR_ASIGNADO

Asunto:

```text
AOCR - Inspector asignado a solicitud AOCR-2026-000101
```

Cuerpo ejemplo:

```text
Titulo: Inspector asignado
Mensaje principal: Por medio del presente, se informa que ha sido asignado/a como Inspector a la solicitud AOCR-2026-000101.
Resumen:
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Estado actual: EN_INSPECCION
- Observacion: Se asigna al inspector Carlos Paredes para revisión técnica.
```

Destinatarios de prueba:

- `inspector.pruebas@aocr.local`
- `operador.pruebas@aocr.local`
- `rt.pruebas@aocr.local`
- `coordinacion.pruebas@aocr.local`

### 4.7 OBSERVADA

Asunto:

```text
AOCR - Observaciones en revisión documental #101
```

Cuerpo ejemplo:

```text
Titulo: Observaciones en revisión documental
Mensaje principal: La solicitud AOCR presenta observaciones en la revisión documental. Debe corregir los documentos indicados y reenviarlos para continuar con el trámite.
Resumen:
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Estado actual: OBSERVADA
- Observacion: Se requiere actualizar la vigencia del certificado de aeronavegabilidad y corregir la firma del documento de poder.
```

Destinatarios de prueba:

- `rt.pruebas@aocr.local`
- `operador.pruebas@aocr.local`

### 4.8 SUBSANADA

Asunto:

```text
AOCR - Correcciones documentales enviadas por RT #101
```

Cuerpo ejemplo:

```text
Titulo: Correcciones documentales enviadas
Mensaje principal: El Representante Técnico ha enviado las correcciones documentales solicitadas. Por favor, revise los documentos actualizados para continuar con el flujo de inspección.
Resumen:
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Estado actual: SUBSANADA
- Observacion: El RT cargó nuevamente certificados, anexos y documentos corregidos.
```

Destinatarios de prueba:

- `inspector.pruebas@aocr.local`
- `coordinacion.pruebas@aocr.local`

### 4.9 ACEPTACION_DOCUMENTAL

Asunto:

```text
AOCR - Revisión documental aprobada #101
```

Cuerpo ejemplo:

```text
Titulo: Revisión documental aprobada
Mensaje principal: La revisión documental de la solicitud AOCR fue completada satisfactoriamente. Todos los documentos han sido aprobados y se habilitó la ejecución de la inspección técnica.
Resumen:
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Estado actual: ACEPTACION_DOCUMENTAL
- Observacion: Todos los documentos vigentes fueron aprobados por el inspector.
```

Destinatarios de prueba:

- `rt.pruebas@aocr.local`
- `inspector.pruebas@aocr.local`
- `coordinacion.pruebas@aocr.local`

### 4.10 ACEPTACION_COORDINADOR_FIRMADA

Asunto:

```text
AOCR - Aceptación documental firmada #101
```

Cuerpo ejemplo:

```text
Titulo: Aceptación documental firmada
Mensaje principal: La coordinación firmó la aceptación documental de la solicitud AOCR. El documento final ya se encuentra disponible para su descarga desde el expediente.
Resumen:
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Estado actual: ACEPTACION_COORDINADOR_FIRMADA
- Observacion: Acta firmada y cargada al expediente.
```

Destinatarios de prueba:

- `rt.pruebas@aocr.local`
- `operador.pruebas@aocr.local`

### 4.11 PENDIENTE_ASIGNACION_INSPECTOR o SOLICITUD_COMPLETADA

Asunto:

```text
AOCR - Solicitud completada por RT, pendiente asignación de inspector #101
```

Cuerpo ejemplo:

```text
Titulo: Solicitud AOCR completada
Mensaje principal: El Representante Técnico completó el llenado de la solicitud AOCR. La solicitud se encuentra pendiente de asignación de inspector para continuar con el proceso de inspección.
Resumen:
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Estado actual: PENDIENTE_ASIGNACION_INSPECTOR
- Observacion: Solicitud formal enviada por RT y lista para asignación.
```

Destinatario de prueba:

- `coordinacion.pruebas@aocr.local`

### 4.12 PAGO_APROBADO o SOLICITUD_HABILITADA

Asunto:

```text
AOCR - Pago aprobado, solicitud habilitada #101
```

Cuerpo ejemplo:

```text
Titulo: Solicitud AOCR habilitada
Mensaje principal: El pago de la orden de recaudación fue aprobado por Financiero. La solicitud AOCR ya se encuentra disponible para que complete el formulario y adjunte la documentación requerida.
Resumen:
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Estado actual: PAGO_APROBADO
- Observacion: Financiero aprobó el comprobante de pago.
```

Destinatarios de prueba:

- `rt.pruebas@aocr.local`
- `operador.pruebas@aocr.local`

### 4.13 DIRDAC_APROBO_INFORME

Asunto:

```text
AOCR - Informe técnico aprobado por DIRDAC, certificado habilitado #101
```

Cuerpo ejemplo:

```text
Titulo: Informe técnico aprobado por DIRDAC
Mensaje principal: El Informe Técnico fue aprobado por DIRDAC sin observaciones. El Certificado AOCR se encuentra habilitado para su generación y firma por el Coordinador.
Resumen:
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Estado actual: DIRDAC_APROBO_INFORME
- Observacion: Dirección aprobó el informe técnico y habilitó la emisión del certificado.
```

Destinatarios de prueba:

- `coordinacion.pruebas@aocr.local`
- `rt.pruebas@aocr.local`

### 4.14 DIRDAC_DEVOLVIO_INFORME

Asunto:

```text
AOCR - Informe técnico devuelto por DIRDAC #101
```

Cuerpo ejemplo:

```text
Titulo: Informe técnico devuelto por DIRDAC
Mensaje principal: DIRDAC / Dirección devolvió el Informe Técnico con observaciones. El Inspector y/o Coordinador deben revisar las observaciones indicadas y subsanar el informe antes de reenviarlo.
Resumen:
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Estado actual: DEVUELTO_DIRDAC
- Observacion: Se solicita ampliar el fundamento técnico y corregir anexos del informe.
```

Destinatarios de prueba:

- `inspector.pruebas@aocr.local`
- `coordinacion.pruebas@aocr.local`

### 4.15 NC_GENERADAS

Asunto:

```text
AOCR - No conformidades registradas en inspeccion #55
```

Cuerpo ejemplo:

```text
Titulo: No conformidades registradas
Mensaje principal: Se registraron no conformidades que requieren validacion de coordinacion y subsanacion del RT.
Resumen:
- Inspeccion: #55
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Observacion: Se registraron tres no conformidades durante la inspección de plataforma.
```

Destinatarios de prueba:

- `rt.pruebas@aocr.local`
- `operador.pruebas@aocr.local`
- `coordinacion.pruebas@aocr.local`

### 4.16 DOCUMENTOS_SUBSANADOS

Asunto:

```text
AOCR - Documentacion subsanada en inspeccion #55
```

Cuerpo ejemplo:

```text
Titulo: Documentacion subsanada
Mensaje principal: El RT actualizo documentos asociados a no conformidades y el expediente requiere revalidacion.
Resumen:
- Inspeccion: #55
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Observacion: El RT adjuntó nuevos respaldos y documentos corregidos.
```

Destinatarios de prueba:

- `inspector.pruebas@aocr.local`
- `coordinacion.pruebas@aocr.local`

### 4.17 DEVOLUCION_INSPECCION

Asunto:

```text
AOCR - Tramite de inspeccion devuelto #55
```

Cuerpo ejemplo:

```text
Titulo: Tramite devuelto para correccion
Mensaje principal: La inspeccion fue devuelta para correccion o para programar una nueva inspeccion, segun observaciones registradas.
Resumen:
- Inspeccion: #55
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Observacion: La inspección fue devuelta para corrección del expediente y reprogramación.
```

Destinatarios de prueba:

- `inspector.pruebas@aocr.local`
- `rt.pruebas@aocr.local`
- `operador.pruebas@aocr.local`
- `coordinacion.pruebas@aocr.local`

### 4.18 APROBACION_INSPECCION

Asunto:

```text
AOCR - Inspeccion aprobada #55
```

Cuerpo ejemplo:

```text
Titulo: Inspeccion aprobada
Mensaje principal: La inspeccion fue aprobada y el expediente queda listo para el siguiente tramo institucional.
Resumen:
- Inspeccion: #55
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Observacion: La inspección fue aprobada y el expediente avanza a revisión institucional.
```

Destinatarios de prueba:

- `coordinacion.pruebas@aocr.local`

### 4.19 REVALIDACION_OK

Asunto:

```text
AOCR - Revalidacion satisfactoria #55
```

Cuerpo ejemplo:

```text
Titulo: Revalidacion satisfactoria
Mensaje principal: La revalidacion de la inspeccion fue satisfactoria y el tramite puede continuar.
Resumen:
- Inspeccion: #55
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Observacion: Todas las no conformidades fueron cerradas satisfactoriamente.
```

Destinatarios de prueba:

- `coordinacion.pruebas@aocr.local`
- `rt.pruebas@aocr.local`
- `operador.pruebas@aocr.local`

### 4.20 REVALIDACION_RECHAZADA

Asunto:

```text
AOCR - Revalidacion con observaciones #55
```

Cuerpo ejemplo:

```text
Titulo: Revalidacion con observaciones
Mensaje principal: La revalidacion mantiene observaciones pendientes y se requiere una nueva subsanacion o ajuste del tramite.
Resumen:
- Inspeccion: #55
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Observacion: Persisten hallazgos técnicos y se requiere una nueva corrección documental.
```

Destinatarios de prueba:

- `inspector.pruebas@aocr.local`
- `coordinacion.pruebas@aocr.local`
- `rt.pruebas@aocr.local`
- `operador.pruebas@aocr.local`

### 4.21 PENDIENTE_FIRMA_DIRDAC

Asunto:

```text
AOCR - Documentos pendientes de revision DIRDAC / Direccion - Jefatura AOCR-2026-000101
```

Cuerpo ejemplo:

```text
Titulo: Documentos pendientes de revision institucional
Mensaje principal: Tiene documentos pendientes de revision institucional en la bandeja de DIRDAC / Direccion - Jefatura. El informe tecnico ya fue firmado por el inspector asignado y queda listo para decision final. La notificacion al RT se emitira una vez que Direccion / Jefatura registre la decision institucional correspondiente.
Resumen:
- Inspeccion: #55
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Observacion: Expediente enviado a decisión institucional final.
```

Destinatario de prueba:

- `coordinacion.pruebas@aocr.local`

### 4.22 INFORME_TECNICO_FIRMADO

Asunto:

```text
AOCR - Informe técnico aprobado AOCR-2026-000101
```

Cuerpo ejemplo:

```text
Titulo: Informe técnico aprobado
Mensaje principal: El informe técnico ya cuenta con la aprobación institucional requerida y queda habilitado para el expediente AOCR.
Resumen:
- Inspeccion: #55
- Solicitud AOCR: #101
- Numero de solicitud: AOCR-2026-000101
- Operador: AeroAndes S.A.
- Observacion: El informe técnico final fue firmado y publicado en el expediente.
```

Destinatarios de prueba:

- `rt.pruebas@aocr.local`
- `coordinacion.pruebas@aocr.local`
- `inspector.pruebas@aocr.local`

### 4.23 ORDEN_RECAUDACION_GENERADA_FINANCIERO

Asunto:

```text
Nueva Orden de Recaudación generada para solicitud AOCR #101
```

Cuerpo ejemplo:

```text
Titulo: Orden de Recaudación generada
Mensaje principal: Se ha generado una nueva Orden de Recaudación asociada a una Solicitud AOCR y queda pendiente la revisión del pago por el área financiera.
Resumen:
- Solicitud AOCR: #101
- Orden: OR-2026-00125
- Operador: AeroAndes S.A.
- Observacion: Orden enviada al área financiera para seguimiento.
```

Destinatario de prueba:

- `financiero.pruebas@aocr.local`

### 4.24 ORDEN_CREADA

Asunto:

```text
Nueva Orden de recaudación - OR-2026-00125
```

Cuerpo ejemplo:

```text
Titulo: Orden de recaudación generada
Mensaje principal: Se generó una nueva Orden de Recaudación asociada a su trámite. Revise el detalle y proceda con el pago correspondiente.
Resumen:
- Solicitud AOCR: #101
- Orden: OR-2026-00125
- Operador: AeroAndes S.A.
- Observacion: Orden disponible para pago del operador.
```

Destinatario de prueba:

- `operador.pruebas@aocr.local`

### 4.25 PAGO_REGISTRADO

Asunto:

```text
Pago registrado - Orden OR-2026-00125
```

Cuerpo ejemplo:

```text
Titulo: Pago registrado
Mensaje principal: El pago de la orden fue registrado y queda pendiente de validación por el área financiera.
Resumen:
- Solicitud AOCR: #101
- Orden: OR-2026-00125
- Operador: AeroAndes S.A.
- Observacion: Comprobante cargado por el operador y pendiente de validación.
```

Destinatarios de prueba:

- `operador.pruebas@aocr.local`
- `financiero.pruebas@aocr.local`

### 4.26 PAGO_VALIDADO

Asunto:

```text
Pago validado - Orden OR-2026-00125
```

Cuerpo ejemplo:

```text
Titulo: Pago validado
Mensaje principal: El pago de la orden fue validado correctamente y el trámite financiero puede continuar.
Resumen:
- Solicitud AOCR: #101
- Orden: OR-2026-00125
- Operador: AeroAndes S.A.
- Observacion: Pago validado por el área financiera.
```

Destinatarios de prueba:

- `operador.pruebas@aocr.local`
- `financiero.pruebas@aocr.local`

### 4.27 FACTURA_GENERADA

Asunto:

```text
Factura generada - Orden OR-2026-00125
```

Cuerpo ejemplo:

```text
Titulo: Factura generada
Mensaje principal: La factura asociada a la orden fue generada y queda disponible para su consulta.
Resumen:
- Solicitud AOCR: #101
- Orden: OR-2026-00125
- Operador: AeroAndes S.A.
- Observacion: Factura emitida y publicada para consulta.
```

Destinatarios de prueba:

- `operador.pruebas@aocr.local`
- `financiero.pruebas@aocr.local`

## 5. Sugerencia de uso en pruebas

Orden recomendado para pruebas manuales:

1. Probar primero `SOLICITUD_REGISTRADA`, `PAGO_APROBADO`, `OBSERVADA` y `ACEPTACION_DOCUMENTAL`.
2. Validar luego `SUBSANADA` por ser el caso más sensible de duplicidad actual.
3. Dejar para una segunda ronda los eventos con adjuntos o tramo final: `ORDEN_CREADA`, `PAGO_REGISTRADO`, `FACTURA_GENERADA`, `AOCR_LEGALIZADO`, `AOCR_EMITIDO_RECIBIDO`, `INFORME_TECNICO_FIRMADO`.

## 6. Observaciones

- Los alias funcionales comparten plantilla en varios casos. Por ejemplo: `OBSERVADA` y `REVISION_DOCUMENTAL_OBSERVADA`; `SUBSANADA` y `CORRECCIONES_ENVIADAS_RT`; `PAGO_APROBADO` y `SOLICITUD_HABILITADA`.
- En varios servicios el cuerpo final HTML incluye un resumen estructurado, saludo al destinatario y observación del trámite. En este documento se resume el contenido esperado para facilitar pruebas manuales.
- Si se requiere, este catálogo puede convertirse después en una matriz CSV o en un script de seed para encolar correos de prueba automáticamente.