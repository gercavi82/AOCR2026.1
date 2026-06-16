# Plan de limpieza transaccional AOCR - 2026-06-15

## Alcance

Objetivo: dejar la base `dgac_des` sin datos operativos de prueba del flujo AOCR, conservando seguridad, usuarios, roles, permisos, menus, parametros, catalogos y configuracion institucional.

No se ejecuto ninguna limpieza contra la base. Solo se ejecutaron consultas de inventario, FKs, secuencias y conteos estimados/diagnosticos.

## Base diagnosticada

- Host: `172.20.16.55`
- Puerto: `5432`
- Base: `dgac_des`
- Esquema: `public`
- Fecha: `2026-06-15`

## Entregables generados

- `scripts/db/20260615_aocr_limpieza_00_respaldo.ps1`
- `scripts/db/20260615_aocr_limpieza_01_inventario.sql`
- `scripts/db/20260615_aocr_limpieza_02_conteo_previo.sql`
- `scripts/db/20260615_aocr_limpieza_03_limpieza_transaccional_rollback.sql`
- `scripts/db/20260615_aocr_limpieza_04_reiniciar_secuencias_rollback.sql`
- `scripts/db/20260615_aocr_limpieza_05_validacion_post.sql`

## Inventario real de tablas

Tablas detectadas en `public`: 78.

Tablas con datos transaccionales relevantes observados en el diagnostico:

| Tabla | Conteo estimado observado |
|---|---:|
| `aocr_tbsolicitud` | 1 |
| `aocr_tbdocumento` | 13 |
| `aocr_tbrevision_documental` | 14 |
| `aocr_tbinspeccion` | 1 |
| `aocr_tblv_operacional_eae` | 1 |
| `aocr_tbinforme_inspeccion` | 1 |
| `aocr_tbhistorial_documental` | 21 |
| `aocr_tbhistorial_estado` | 10 |
| `aocr_tbhistorial_estado_inspeccion` | 5 |
| `aocr_tbnotificacion` | 9 |
| `email_queue` | 15 |
| `aocr_or_orden` | 1 |
| `aocr_or_orden_detalle` | 4 |
| `aocr_tbpago` | 1 |
| `aocr_tbauditoria` | 19 |
| `aocr_tblog` | 108 |
| `aocr_audit_trail` | 6 |
| `aocr_tbaeronave_solicitud` | 2 |

Para conteos exactos antes de borrar, ejecutar `20260615_aocr_limpieza_02_conteo_previo.sql`.

## Grupo A - Tablas protegidas, no tocar

Seguridad, login, roles, permisos y menus:

- `usuario`
- `usuario_rol`
- `rol`
- `permisos`
- `seguridad_permiso`
- `seguridad_rol_permiso`
- `menu`
- `submenu`
- `auditoria_seguridad`
- `usuario_as400`
- `usuario_as400_adicional`
- `usuario_backup_eliminados`

Configuracion, parametros, catalogos y maestros operativos:

- `aocr_or_concepto`
- `aocr_tbparametro`
- `parametros`
- `aocr_tbcorreo_institucional`
- `aocr_tbinspectores`
- `aocr_tbfirma_posicion_documento`
- `aocr_usuario_compania_rt`
- `aocr_usuario_interno_rt`
- `aocr_asignacion_rt`
- `aocr_usuario_transferencia`
- `aocr_usuario_transferencia_detalle`
- `sync_state`
- `conceptos`
- `contribuyentes`

Notas:

- `aocr_or_concepto` contiene conceptos base de recaudacion y se protege.
- `aocr_tbfirma_posicion_documento` parece configuracion de posiciones de firma, no documento firmado; se protege.
- `aocr_usuario_compania_rt`, `aocr_usuario_interno_rt` y `aocr_asignacion_rt` se protegen por relacion usuario/RT/compania.

## Grupo B - Tablas transaccionales a limpiar

Incluidas en `20260615_aocr_limpieza_03_limpieza_transaccional_rollback.sql`:

