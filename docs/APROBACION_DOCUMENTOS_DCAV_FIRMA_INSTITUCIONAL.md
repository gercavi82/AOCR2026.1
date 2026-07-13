# Aprobación conjunta DCAV y bandeja de firma institucional

## Diagnóstico

La aprobación anterior estaba integrada en el servicio genérico de segunda revisión, ignoraba las observaciones estructuradas porque el contador estaba fijado en cero, incluía al usuario en la clave idempotente y notificaba mediante alias amplios. La bandeja existente mezclaba estados de firma, estados legacy e informes; su contador no utilizaba la misma consulta.

El rol firmante real verificado en `dgac_des` es `Direccion` (`codigorol=17`). El código no fija el número: resuelve el rol por su descripción persistida para evitar dependencias entre ambientes.

## Diseño

`IAprobacionDocumentosDcavService` ejecuta la transición única `PENDIENTE_REVISION_DOCUMENTOS_DCAV → PENDIENTE_FIRMA_DIRDAC`. Bajo aislamiento serializable recupera el paquete exacto del evento de envío, valida usuario DCAV activo, estado/versiones, AOCR, Condiciones, PDF físico/tamaño/SHA-256, Informe Técnico, LV/EAE y que toda observación esté `CERRADA_DCAV`.

Las dos actualizaciones documentales, el cambio central, historial, nueve eventos de auditoría, notificaciones, outbox e idempotencia se realizan en la misma transacción. La clave no contiene usuario: `SolicitudId:InspeccionId:AocrId:VersionAocr:CondicionesId:VersionCondiciones:APROBAR_DOCUMENTOS_DCAV`.

La bandeja `/aocr/FirmaInstitucionalAocr/Pendientes` está autorizada exclusivamente para `Direccion`, es de sólo consulta y usa exactamente la misma consulta que su contador. La aplicación de firmas queda fuera de esta fase.

## Despliegue y reversión

Ejecutar `scripts/010_aprobacion_documentos_dcav_firma_institucional.sql`. No crea tablas ni columnas; sólo valida el esquema/rol e incorpora dos índices parciales. El rollback elimina exclusivamente esos índices y no revierte aprobaciones funcionales ya confirmadas.

## Verificación

Compilar `AOCR.sln`, compilar Razor y ejecutar `AprobacionDocumentosDcavTests` junto con sus pruebas de integración. Si no existe un expediente real pendiente no se deben fabricar documentos ni modificar solicitudes para simular la transición.
