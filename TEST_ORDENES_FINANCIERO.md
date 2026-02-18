# Test Ordenes Financiero

Fecha: 2026-02-13

## Precondiciones
- Usuario con rol `FINANCIERO` (o `Financiero`) autenticado.
- BD Postgres disponible con ordenes en estados PENDIENTE/PROCESADA.
- Servicio/worker de `EmailQueueService` activo.

## Casos de Prueba

1. Bandeja con filtros
- Ir a `Financiero/Index`.
- Filtrar por estado PROCESADA, fechas y solicitante.
- Verificar que se muestran ordenes y que el aviso de "sin resultados" aparece cuando aplica.

2. Detalle de orden
- Abrir detalle desde la bandeja.
- Verificar datos de orden, pago y links de PDF/comprobante.

3. Aprobar pago con factura adjunta
- En detalle, adjuntar PDF de factura.
- Ejecutar "Aprobar pago".
- Verificar cambio de estado a FACTURADA, historial y registro en `email_queue` con adjunto.

4. Aprobar orden sin pago
- En detalle de orden sin pago, aprobar con o sin PDF.
- Verificar estado FACTURADA y correo encolado.

5. Rechazar orden
- En detalle, ingresar motivo y rechazar.
- Verificar estado PENDIENTE, historial, auditoria y correo encolado (con comprobante si existe).

6. Reenviar factura
- En orden FACTURADA, usar "Reenviar factura".
- Verificar registro en `email_queue` y adjunto PDF.

7. Descarga de comprobante
- En detalle con comprobante, descargar y validar archivo.

8. Permisos
- Acceso directo a rutas financieras con usuario sin rol FINANCIERO debe ser denegado.

## Resultados Esperados
- Sin errores 500.
- Mapeo correcto de columnas y SQL sin fallos.
- Correos encolados con estado PENDIENTE y enviados por worker.
- Historial y auditoria registrados.