- `email_attachment`
- `email_queue`
- `aocr_tbnotificacion`
- `aocr_tbhistorial_documental`
- `aocr_tbhistorial_estado_inspeccion`
- `aocr_tbhistorial_estado`
- `aocr_audit_trail`
- `aocr_tbauditoria`
- `aocr_tblog`
- `aocr_declaracion_historial`
- `aocr_declaracion_tmp`
- `aocr_idempotency_key`
- `aocr_sync_log`
- `aocr_tb_sync_log`
- `sync_log`
- `aocr_tbcorreo_institucional_historial`
- `aocr_tbfirma_documento`
- `aocr_tbdocumento_subsanacion`
- `aocr_tbsubsanacion`
- `aocr_tbrevision_documental`
- `aocr_tbdocumento_inspeccion`
- `aocr_tbdocumento`
- `aocr_tbchecklist_solicitud`
- `aocr_tbchecklist_item`
- `aocr_tbchecklist`
- `aocr_tbhallazgo`
- `aocr_tbinforme_inspeccion`
- `aocr_tbinforme`
- `aocr_tblv_operacional_eae`
- `aocr_tbobservacion`
- `aocr_tbcertificado`
- `aocr_tbaeronave_solicitud`
- `aocr_tbaeronave`
- `aocr_tb_factura_pago`
- `aocr_tbpago`
- `aocr_or_orden_detalle`
- `aocr_or_orden`
- `aocr_orden_recaudacion`
- `detalles_orden`
- `historial_estados_orden`
- `pagos`
- `ordenes_recaudacion`
- `fr3_detalle_pg`
- `fr3_pg`
- `fr3_detalle`
- `fr3`
- `aocr_tbviatico`
- `aocr_tbinspeccion`
- `aocr_tbsolicitud`

## Grupo C - Tablas dudosas no incluidas en limpieza automatica

Estas tablas no se limpian automaticamente porque pueden contener configuracion, maestros, relaciones de usuarios o integracion:

- `aocr_asignacion_rt`
- `aocr_tbfirma_posicion_documento`
- `aocr_tbcorreo_institucional`
- `aocr_tbinspectores`
- `aocr_tbparametro`
- `aocr_usuario_compania_rt`
- `aocr_usuario_interno_rt`
- `aocr_usuario_transferencia`
- `aocr_usuario_transferencia_detalle`
- `auditoria_seguridad`
- `conceptos`
- `contribuyentes`
- `parametros`
- `sync_state`
- `usuario_as400`
- `usuario_as400_adicional`

## Orden de ejecucion recomendado

1. Ejecutar respaldo completo y respaldo transaccional:
   `scripts/db/20260615_aocr_limpieza_00_respaldo.ps1`
2. Ejecutar inventario:
   `scripts/db/20260615_aocr_limpieza_01_inventario.sql`
3. Ejecutar conteo previo:
   `scripts/db/20260615_aocr_limpieza_02_conteo_previo.sql`
4. Ejecutar limpieza de prueba:
   `scripts/db/20260615_aocr_limpieza_03_limpieza_transaccional_rollback.sql`
5. Revisar resultados. Debe terminar en `ROLLBACK`.
6. Si todo es correcto, cambiar manualmente `ROLLBACK` por `COMMIT` en una copia del script y ejecutar.
7. Ejecutar reinicio de secuencias de prueba:
   `scripts/db/20260615_aocr_limpieza_04_reiniciar_secuencias_rollback.sql`
8. Si procede, cambiar manualmente `ROLLBACK` por `COMMIT` en una copia y ejecutar.
9. Ejecutar validacion posterior:
   `scripts/db/20260615_aocr_limpieza_05_validacion_post.sql`
10. Validar login, navegacion, sidebar, creacion de nueva solicitud, orden, carga documental y ausencia de errores 500.

## Limpieza de archivos fisicos

No se eliminaron archivos fisicos.

Antes de limpiar archivos:

1. Respaldar `CapaPresentacion/App_Data/Uploads`.
2. Confirmar que los archivos son de prueba.
3. Eliminar contenido generado, no carpetas.
4. Mantener permisos del App Pool.

Rutas a revisar:

- `CapaPresentacion/App_Data/Uploads/Inspecciones`
- `CapaPresentacion/App_Data/Uploads/Inspecciones/InformesTecnicos`
- `CapaPresentacion/App_Data/Uploads/Inspecciones/InformesTecnicos/Firmados`
- `CapaPresentacion/App_Data/Uploads/Inspecciones/ListasVerificacionEae`
- `CapaPresentacion/App_Data/AOCR`
- `CapaPresentacion/App_Data/TempPdf`

## Confirmacion de seguridad

Los scripts generados no contienen:

- `DROP TABLE`
- `DROP SCHEMA`
- `TRUNCATE`
- `CASCADE`
- `ALTER TABLE DROP CONSTRAINT`
- `DELETE` sobre `usuario`, `rol`, `permisos`, `seguridad_permiso`, `seguridad_rol_permiso`, `menu`, `submenu`, `aocr_or_concepto`, `aocr_tbcorreo_institucional`, `aocr_tbinspectores`, `aocr_usuario_compania_rt` ni `aocr_usuario_interno_rt`.

La limpieza queda preparada, reversible en primera ejecucion y con `ROLLBACK` activo por defecto.
