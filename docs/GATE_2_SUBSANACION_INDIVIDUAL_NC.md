# GATE 2 — Subsanación individual vinculada a NC

## Línea base y resultado

- Rama: `firma-dirdac-tec`.
- Commit base: `a3ac9b6fe87299121a02ecee87b45b23b0c90fd6`.
- Línea base oficial del checkout: 263 pruebas, 243 aprobadas, 19 fallidas y 1 omitida.
- Suite tras GATE 1 y GATE 2: 269 pruebas, 249 aprobadas, 19 fallidas y 1 omitida.
- Las cifras históricas 91/588 no son reproducibles y no se usan como referencia.

## Comportamiento implementado

El flujo individual existente de `SolicitudAOCR/Subsanar` se conserva. Cuando la solicitud tiene una NC
`SIN_INSPECCION` en `FIRMADA_COORDINADOR`, `EN_SUBSANACION` o `SUBSANACION_DEVUELTA`, cada reemplazo se registra
mediante una única transacción PostgreSQL:

1. bloquea la versión observada;
2. verifica solicitud, NC, ruta y estado habilitante;
3. rechaza documentos aceptados/aprobados;
4. crea la versión N+1 pendiente de revisión;
5. conserva la versión N como histórica;
6. registra NC, observación, nombres, tamaño, SHA-256, usuario, fecha y correlación;
7. confirma todo o revierte todo.

Si falla la transacción, el controlador elimina como compensación el archivo físico recién almacenado. Las
subsanaciones documentales ordinarias sin NC mantienen el comportamiento previo.

La pantalla presenta tipo, nombre, estado, observación, versión, fecha, acciones permitidas y el historial de
versiones. Los documentos aceptados siguen visibles únicamente para consulta/descarga.

## Seguridad e integridad

- autorización por roles externos y validación de propietario de la solicitud;
- almacenamiento privado bajo `App_Data`;
- nombre físico aleatorio;
- lista blanca de extensión/MIME, límite de tamaño y bytes mágicos;
- SHA-256 persistido en minúsculas;
- FKs a NC y a ambas versiones documentales;
- unicidad de la nueva versión en la bitácora;
- CHECK que exige relación y sucesión N/N+1 para registros individuales.

## SQL y validación

- `scripts/sql/015_gate2_subsanacion_individual_nc.sql` se ejecutó dos veces sin errores en `dgac_des`.
- `scripts/sql/015_gate2_subsanacion_individual_nc_rollback.sql` se validó en la base desechable
  `aocr_gate2_194bda4cae59`, eliminada al finalizar.
- El rollback es no destructivo: elimina objetos de integridad Gate 2 y conserva columnas/datos de auditoría.

## Pruebas

`Gate2SubsanacionIndividualNcIntegrationTests` valida contra PostgreSQL real:

- columnas, restricciones e índices;
- rollback del DAO ante NC inválida sin alterar conteo ni estado del documento;
- rechazo de vínculos individuales incompletos.

Build Debug, Build Release y precompilación Razor aprobaron. Permanece la advertencia preexistente de
`itext.commons` respecto de `System.IO.Compression`.

Los 19 fallos globales son los mismos de la línea base: 17 externos (AS400/configuración y dato financiero) y
2 de caracterización/contrato del flujo insatisfactorio. No apareció ninguna regresión nueva. La prueba omitida
sigue siendo `FlujoCompleto_CrearOrdenHastaPago_Exitoso`.
