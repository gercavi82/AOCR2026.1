# GATE 1 - Consistencia SQL y relaciones de No Conformidad

## Línea base reproducible

- Rama: `firma-dirdac-tec`.
- Commit inicial: `a3ac9b6fe87299121a02ecee87b45b23b0c90fd6`.
- Suite inicial reproducible: 263 pruebas; 243 aprobadas, 19 fallidas y 1 omitida.
- Las cifras históricas 91/588 quedan descartadas hasta que exista un proyecto, ensamblado o comando que las reproduzca.

Los 19 fallos base se clasifican en 17 externos (16 por AS400 no configurado y uno de pago sin comprobante), dos relacionados con caracterización/autorización del flujo insatisfactorio ya existentes y cero regresiones nuevas. La prueba omitida es `FlujoCompleto_CrearOrdenHastaPago_Exitoso`.

## Relaciones incorporadas

`aocr_tbnoconformidad` conserva las claves legacy y agrega raíz de NC, solicitud/inspección/informe de origen, solicitud e inspección de reevaluación, informe de cierre, ciclo, datos de cierre y correlación.

La raíz y versión identifican una cadena formal de versiones. Los índices parciales impiden más de una solicitud activa por raíz, reutilizar una solicitud nueva en dos NC y repetir un `correlation_id`. `VincularNuevaEvaluacion` requiere una transacción y permite repetir idempotentemente el mismo vínculo.

Las FK de nuevas relaciones y cierre protegen nuevas escrituras. Las tres FK de origen no se habilitan todavía porque existen NC legacy cuyos códigos de solicitud, inspección o informe no están presentes en las tablas institucionales actuales; una FK `NOT VALID` también impediría actualizar esas filas. Las columnas de origen e índices sí quedan disponibles para reconciliación posterior.

## SQL y validación

- Migración: `scripts/sql/014_gate_nc_relaciones.sql`.
- Rollback: `scripts/sql/014_gate_nc_relaciones_rollback.sql`.
- La migración se ejecutó dos veces sobre `dgac_des` sin errores.
- El rollback se validó en una base temporal: conservó columnas y filas, eliminó constraints, trigger e índices nuevos, y la base temporal fue destruida.

## Resultado final

Se agregaron tres pruebas PostgreSQL transaccionales. La suite final contiene 266 pruebas: 246 aprobadas, los mismos 19 fallos base y 1 omitida. No existen fallos nuevos ni fallos base desaparecidos de forma artificial.
