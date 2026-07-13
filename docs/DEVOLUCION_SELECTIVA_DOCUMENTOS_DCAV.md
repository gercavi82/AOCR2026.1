# Devolución selectiva de documentos DCAV

La fase implementa la devolución de AOCR, Condiciones y Limitaciones, o ambos, sin modificar la versión ni el PDF enviados.

## Contrato

- El documento observado pasa a histórico `OBSERVADO_DCAV` y se crea una nueva versión vigente `CORRECCION_INSPECTOR`, sin archivo ni hash heredados.
- El documento no observado conserva ID, versión y PDF, queda `APROBADO_DCAV` y no es editable.
- El expediente pasa de `PENDIENTE_REVISION_DOCUMENTOS_DCAV` a `DOCUMENTOS_OBSERVADOS_DCAV`.
- Cada observación se guarda en `aocr_tbobservacion.mensaje` como JSON con esquema `DCAV_DOCUMENTAL_V1`. No se agregan tablas ni columnas.
- El ciclo es `ABIERTA`, `ATENDIDA_INSPECTOR`, `CERRADA_DCAV`.
- El reenvío admite una versión nueva `GENERADO` y una versión previa `APROBADO_DCAV`; exige al menos una corrección nueva y reutiliza el PDF aprobado.

## Operación

Ejecutar `scripts/009_devolucion_selectiva_dcav.sql`. El script sólo valida el esquema existente y crea un índice parcial. El rollback elimina únicamente ese índice y no intenta revertir decisiones funcionales.

La operación de negocio usa aislamiento serializable, bloqueo asesor por expediente, versión esperada, clave idempotente, historial, auditoría y notificación dentro de la misma transacción.

## Validación

Compilar `AOCR.sln`, ejecutar `DevolucionDocumentosDcavTests` y comprobar que el índice `idx_aocr_observacion_dcav_documental` exista. Una devolución funcional requiere un expediente real en `PENDIENTE_REVISION_DOCUMENTOS_DCAV`; no se fabrican datos de negocio para la prueba.
