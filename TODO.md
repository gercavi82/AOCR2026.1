# TODO - Correcciones integrales AOCR por roles

## Paso 0 (Preparación)
- [x] Analizar ubicación de lógica mezclada por rol: principalmente `CapaPresentacion/Controllers/SolicitudAOCRController.cs`.
- [x] Identificar servicios/BL relevantes: `SolicitudEstadoTransitionBL`, `NotificacionBL`, validaciones documentales.

## Paso 1 (Matriz canónica)
- [ ] Crear/implementar un servicio/helper en CapaNegocio que defina:
  - rol → estados permitidos → acciones → bandeja destino
  - rol propietario de cada etapa
  - reglas de "quién ejecuta" cada acción

## Paso 2 (Refuerzo de backend por endpoints)
- [ ] Endurecer `SolicitudAOCRController.cs` para que:
  - RT solo ejecute acciones técnicas/documentales que correspondan
  - Coordinación solo revise/observe/acepte formalmente y asigne/remita
  - Inspector solo reciba cuando venga de Coordinación y ejecute revisión técnica/LV/Informe/NC
  - Financiero valide solo pago/comprobante/factura
  - DIRDAC/DCAV firme AOCR/Condiciones y libere finales

## Paso 3 (Bandejas / Sidebar / Contadores)
- [ ] Ajustar consultas de bandejas para que filtren por rol propietario y estado real.
- [ ] Corregir contadores del sidebar contra el conteo real.

## Paso 4 (Documentos: carga múltiple/versionado/descarga)
- [ ] Verificar y corregir en RT que la carga múltiple NO borre documentos anteriores.
- [ ] Asegurar versionado y listado ordenado por sección/documento.

## Paso 5 (Notificaciones y trazabilidad)
- [ ] Alinear generación de notificaciones con transiciones reales y rol correcto.
- [ ] Evitar duplicados con idempotencia/eventKey.
- [ ] Confirmar trazabilidad completa: estado anterior→nuevo, observación, módulo, usuario.

## Paso 6 (Pruebas)
- [ ] Build/compilación sin errores.
- [ ] Ejecutar tests existentes (`AOCR.Tests`) y/o checklist de regresión.
- [ ] Validar manualmente:
  - permisos por URL directa
  - JS console errors
  - 500 en backend
  - no saltos de rol en el flujo

