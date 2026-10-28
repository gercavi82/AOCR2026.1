# Numeración AOCR por trámite original — 28/10/2026

Regla confirmada por el usuario: conservar el consecutivo del trámite original para todos los AOCR. `DGAC-GOP-2026-AOCR003` produce **DGAC-129-QTR-003** para el explotador QTR. La renovación o modificación mantiene exactamente el reconocimiento de su emisión original, aunque el nuevo trámite tenga otro consecutivo.

## Implementación

- `CertificadoDAO.AsegurarNumeroOriginal` deriva el número de `numero_solicitud` de la emisión, sin usar el id interno ni consumir otra secuencia. Se conservan el bloqueo de concurrencia y la unicidad de la emisión original.
- Admite referencias terminadas en `AOCR003` y `AOCR-003`; conserva los dígitos originales y completa a tres posiciones cuando sea necesario. Rechaza consecutivos cero, referencias sin AOCR y códigos OACI inválidos.
- La ruta heredada de Condiciones consulta el número AOCR persistido; deja de utilizar el número del AOC o inventar un número a partir del id de solicitud. Para renovaciones o modificaciones sin original vinculado se exige completar esa vinculación.
- El generador anterior y FR3 mantienen sus contratos; la secuencia creada en la migración del 27/10 se conserva por compatibilidad, pero esta asignación ya no la consume.

## Borradores existentes

Aplicar en pruebas `scripts/sql/20261028_numero_aocr_consecutivo_original.sql` después de la migración del 27/10. El script presenta al terminar las solicitudes corregidas, número anterior y nuevo. No fue ejecutado en producción durante esta implementación.

La regularización solo modifica numeración interna conocida de borradores editables. Corrige en conjunto emisión y renovaciones/modificaciones que comparten esa referencia. Si algún miembro del grupo tiene un documento enviado, bloqueado o firmado, se conserva todo el grupo para revisión manual. También se contempla el circuito heredado de CL. Se aborta ante referencias ambiguas o colisiones con otra emisión original.

Se conservan archivos y registros históricos: los PDF previos dejan de estar vigentes y debe pulsarse **Regenerar PDF** para producir AOCR y CL con el número corregido. El certificado borrador pierde la ruta al PDF anterior para impedir que se sirva como alternativa vigente. El disparador de inmutabilidad queda habilitado al terminar; cualquier error revierte la transacción. Se requiere una cuenta con permisos administrativos para ejecutar esta migración.

Los firmados no se renumeran. Para ellos, la regla exige revisar el caso institucionalmente antes de emitir una corrección o nueva versión.

## Pruebas

Resultado: **88 pruebas automatizadas y 135 comprobaciones PostgreSQL correctas**. Evidencias locales: `.tmp/test-results/numero-original-final2.trx` y `.tmp/test-numero-original-pg.log`. Compilación Release preparada en `public/public1/bin`; la publicación y la ejecución del SQL en el servidor siguen pendientes.

- Pruebas de número puro: referencia institucional, referencia corta, relleno mínimo, consecutivo de cuatro dígitos y entradas inválidas.
- PostgreSQL temporal: emisión AOCR003 con id interno 201 produce QTR-003; renovación y modificación conservan ese número; concurrencia sin consumir contador nuevo; conservación de firmados.
- Regularización: emisión y renovación editables, retiro de PDF anteriores sin reescritura, protección de un grupo con renovación enviada y de CL heredada remitida, certificado firmado intacto, repetición idempotente y disparador de inmutabilidad activo.

Este cambio cubre la aclaración de numeración. Las otras observaciones del PDF 20 —archivo documental, operaciones múltiples por estación, formato y circuito completo de revisión— continúan pendientes de implementación/verificación.
