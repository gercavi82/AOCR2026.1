# TEST_CHECKLIST

## Navegacion y UI
- [ ] Abrir app y verificar que el layout carga (sidebar sin 500).
- [ ] Click en "Nueva Orden" desde el sidebar -> /OrdenRecaudacion/Nueva.
- [ ] Abrir /OrdenRecaudacion/Obligatoria directo y verificar que carga listado.

## Nueva Orden (GET)
- [ ] Ver combos de conceptos cargados.
- [ ] Ver combos de solicitudes (segun rol).

## Nueva Orden (POST)
- [ ] Crear orden con 1 concepto -> debe guardar en aocr_or_orden.
- [ ] Ver que crea detalles en aocr_or_orden_detalle.
- [ ] Ver que redirige a Detalles con mensaje OK.

## Mis Ordenes
- [ ] Ir a /Orden/MisOrdenes y verificar que aparece la orden creada.
- [ ] Validar que subtotal/admin/total no rompen si vienen null.

## Estados y transiciones
- [ ] Generar orden BORRADOR -> PENDIENTE/GENERADA.
- [ ] Verificar que CambiarEstado actualiza en DB.

## BD / Conexion
- [ ] Confirmar que connectionStrings usa AOCRConnection/PostgreSQL en Web.config.
- [ ] Verificar querys usan aocr_or_orden y aocr_or_orden_detalle.

## Roles / Autorizacion
- [ ] Usuario sin rol permitido no puede acceder a Nueva (401/403 controlado).
- [ ] Usuario admin puede crear/editar.
