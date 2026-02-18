# Diseno Flujo Financiero - Ordenes de Recaudacion

Fecha: 2026-02-13
Alcance: Modulo financiero (recepcion, revision, aprobacion/rechazo y correos)

## Flujo Paso a Paso (Financiero)
1. Bandeja Financiero (`Financiero/Index`) lista ordenes filtradas por estado/fecha/solicitante/numero.
2. Usuario abre detalle (`Financiero/DetalleOrden?id={id}`) con orden, pago y historial.
3. Aprobacion:
- Si hay pago, `Financiero/AprobarPago` valida pago, cambia estado orden a FACTURADA y encola correo con factura.
- Si no hay pago, `Financiero/AprobarOrden` cambia estado y encola correo con factura generada o adjunta.
4. Rechazo:
- `Financiero/RechazarOrden` registra motivo, cambia estado a PENDIENTE y encola correo con comprobante si existe.
5. Reenvio factura:
- `Financiero/ReenviarFactura` regenera PDF y encola correo.
6. Worker de correo procesa `email_queue` con reintentos y adjuntos.

## Mapa (Sidebar -> Ruta -> Controller -> BL/DAO -> DB -> EmailQueue -> Worker)
- Sidebar -> `Financiero/Index` -> `FinancieroController.Index` -> `OrdenRecaudacionDAO.ListarFiltrado` -> Postgres (`aocr_or_orden`, `aocr_or_pago`) -> (sin correo)
- Detalle -> `Financiero/DetalleOrden` -> `FinancieroController.DetalleOrden` -> `OrdenRecaudacionDAO.ObtenerOrdenPorId`, `ObtenerUltimoPagoPorOrden` -> Postgres -> (sin correo)
- Aprobar -> `Financiero/AprobarPago` o `Financiero/AprobarOrden` -> `OrdenRecaudacionDAO.ActualizarPagoYEstadoTransaccional`, `HistorialEstadoBL.RegistrarCambioEstado` -> Postgres -> `EmailQueueService.EncolarAsync` -> `email_queue` -> `EmailQueueService.ProcessItemAsync`
- Rechazar -> `Financiero/RechazarOrden` -> `OrdenRecaudacionDAO.ActualizarPagoYEstadoTransaccional` -> Postgres -> `EmailQueueService.EncolarAsync` -> `email_queue` -> Worker
- Reenvio -> `Financiero/ReenviarFactura` -> `EmailQueueService.EncolarAsync` -> `email_queue` -> Worker

## Estados y Transiciones Permitidas
- BORRADOR -> GENERADA (solicitante)
- GENERADA -> PENDIENTE (solicitante adjunta comprobante)
- PENDIENTE -> PROCESADA (validacion interna previa, si aplica)
- PROCESADA -> FACTURADA (financiero aprueba)
- PROCESADA -> PENDIENTE (financiero rechaza)
- PENDIENTE -> FACTURADA (financiero aprueba)
- FACTURADA -> COMPLETADA (cierre administrativo o proceso posterior)
- Cualquier estado -> ANULADA (segun reglas del modulo principal)

## Plantillas de Correo
- `Views/EmailTemplates/OrdenAprobada.cshtml` (Orden Facturada)
- `Views/EmailTemplates/OrdenRechazada.cshtml` (Orden Rechazada)
- `Views/EmailTemplates/PagoRecibidoFinanciero.cshtml` (Notificacion a Financiero)

## Auditoria
- Se registra historial de estado en `HistorialEstadoBL` y auditoria con `AuditService` para orden y pago.
