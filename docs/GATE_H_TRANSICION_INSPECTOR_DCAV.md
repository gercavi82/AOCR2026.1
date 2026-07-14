# GATE H — Transición Inspector → DCAV

## Decisión funcional

Se implementa el flujo DCAV explícito. El rol responsable de revisar el Informe Técnico es `DirectorCertificacionesDcav`. Los aliases anteriores de Dirección/DIRDAC se conservan temporalmente para compatibilidad, pero la fuente de la bandeja es exclusivamente el estado central `PENDIENTE_REVISION_INFORME_DCAV`.

## Transiciones

1. El Inspector finaliza y firma un informe con resultado `SATISFACTORIO`.
2. En una unidad transaccional se marca el informe como enviado y el expediente queda en `PENDIENTE_REVISION_INFORME_DCAV`, etapa `REVISION_INFORME_DCAV`, responsable `DirectorCertificacionesDcav`.
3. La bandeja consulta `aocr_proceso_estado`; no infiere pendientes desde aliases históricos de `estado_informe`.
4. La aprobación cambia el estado central a `INFORME_TECNICO_APROBADO_DCAV` y etapa `EMISION_AOCR_CONDICIONES`, habilitando los Módulos 7 y 8 mediante el servicio existente de generación AOCR.
5. La devolución cambia a `INFORME_TECNICO_OBSERVADO_DCAV` y devuelve la responsabilidad al Inspector.

## Base de datos

Aplicar `scripts/sql/012_gate_h_dcav_estado_central.sql` antes de desplegar los ensamblados. El rollback elimina la infraestructura creada por esta fase y solo debe ejecutarse después de revertir la aplicación.

## Seguridad y compatibilidad

`DirectorCertificacionesDcav` se agregó a la autorización de decisión institucional y al sidebar. No se concede esta capacidad a Coordinación. Las rutas antiguas `PendientesDireccion` y `RevisionDireccion` se conservan para no romper marcadores, pero leen el flujo DCAV nuevo.

## Despliegue

1. Respaldar la base.
2. Ejecutar migración 012.
3. Desplegar `CapaDatos.dll`, `CapaNegocio.dll`, `CapaPresentacion.dll` y vistas.
4. Probar firma Inspector satisfactoria, aparición en bandeja DCAV, aprobación, habilitación AOCR/CyL y devolución.
5. Vigilar errores de transición; no continuar si el estado central no se persiste.

## Cierre de Módulos 7 y 8

La emisión se libera solamente cuando existen las últimas firmas de `RECONOCIMIENTO` y `CONDICIONES_LIMITACIONES`. Cada evidencia debe pertenecer a la solicitud, registrar hash SHA-256, tamaño, rol firmante, fecha y una ruta existente. Una NC vigente distinta de `CERRADA`, `CERRADO` o `ANULADA` bloquea el cierre.

La descarga de Condiciones y Limitaciones es una operación de lectura y no cambia el expediente a `FINALIZADO`. El cierre se decide en `AocrFinalizacionService` después de la segunda firma institucional. El estado central avanza a `PENDIENTE_GENERACION_CYL`, `CYL_FIRMADAS` o `DOCUMENTACION_FINAL_COMPLETA` según corresponda.

## Destinatarios y documentos

Las notificaciones institucionales incluyen primero a `DirectorCertificacionesDcav` y conservan aliases legacy durante la transición. Se excluyen correos inválidos y duplicados mediante claves idempotentes. La entrega final al RT solo se encola después de recalcular y comparar el SHA-256 de ambos archivos.

Aplicar también `scripts/sql/013_gate_h_cierre_modulos_7_8.sql`. Su rollback elimina exclusivamente los índices incorporados por el cierre de los Módulos 7 y 8.
